using System;
using System.Threading;
using System.Threading.Tasks;
using BTCPayServer.Client;
using BTCPayServer.Lightning;
using BTCPayServer.Plugins.LNbank.Data.Models;
using BTCPayServer.Plugins.LNbank.Exceptions;
using BTCPayServer.Plugins.LNbank.Hubs;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Transaction = BTCPayServer.Plugins.LNbank.Data.Models.Transaction;

namespace BTCPayServer.Plugins.LNbank.Services.Wallets;

public class WalletService
{
    private readonly ILogger _logger;
    private readonly Network _network;
    private readonly BTCPayService _btcpayService;
    private readonly LNURLService _lnurlService;
    private readonly WalletRepository _walletRepository;
    private readonly IHubContext<TransactionHub> _transactionHub;
    private readonly LNbankPluginDbContextFactory _dbContextFactory;

    public static readonly TimeSpan SendTimeout = TimeSpan.FromSeconds(20);

    public WalletService(
        ILogger<WalletService> logger,
        IHubContext<TransactionHub> transactionHub,
        BTCPayService btcpayService,
        BTCPayNetworkProvider btcPayNetworkProvider,
        LNbankPluginDbContextFactory dbContextFactory,
        WalletRepository walletRepository,
        LNURLService lnurlService)
    {
        _logger = logger;
        _btcpayService = btcpayService;
        _transactionHub = transactionHub;
        _walletRepository = walletRepository;
        _dbContextFactory = dbContextFactory;
        _lnurlService = lnurlService;
        _network = btcPayNetworkProvider.GetNetwork<BTCPayNetwork>(BTCPayService.CryptoCode).NBitcoinNetwork;
    }

    public async Task<bool> IsPaid(string paymentHash)
    {
        var transaction = await _walletRepository.GetTransaction(new TransactionQuery
        {
            PaymentHash = paymentHash
        });
        if (transaction != null) return transaction.IsPaid;
        
        var payment = await _btcpayService.GetLightningPayment(paymentHash);
        return payment?.Status == LightningPaymentStatus.Complete;
    }
    
    public async Task<Transaction> Receive(Wallet wallet, long amount, string description, bool attachDescription, bool privateRouteHints, TimeSpan? expiry, CancellationToken cancellationToken = default) =>
        await Receive(wallet, amount, description, null, attachDescription, privateRouteHints, expiry, cancellationToken);
    
    public async Task<Transaction> Receive(Wallet wallet, long amount, string description, uint256 descriptionHash, CancellationToken cancellationToken = default) =>
        await Receive(wallet, amount, description, descriptionHash, false, false, null, cancellationToken);
        
    public async Task<Transaction> Receive(Wallet wallet, long amount, uint256 descriptionHash, bool privateRouteHints, TimeSpan? expiry, CancellationToken cancellationToken = default) =>
        await Receive(wallet, amount, null, descriptionHash, false, privateRouteHints, expiry, cancellationToken);

    private async Task<Transaction> Receive(Wallet wallet, long amount, string description, uint256 descriptionHash, bool attachDescription, bool privateRouteHints, TimeSpan? expiry, CancellationToken cancellationToken = default)
    {
        if (amount < 0) throw new ArgumentException("Amount should be a non-negative value", nameof(amount));
        if (expiry <= TimeSpan.Zero) throw new ArgumentException("Expiry should be more than 0", nameof(expiry));

        var desc = attachDescription && !string.IsNullOrEmpty(description) ? description : string.Empty;
        var data = await _btcpayService.CreateLightningInvoice(new LightningInvoiceCreateRequest
        {
            Amount = amount,
            Description = desc,
            DescriptionHash = descriptionHash,
            PrivateRouteHints = privateRouteHints,
            Expiry = expiry ?? LightningInvoiceCreateRequest.ExpiryDefault
        });

        await using var dbContext = _dbContextFactory.CreateContext();
        var bolt11 = ParsePaymentRequest(data.BOLT11);
        var entry = await dbContext.Transactions.AddAsync(new Transaction
        {
            WalletId = wallet.WalletId,
            InvoiceId = data.Id,
            Amount = data.Amount,
            ExpiresAt = data.ExpiresAt,
            PaymentRequest = data.BOLT11,
            PaymentHash = bolt11.PaymentHash?.ToString(),
            Description = description
        }, cancellationToken);

        await dbContext.SaveChangesAsync(cancellationToken);

        return entry.Entity;
    }
    
    public async Task<Transaction> Send(Wallet wallet, string paymentRequest)
    {
        var bolt11 = ParsePaymentRequest(paymentRequest);
        return await Send(wallet, bolt11, bolt11.ShortDescription);
    }

    public async Task<Transaction> Send(Wallet wallet, BOLT11PaymentRequest bolt11, string description, LightMoney explicitAmount = null, float maxFeePercent = 3, CancellationToken cancellationToken = default)
    {
        if (bolt11.ExpiryDate <= DateTimeOffset.UtcNow)
        {
            throw new PaymentRequestValidationException($"Payment request already expired at {bolt11.ExpiryDate}.");
        }

        // check balance
        var amount = bolt11.MinimumAmount == LightMoney.Zero ? explicitAmount : bolt11.MinimumAmount;
        if (wallet.Balance < amount)
        {
            throw new InsufficientBalanceException($"Insufficient balance: {Sats(wallet.Balance)} — tried to send {Sats(amount)}.");
        }

        // check if the invoice exists already
        var paymentRequest = bolt11.ToString();
        var receivingTransaction = await ValidatePaymentRequest(paymentRequest);
        var isInternal = !string.IsNullOrEmpty(receivingTransaction?.InvoiceId);

        var sendingTransaction = new Transaction
        {
            WalletId = wallet.WalletId,
            PaymentRequest = paymentRequest,
            PaymentHash = bolt11.PaymentHash?.ToString(),
            ExpiresAt = bolt11.ExpiryDate,
            Description = description,
            Amount = amount,
            AmountSettled = new LightMoney(amount.MilliSatoshi * -1),
        };
        
        return await (isInternal
            ? SendInternal(sendingTransaction, receivingTransaction, cancellationToken)
            : SendExternal(sendingTransaction, amount, wallet.Balance, maxFeePercent, cancellationToken));
    }

    private async Task<Transaction> SendInternal(Transaction sendingTransaction, Transaction receivingTransaction, CancellationToken cancellationToken = default)
    {
        Transaction transaction = null;
        await using var dbContext = _dbContextFactory.CreateContext();
        var executionStrategy = dbContext.Database.CreateExecutionStrategy();
        var isSettled = false;
        
        await executionStrategy.ExecuteAsync(async () =>
        {
            await using var dbTransaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
            try
            {
                var now = DateTimeOffset.UtcNow;

                var receiveEntry = dbContext.Entry(receivingTransaction);
                var sendingEntry = await dbContext.Transactions.AddAsync(sendingTransaction, cancellationToken);
                
                sendingEntry.Entity.SetSettled(sendingTransaction.Amount, sendingTransaction.AmountSettled, null, now);
                receiveEntry.Entity.SetSettled(sendingTransaction.Amount, sendingTransaction.Amount, null, now);
                receiveEntry.State = EntityState.Modified;
                await dbContext.SaveChangesAsync(cancellationToken);
                await dbTransaction.CommitAsync(cancellationToken);
                
                _logger.LogInformation("Settled transaction {TransactionId} internally. Paid by {SendingTransactionId}", 
                    receivingTransaction.TransactionId, sendingTransaction.TransactionId);

                transaction = sendingEntry.Entity;
                isSettled = transaction.IsSettled;
            }
            catch (Exception)
            {
                await dbTransaction.RollbackAsync(cancellationToken);
                
                _logger.LogInformation("Settling transaction {TransactionId} internally failed", receivingTransaction.TransactionId);
                
                throw;
            }
        });

        if (isSettled)
        {
            await BroadcastTransactionUpdate(sendingTransaction, Transaction.StatusSettled);
            await BroadcastTransactionUpdate(receivingTransaction, Transaction.StatusSettled);
        }

        return transaction;
    }

    private async Task<Transaction> SendExternal(Transaction sendingTransaction, LightMoney amount, LightMoney walletBalance, float maxFeePercent, CancellationToken cancellationToken = default)
    {
        // Account for fees
        var maxFeeAmount = LightMoney.Satoshis(amount.ToUnit(LightMoneyUnit.Satoshi) * (decimal)maxFeePercent / 100);
        var amountWithFee = amount + maxFeeAmount;
        if (walletBalance < amountWithFee)
        {
            throw new InsufficientBalanceException(
                $"Insufficient balance: {Sats(walletBalance)} — tried to send {Sats(amount)} and need to keep a fee reserve of {Millisats(maxFeeAmount)}.");
        }

        await using var dbContext = _dbContextFactory.CreateContext();
        
        // Create preliminary transaction entry - if something fails afterwards, the LightningInvoiceWatcher will handle cleanup
        sendingTransaction.Amount = amount;
        sendingTransaction.AmountSettled = new LightMoney(amountWithFee.MilliSatoshi * -1);
        sendingTransaction.RoutingFee = maxFeeAmount;
        sendingTransaction.ExplicitStatus = Transaction.StatusPending;
        var sendingEntry = await dbContext.Transactions.AddAsync(sendingTransaction, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);

        try
        {
            // Pay the invoice - cancel after timeout, potentially caused by hold invoices
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(SendTimeout);

            // Pass explicit amount only for zero amount invoices, because the implementations might throw an exception otherwise
            var bolt11 = ParsePaymentRequest(sendingTransaction.PaymentRequest);
            var request = new LightningInvoicePayRequest
            {
                PaymentRequest = sendingTransaction.PaymentRequest,
                MaxFeePercent = maxFeePercent,
                Amount = bolt11.MinimumAmount == LightMoney.Zero ? amount : null
            };
            var result = await _btcpayService.PayLightningInvoice(request, cts.Token);

            // Check result
            if (result.TotalAmount == null)
            {
                throw new PaymentRequestValidationException("Payment request has already been paid.");
            }

            // Set amounts according to actual amounts paid, including fees
            var settledAmount = new LightMoney(result.TotalAmount * -1);
            var originalAmount = result.TotalAmount - result.FeeAmount;

            await Settle(sendingEntry.Entity, originalAmount, settledAmount, result.FeeAmount, DateTimeOffset.UtcNow);
        }
        catch (GreenfieldAPIException ex)
        {
            switch (ex.APIError.Code)
            {
                case "could-not-find-route":
                case "generic-error":
                    // Remove preliminary transaction entry, payment could not be sent
                    dbContext.Transactions.Remove(sendingTransaction);
                    await dbContext.SaveChangesAsync(cancellationToken);
                    break;
            }
            
            // Rethrow to inform about the error up in the stack
            throw;
        }
        catch (Exception ex) when (ex is TaskCanceledException or OperationCanceledException)
        {
            // Timeout, potentially caused by hold invoices
            // Payment will be saved as pending, the LightningInvoiceWatcher will handle settling/cancelling
            _logger.LogInformation("Sending transaction {TransactionId} timed out. Saved as pending", sendingEntry.Entity.TransactionId);
        }

        return sendingEntry.Entity;
    }

    public bool ValidateDescriptionHash(string paymentRequest, string metadata)
    {
        return ParsePaymentRequest(paymentRequest).VerifyDescriptionHash(metadata);
    }

    public async Task<Transaction> ValidatePaymentRequest(string paymentRequest)
    {
        Transaction transaction = await _walletRepository.GetTransaction(new TransactionQuery
        {
            PaymentRequest = paymentRequest
        });

        return transaction switch
        {
            { IsExpired: true } => throw new PaymentRequestValidationException($"Payment request already expired at {transaction.ExpiresAt}."),
            { IsSettled: true } => throw new PaymentRequestValidationException("Payment request has already been settled."),
            { IsPaid: true } => throw new PaymentRequestValidationException("Payment request has already been paid."),
            _ => transaction
        };
    }

    public BOLT11PaymentRequest ParsePaymentRequest(string payReq)
    {
        return BOLT11PaymentRequest.Parse(payReq.Trim(), _network);
    }

    public async Task<BOLT11PaymentRequest> GetBolt11(string destination)
    {
        try
        {
            return ParsePaymentRequest(destination);
        }
        catch (Exception)
        {
            return await _lnurlService.GetBolt11(destination, _network);
        }
    }

    public async Task<bool> Cancel(string invoiceId)
    {
        var transaction = await _walletRepository.GetTransaction(new TransactionQuery { InvoiceId = invoiceId });
        
        return await Cancel(transaction);
    }
        
    public async Task<bool> Expire(Transaction transaction)
    {
        var result = transaction.SetExpired();
        if (result)
        {
            await _walletRepository.UpdateTransaction(transaction);
            await BroadcastTransactionUpdate(transaction, Transaction.StatusCancelled);
        }
        
        _logger.LogInformation(
            // ReSharper disable once TemplateIsNotCompileTimeConstantProblem
            result ? "Expired transaction {TransactionId}" : "Expiring transaction {TransactionId} failed",
            transaction.TransactionId);
        
        return true;
    }
    
    public async Task<bool> Cancel(Transaction transaction)
    {
        var result = transaction.SetCancelled();
        if (result)
        {
            await _walletRepository.UpdateTransaction(transaction);
            await BroadcastTransactionUpdate(transaction, Transaction.StatusCancelled);
        }
        
        _logger.LogInformation(
            // ReSharper disable once TemplateIsNotCompileTimeConstantProblem
            result ? "Cancelled transaction {TransactionId}" : "Cancelling transaction {TransactionId} failed",
            transaction.TransactionId);
        
        return true;
    }
    
    public async Task<bool> Invalidate(Transaction transaction)
    {
        var result = transaction.SetInvalid();
        if (result)
        {
            await _walletRepository.UpdateTransaction(transaction);
            await BroadcastTransactionUpdate(transaction, Transaction.StatusInvalid);
        }
        
        _logger.LogInformation(
            // ReSharper disable once TemplateIsNotCompileTimeConstantProblem
            result ? "Invalidated transaction {TransactionId}" : "Invalidating transaction {TransactionId} failed",
            transaction.TransactionId);
        
        return result;
    }

    public async Task<bool> Settle(Transaction transaction, LightMoney amount, LightMoney amountSettled, LightMoney routingFee, DateTimeOffset date)
    {
        var result = transaction.SetSettled(amount, amountSettled, routingFee, date);
        if (result)
        {
            await _walletRepository.UpdateTransaction(transaction);
            await BroadcastTransactionUpdate(transaction, Transaction.StatusSettled);
        }
        
        _logger.LogInformation(
            // ReSharper disable once TemplateIsNotCompileTimeConstantProblem
            result ? "Settled transaction {TransactionId}" : "Settling transaction {TransactionId} failed",
            transaction.TransactionId);
        
        return result;
    }
    
    private async Task BroadcastTransactionUpdate(Transaction transaction, string eventName)
    {
        await _transactionHub.Clients.All.SendAsync("transaction-update", new
        {
            transaction.TransactionId,
            transaction.InvoiceId,
            transaction.WalletId,
            transaction.Status,
            transaction.IsPaid,
            transaction.IsExpired,
            Event = eventName
        });
    }
        
    private static string Sats(LightMoney amount) => $"{Math.Round(amount.ToUnit(LightMoneyUnit.Satoshi))} sats";
    private static string Millisats(LightMoney amount) => $"{amount.ToUnit(LightMoneyUnit.MilliSatoshi)} millisats";
}
