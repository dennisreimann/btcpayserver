using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BTCPayServer.Client;
using BTCPayServer.Lightning;
using BTCPayServer.Plugins.LNbank.Data.Models;
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
        return await FilterWallets(dbContext.Wallets.AsQueryable(), query).ToListAsync();
    }

    private IQueryable<Wallet> FilterWallets(IQueryable<Wallet> queryable, WalletsQuery query)
    {
        if (query.UserId != null)
        {
            queryable = queryable.Where(wallet => query.UserId.Contains(wallet.UserId));
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

    public async Task<Wallet> GetWallet(WalletQuery query)
    {
        await using var dbContext = _dbContextFactory.CreateContext();
        var walletsQuery = new WalletsQuery
        {
            IncludeTransactions = query.IncludeTransactions,
            IncludeAccessKeys = query.IncludeAccessKeys,
            UserId = query.UserId is null? null : new []{ query.UserId},
            WalletId = query.WalletId is null? null : new []{ query.WalletId},
            AccessKey = query.AccessKey is null? null : new []{ query.AccessKey},
        };
        return await FilterWallets(dbContext.Wallets.AsQueryable(), walletsQuery).FirstOrDefaultAsync();
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
        if (amount <= 0) throw new ArgumentException(nameof(amount));

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

    public async Task<Transaction> Send(Wallet wallet, BOLT11PaymentRequest bolt11, string paymentRequest, string description, float maxFeePercent = 3, CancellationToken cancellationToken = default)
    {
        if (bolt11.ExpiryDate <= DateTimeOffset.UtcNow)
        {
            throw new Exception($"Payment request already expired at {bolt11.ExpiryDate}.");
        }

        // check balance
        var amount = bolt11.MinimumAmount;
        if (wallet.Balance < amount)
        {
            throw new Exception($"Insufficient balance: {Sats(wallet.Balance)} — tried to send {Sats(amount)}.");
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
        
        var finalizedTransaction = await (isInternal
            ? SendInternal(sendingTransaction, receivingTransaction, amount, cancellationToken)
            : SendExternal(sendingTransaction, amount, wallet.Balance, maxFeePercent, cancellationToken));

        if (finalizedTransaction != null)
        {
            await BroadcastTransactionUpdate(finalizedTransaction, "paid");
        }
        
        return finalizedTransaction;
    }

    private async Task<Transaction> SendInternal(Transaction sendingTransaction, Transaction receivingTransaction, LightMoney amount, CancellationToken cancellationToken = default)
    {
        Transaction transaction = null;
        await using var dbContext = _dbContextFactory.CreateContext();
        var executionStrategy = dbContext.Database.CreateExecutionStrategy();
        
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

                transaction = sendingEntry.Entity;
            }
            catch (Exception)
            {
                await dbTransaction.RollbackAsync(cancellationToken);
                throw;
            }
        });

        return transaction;
    }

    private async Task<Transaction> SendExternal(Transaction sendingTransaction, LightMoney amount, LightMoney walletBalance, float maxFeePercent, CancellationToken cancellationToken = default)
    {
        // Account for fees
        var maxFeeAmount = LightMoney.Satoshis(amount.ToUnit(LightMoneyUnit.Satoshi) * (decimal)maxFeePercent / 100);
        var amountWithFee = amount + maxFeeAmount;
        if (walletBalance < amountWithFee)
        {
            throw new Exception(
                $"Insufficient balance: {Sats(walletBalance)} — tried to send {Sats(amount)} and need to keep a fee reserve of {Millisats(maxFeeAmount)}.");
        }

        Transaction transaction;
        await using var dbContext = _dbContextFactory.CreateContext();
        
        // Create preliminary transaction entry
        sendingTransaction.Amount = amountWithFee;
        sendingTransaction.AmountSettled = new LightMoney(amountWithFee.MilliSatoshi * -1);
        sendingTransaction.RoutingFee = maxFeeAmount;
        var sendingEntry = await dbContext.Transactions.AddAsync(sendingTransaction, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
        
        try
        {
            // Pay the invoice - cancel after 5 seconds, potentially caused by HODL invoices
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(TimeSpan.FromSeconds(5));
            var result = await _btcpayService.PayLightningInvoice(new LightningInvoicePayRequest
            {
                PaymentRequest = sendingTransaction.PaymentRequest, 
                MaxFeePercent = maxFeePercent
            }, cts.Token);
            
            // Check result
            if (result.TotalAmount == null)
            {
                throw new Exception("Payment request has already been paid.");
            }

            // Set amount to actual total amount paid, including fees
            var amountSettled = new LightMoney(result.TotalAmount * -1);
            sendingEntry.Entity.SetSettled(result.TotalAmount, amountSettled, result.FeeAmount, DateTimeOffset.UtcNow);

            // Update entry with payment data
            sendingEntry.State = EntityState.Modified;
            await dbContext.SaveChangesAsync(cts.Token);

            transaction = sendingEntry.Entity;
        }
        catch (Exception ex) when (ex is TaskCanceledException)
        {
            // HttpClient.Timeout, potentially caused by HODL invoices
            // Payment may be pending, do not remove the transaction
            // LightningInvoiceWatcher will handle settling/cancelling
            throw;
        }
        catch (Exception ex) when (ex is GreenfieldAPIException)
        {
            throw;
        }
        catch (Exception)
        {
            // Remove preliminary transaction only in case the payment could not be initiated.
            dbContext.Transactions.Remove(sendingEntry.Entity);
            await dbContext.SaveChangesAsync(cancellationToken);
            throw;
        }

        return transaction;
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
            { IsExpired: true } => throw new Exception($"Payment request already expired at {transaction.ExpiresAt}."),
            { IsSettled: true } => throw new Exception("Payment request has already been settled."),
            { IsPaid: true } => throw new Exception("Payment request has already been paid."),
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
                Key = Encoders.Hex.EncodeData(RandomUtils.GetBytes(20))
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
            var walletQuery = new WalletQuery
            {
                WalletId = query.WalletId,
                IncludeTransactions = true
            };

            if (query.UserId != null) walletQuery.UserId = query.UserId;

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

        if (!query.IncludingExpired)
        {
            var enumerable = queryable.AsEnumerable(); // Switch to client side filtering
            return enumerable.Where(t => t.ExpiresAt > DateTimeOffset.UtcNow).ToList();
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
        if (!transaction.SetCancelled()) return false;
        
        await UpdateTransaction(transaction);
        await BroadcastTransactionUpdate(transaction, Transaction.StatusCancelled);
        return true;
    }

    public async Task<bool> Settle(Transaction transaction, LightMoney amount, LightMoney amountSettled, LightMoney routingFee, DateTimeOffset date)
    {
        if (!transaction.SetSettled(amount, amountSettled, routingFee, date)) return false;
        
        await UpdateTransaction(transaction);
        await BroadcastTransactionUpdate(transaction, Transaction.StatusSettled);
        return true;
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
    public static string Millisats(LightMoney amount) => $"{amount.ToUnit(LightMoneyUnit.MilliSatoshi)} millisats";

}
