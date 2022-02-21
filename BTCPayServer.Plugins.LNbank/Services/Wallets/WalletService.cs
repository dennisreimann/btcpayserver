using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BTCPayServer.Lightning;
using BTCPayServer.Plugins.LNbank.Data;
using BTCPayServer.Plugins.LNbank.Data.Models;
using BTCPayServer.Plugins.LNbank.Hubs;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
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
        var walletsQuery = new WalletsQuery()
        {
            IncludeTransactions = query.IncludeTransactions,
            IncludeAccessKeys = query.IncludeAccessKeys,
            UserId = query.UserId is null? null : new []{ query.UserId},
            WalletId = query.WalletId is null? null : new []{ query.WalletId},
            AccessKey = query.AccessKey is null? null : new []{ query.AccessKey},
        };
        return await FilterWallets(dbContext.Wallets.AsQueryable(), walletsQuery).FirstOrDefaultAsync();
    }

    public async Task<Transaction> Receive(Wallet wallet, long amount, string description, bool attachDescription, bool privateRouteHints) =>
        await Receive(wallet, amount, description, null, attachDescription, privateRouteHints);
        
    public async Task<Transaction> Receive(Wallet wallet, long amount, uint256 descriptionHash, bool privateRouteHints) =>
        await Receive(wallet, amount, null, descriptionHash, false, privateRouteHints);

    private async Task<Transaction> Receive(Wallet wallet, long amount, string description, uint256 descriptionHash, bool attachDescription, bool privateRouteHints)
    {
        await using var dbContext = _dbContextFactory.CreateContext();
        if (amount <= 0) throw new ArgumentException(nameof(amount));

        var data = await _btcpayService.CreateLightningInvoice(new LightningInvoiceCreateRequest
        {
            Amount = amount,
            Description = attachDescription ? description : null,
            DescriptionHash = descriptionHash,
            PrivateRouteHints = privateRouteHints
        });

        var entry = await dbContext.Transactions.AddAsync(new Transaction
        {
            WalletId = wallet.WalletId,
            InvoiceId = data.Id,
            Amount = data.Amount,
            ExpiresAt = data.ExpiresAt,
            PaymentRequest = data.BOLT11,
            Description = description ?? descriptionHash?.ToString() ?? string.Empty
        });
            
        await dbContext.SaveChangesAsync();

        return entry.Entity;
    }

    public async Task<Transaction> Send(Wallet wallet, BOLT11PaymentRequest bolt11, string paymentRequest, string description, float maxFeePercent = 3)
    {
        if (bolt11.ExpiryDate <= DateTimeOffset.UtcNow)
        {
            throw new Exception($"Payment request already expired at {bolt11.ExpiryDate}.");
        }

        // check balance
        LightMoney routingFee = null;
        var amount = bolt11.MinimumAmount;
        if (wallet.Balance < amount)
        {
            throw new Exception($"Insufficient balance: {Sats(wallet.Balance)}, tried to send {Sats(amount)}.");
        }

        // check if the invoice exists already
        var transaction = await ValidatePaymentRequest(paymentRequest);
        var isInternal = !string.IsNullOrEmpty(transaction?.InvoiceId);
        if (!isInternal)
        {
            // Account for fees
            var maxFeeAmount = LightMoney.Satoshis(amount.ToUnit(LightMoneyUnit.Satoshi) * (decimal)maxFeePercent / 100);
            var amountWithFee = amount + maxFeeAmount;
            if (wallet.Balance < amountWithFee)
            {
                throw new Exception($"Insufficient balance: {Sats(wallet.Balance)}, tried to send {Sats(amount)} and need to keep a fee reserve of {Sats(maxFeeAmount)}.");
            }
            var result = await _btcpayService.PayLightningInvoice(new LightningInvoicePayRequest
            {
                PaymentRequest = paymentRequest,
                MaxFeePercent = maxFeePercent
            });
                
            // Set amount to actual total amount paid, including fees
            amount = result.TotalAmount;
            routingFee = result.FeeAmount;
        }
            
        await using var dbContext = _dbContextFactory.CreateContext();
        var executionStrategy = dbContext.Database.CreateExecutionStrategy();
        await executionStrategy.ExecuteAsync(async () =>
        {
            // https://docs.microsoft.com/en-us/ef/core/saving/transactions#controlling-transactions
            await using var dbTransaction = await dbContext.Database.BeginTransactionAsync();
            try
            {
                var now = DateTimeOffset.UtcNow;
                var entry = await dbContext.Transactions.AddAsync(new Transaction
                {
                    WalletId = wallet.WalletId,
                    PaymentRequest = paymentRequest,
                    Amount = amount,
                    AmountSettled = new LightMoney(amount.MilliSatoshi * -1),
                    RoutingFee = routingFee,
                    ExpiresAt = bolt11.ExpiryDate,
                    Description = description,
                    PaidAt = now
                });
                await dbContext.SaveChangesAsync();

                if (transaction != null)
                {
                    await MarkTransactionPaid(transaction, amount, now);
                }
                await dbTransaction.CommitAsync();

                return entry.Entity;
            }
            catch (Exception)
            {
                await dbTransaction.RollbackAsync();
            }

            return null;
        });

        return null;
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
            { IsPaid: true } => throw new Exception("Payment request has already been paid."),
            _ => transaction
        };
    }

    public async Task AddOrUpdateWallet(Wallet wallet)
    {
        await using var dbContext = _dbContextFactory.CreateContext();

        if (string.IsNullOrEmpty(wallet.WalletId))
        {
            wallet.AccessKeys ??= new List<AccessKey>();
            wallet.AccessKeys.Add(new AccessKey()
            {
                Key = Encoders.Hex.EncodeData(RandomUtils.GetBytes(20))
            });
            await dbContext.Wallets.AddAsync(wallet);
        }
        else
        {
            var entry = dbContext.Entry(wallet);
            entry.State = EntityState.Modified;
        }
        await dbContext.SaveChangesAsync();
    }

    public async Task RemoveWallet(Wallet wallet)
    {
        await using var dbContext = _dbContextFactory.CreateContext();
        dbContext.Wallets.Remove(wallet);
        await dbContext.SaveChangesAsync();
    }

    public async Task<IEnumerable<Transaction>> GetPendingTransactions()
    {
        return await GetTransactions(new TransactionsQuery
        {
            IncludingPending = true,
            IncludingExpired = false,
            IncludingPaid = false
        });
    }

    public async Task CheckPendingTransaction(Transaction transaction, CancellationToken cancellationToken = default)
    {
        var invoice = await _btcpayService.GetLightningInvoice(transaction.InvoiceId, cancellationToken);
        if (invoice.Status == LightningInvoiceStatus.Paid)
        {
            await MarkTransactionPaid(transaction, invoice.AmountReceived, invoice.PaidAt);
        }
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

    public async Task UpdateTransaction(Transaction transaction)
    {
        await using var dbContext = _dbContextFactory.CreateContext();
        var entry = dbContext.Entry(transaction);
        entry.State = EntityState.Modified;

        await dbContext.SaveChangesAsync();
    }

    public BOLT11PaymentRequest ParsePaymentRequest(string payReq)
    {
        return BOLT11PaymentRequest.Parse(payReq, _network);
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

        if (!query.IncludingExpired)
        {
            var enumerable = queryable.AsEnumerable(); // Switch to client side filtering
            return enumerable.Where(t => t.ExpiresAt > DateTimeOffset.UtcNow).ToList();
        }

        return await queryable.ToListAsync();
    }

    private async Task MarkTransactionPaid(Transaction transaction, LightMoney amountSettled, DateTimeOffset? date)
    {
        _logger.LogInformation($"Marking transaction {transaction.TransactionId} as paid");

        transaction.AmountSettled = amountSettled;
        transaction.PaidAt = date;

        await UpdateTransaction(transaction);
        await _transactionHub.Clients.All.SendAsync("transaction-update", new
        {
            transaction.TransactionId,
            transaction.InvoiceId,
            transaction.WalletId,
            transaction.Status,
            transaction.IsPaid,
            transaction.IsExpired,
            Event = "paid"
        });
    }

    public async Task Cancel(string invoiceId)
    {
        var transaction = await GetTransaction(new TransactionQuery { InvoiceId = invoiceId });
        if (transaction.SetCancelled())
        {
            await UpdateTransaction(transaction);
            await _transactionHub.Clients.All.SendAsync("transaction-update", new
            {
                transaction.TransactionId,
                transaction.InvoiceId,
                transaction.WalletId,
                transaction.Status,
                transaction.IsPaid,
                transaction.IsExpired,
                Event = "cancelled"
            });
        }
    }
        
    private static string Sats(LightMoney amount) => $"{Math.Round(amount.ToUnit(LightMoneyUnit.Satoshi))} sats";
}
