using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BTCPayServer.Lightning;
using BTCPayServer.Plugins.LNbank.Data.Models;
using BTCPayServer.Plugins.LNbank.Exceptions;
using BTCPayServer.Plugins.LNbank.Hubs;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.Extensions.Logging;
using NBitcoin;
using NBitcoin.DataEncoders;
using Transaction = BTCPayServer.Plugins.LNbank.Data.Models.Transaction;

namespace BTCPayServer.Plugins.LNbank.Services.Wallets;

public class WalletService
{
    private readonly ILogger _logger;
    private readonly BTCPayService _btcpayService;
    private readonly LNbankPluginDbContextFactory _dbContextFactory;
    private readonly IHubContext<TransactionHub> _transactionHub;
    private readonly Network _network;

    public static readonly TimeSpan SendTimeout = TimeSpan.FromSeconds(20);

    public WalletService(
        ILogger<WalletService> logger,
        IHubContext<TransactionHub> transactionHub,
        BTCPayService btcpayService,
        BTCPayNetworkProvider btcPayNetworkProvider,
        LNbankPluginDbContextFactory dbContextFactory)
    {
        _logger = logger;
        _btcpayService = btcpayService;
        _dbContextFactory = dbContextFactory;
        _transactionHub = transactionHub;
        _network = btcPayNetworkProvider.GetNetwork<BTCPayNetwork>(BTCPayService.CryptoCode).NBitcoinNetwork;
    }

    public async Task<IEnumerable<Wallet>> GetWallets(WalletsQuery query)
    {
        await using var dbContext = _dbContextFactory.CreateContext();
        var wallets = await FilterWallets(dbContext.Wallets.AsQueryable(), query).ToListAsync();
        return wallets.Select(wallet =>
        {
            wallet.AccessLevel = wallet.AccessKeys.Single(ak => query.UserId.Contains(ak.UserId)).Level;
            return wallet;
        });
    }

    private IQueryable<Wallet> FilterWallets(IQueryable<Wallet> queryable, WalletsQuery query)
    {
        if (query.UserId != null)
        {
            queryable = queryable
                .Include(w => w.AccessKeys)
                .Where(w => w.AccessKeys.Any(ak => query.UserId.Contains(ak.UserId)));
        }
        
        if (query.AccessKey != null)
        {
            queryable = queryable.Include(wallet => wallet.AccessKeys).Where(wallet =>
                wallet.AccessKeys.Any(key => query.AccessKey.Contains(key.Key)));
        }

        if (query.WalletId != null)
        {
            queryable = queryable.Where(wallet => query.WalletId.Contains(wallet.WalletId));
        }

        if (query.IncludeTransactions)
        {
            queryable = queryable.Include(w => w.Transactions).AsNoTracking();
        }

        if (query.IncludeAccessKeys)
        {
            queryable = queryable.Include(w => w.AccessKeys).AsNoTracking();
        }

        return queryable;
    }

    public async Task<Wallet> GetWallet(WalletsQuery query)
    {
        await using var dbContext = _dbContextFactory.CreateContext();
        var wallet = await FilterWallets(dbContext.Wallets.AsQueryable(), query).FirstOrDefaultAsync();
        if (wallet != null && query.UserId != null)
        {
            wallet.AccessLevel = wallet.AccessKeys.Single(ak => query.UserId.Contains(ak.UserId)).Level;
        }
        return wallet;
    }

    public async Task<Transaction> Receive(Wallet wallet, long amount, string description, bool attachDescription, bool privateRouteHints, TimeSpan? expiry, CancellationToken cancellationToken = default) =>
        await Receive(wallet, amount, description, null, attachDescription, privateRouteHints, expiry, cancellationToken);
    
    public async Task<Transaction> Receive(Wallet wallet, long amount, string description, uint256 descriptionHash, CancellationToken cancellationToken = default) =>
        await Receive(wallet, amount, description, descriptionHash, false, false, null, cancellationToken);
        
    public async Task<Transaction> Receive(Wallet wallet, long amount, uint256 descriptionHash, bool privateRouteHints, TimeSpan? expiry, CancellationToken cancellationToken = default) =>
        await Receive(wallet, amount, null, descriptionHash, false, privateRouteHints, expiry, cancellationToken);

    private async Task<Transaction> Receive(Wallet wallet, long amount, string description, uint256 descriptionHash, bool attachDescription, bool privateRouteHints, TimeSpan? expiry, CancellationToken cancellationToken = default)
    {
        await using var dbContext = _dbContextFactory.CreateContext();
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

        var entry = await dbContext.Transactions.AddAsync(new Transaction
        {
            WalletId = wallet.WalletId,
            InvoiceId = data.Id,
            Amount = data.Amount,
            ExpiresAt = data.ExpiresAt,
            PaymentRequest = data.BOLT11,
            Description = description
        }, cancellationToken);

        await dbContext.SaveChangesAsync(cancellationToken);

        return entry.Entity;
    }

    public async Task<Transaction> Send(Wallet wallet, BOLT11PaymentRequest bolt11, string paymentRequest, string description, LightMoney explicitAmount = null, float maxFeePercent = 3, CancellationToken cancellationToken = default)
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
        var receivingTransaction = await ValidatePaymentRequest(paymentRequest);
        var isInternal = !string.IsNullOrEmpty(receivingTransaction?.InvoiceId);

        var sendingTransaction = new Transaction
        {
            WalletId = wallet.WalletId,
            PaymentRequest = paymentRequest,
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
        Transaction transaction = await GetTransaction(new TransactionQuery
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

    public async Task<Wallet> AddOrUpdateWallet(Wallet wallet)
    {
        await using var dbContext = _dbContextFactory.CreateContext();

        EntityEntry entry;
        if (string.IsNullOrEmpty(wallet.WalletId))
        {
            wallet.AccessKeys ??= new List<AccessKey>();
            wallet.AccessKeys.Add(new AccessKey
            {
                UserId = wallet.UserId,
                Level = AccessLevel.Admin
            });
            entry = await dbContext.Wallets.AddAsync(wallet);
        }
        else
        {
            entry = dbContext.Update(wallet);
        }
        await dbContext.SaveChangesAsync();

        return (Wallet)entry.Entity;
    }

    public async Task<AccessKey> AddOrUpdateAccessKey(string walletId, string userId, AccessLevel level)
    {
        await using var dbContext = _dbContextFactory.CreateContext();
        var accessKey = await dbContext.AccessKeys.FirstOrDefaultAsync(a => a.WalletId == walletId && a.UserId == userId);

        if (accessKey == null)
        {
            accessKey = new AccessKey
            {
                UserId = userId,
                WalletId = walletId,
                Level = level
            };
            await dbContext.AccessKeys.AddAsync(accessKey);
        }
        else if (accessKey.Level != level)
        {
            accessKey.Level = level;
            dbContext.Update(accessKey);
        }
        await dbContext.SaveChangesAsync();

        return accessKey;
    }

    public async Task DeleteAccessKey(string walletId, string key)
    {
        await using var dbContext = _dbContextFactory.CreateContext();
        var accessKey = await dbContext.AccessKeys.FirstAsync(a => a.WalletId == walletId && a.Key == key);

        dbContext.AccessKeys.Remove(accessKey);
        await dbContext.SaveChangesAsync();
    }

    public async Task RemoveWallet(Wallet wallet)
    {
        if (wallet.Balance > 0)
        {
            throw new Exception("This wallet still has a balance.");
        }
        
        wallet.IsSoftDeleted = true;
        await AddOrUpdateWallet(wallet);
    }

    public async Task<IEnumerable<Transaction>> GetPendingTransactions()
    {
        return await GetTransactions(new TransactionsQuery
        {
            IncludingPending = true,
            IncludingExpired = false,
            IncludingInvalid = false,
            IncludingCancelled = false,
            IncludingPaid = false
        });
    }
    
    public async Task<Transaction> GetTransaction(TransactionQuery query)
    {
        await using var dbContext = _dbContextFactory.CreateContext();
        IQueryable<Transaction> queryable = dbContext.Transactions.AsQueryable();

        if (query.WalletId != null)
        {
            var walletQuery = new WalletsQuery
            {
                WalletId = new[] { query.WalletId },
                IncludeTransactions = true
            };

            if (query.UserId != null) walletQuery.UserId = new[] { query.UserId };

            var wallet = await GetWallet(walletQuery);

            if (wallet == null) return null;

            queryable = wallet.Transactions.AsQueryable();
        }

        if (query.InvoiceId != null)
        {
            queryable = queryable.Where(t => t.InvoiceId == query.InvoiceId);
        }
        else if (query.HasInvoiceId)
        {
            queryable = queryable.Where(t => t.InvoiceId != null);
        }

        if (query.TransactionId != null)
        {
            queryable = queryable.Where(t => t.TransactionId == query.TransactionId);
        }

        if (query.PaymentRequest != null)
        {
            queryable = queryable.Where(t => t.PaymentRequest == query.PaymentRequest);
        }

        return queryable.FirstOrDefault();
    }

    public async Task<Transaction> UpdateTransaction(Transaction transaction)
    {
        await using var dbContext = _dbContextFactory.CreateContext();
        var entry = dbContext.Entry(transaction);
        entry.State = EntityState.Modified;

        await dbContext.SaveChangesAsync();

        return entry.Entity;
    }

    public BOLT11PaymentRequest ParsePaymentRequest(string payReq)
    {
        return BOLT11PaymentRequest.Parse(payReq.Trim(), _network);
    }

    private async Task<IEnumerable<Transaction>> GetTransactions(TransactionsQuery query)
    {
        await using var dbContext = _dbContextFactory.CreateContext();
        var queryable = dbContext.Transactions.AsQueryable();

        if (query.UserId != null) query.IncludeWallet = true;

        if (query.WalletId != null)
        {
            queryable = queryable.Where(t => t.WalletId == query.WalletId);
        }
        
        if (query.IncludeWallet)
        {
            queryable = queryable.Include(t => t.Wallet).AsNoTracking();
        }
        
        if (query.UserId != null)
        {
            queryable = queryable.Where(t => t.Wallet.UserId == query.UserId);
        }

        if (!query.IncludingPaid)
        {
            queryable = queryable.Where(t => t.PaidAt == null);
        }

        if (!query.IncludingPending)
        {
            queryable = queryable.Where(t => t.PaidAt != null);
        }

        if (!query.IncludingCancelled)
        {
            queryable = queryable.Where(t => t.ExplicitStatus != Transaction.StatusCancelled);
        }

        if (!query.IncludingInvalid)
        {
            queryable = queryable.Where(t => t.ExplicitStatus != Transaction.StatusInvalid);
        }

        if (!query.IncludingExpired)
        {
            var enumerable = queryable.AsEnumerable(); // Switch to client side filtering
            return enumerable.Where(t => t.ExpiresAt > DateTimeOffset.UtcNow || t.ExplicitStatus != null).ToList();
        }

        return await queryable.ToListAsync();
    }

    public async Task<bool> Cancel(string invoiceId)
    {
        var transaction = await GetTransaction(new TransactionQuery { InvoiceId = invoiceId });
        
        return await Cancel(transaction);
    }
    
    public async Task<bool> Cancel(Transaction transaction)
    {
        var result = transaction.SetCancelled();
        if (result)
        {
            await UpdateTransaction(transaction);
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
            await UpdateTransaction(transaction);
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
            await UpdateTransaction(transaction);
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
