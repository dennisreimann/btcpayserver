using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BTCPayServer.Lightning;
using BTCPayServer.Plugins.LNbank.Data.Models;
using BTCPayServer.Plugins.LNbank.Services.Wallets;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace BTCPayServer.Plugins.LNbank.Services;

public class LightningInvoiceWatcher : BackgroundService
{
    private readonly IServiceScopeFactory _serviceScopeFactory;
    private readonly ILogger<LightningInvoiceWatcher> _logger;
    private readonly BTCPayService _btcpayService;

    private static readonly TimeSpan _checkInterval = TimeSpan.FromSeconds(5);
    
    // grace period before starting to check a pending transaction, which is inflight
    // and might get handled in the request context that initiated the payment 
    private static readonly TimeSpan _inflightDelay = WalletService.SendTimeout + _checkInterval;

    public LightningInvoiceWatcher(
        BTCPayService btcpayService,
        IServiceScopeFactory serviceScopeFactory,
        ILogger<LightningInvoiceWatcher> logger)
    {
        _logger = logger;
        _btcpayService = btcpayService;
        _serviceScopeFactory = serviceScopeFactory;
    }

    protected override async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Starting");

        while (!cancellationToken.IsCancellationRequested)
        {
            using var scope = _serviceScopeFactory.CreateScope();
            var walletService = scope.ServiceProvider.GetRequiredService<WalletService>();

            var transactions = await walletService.GetPendingTransactions();
            var list = transactions.ToList();
            int count = list.Count;

            if (count > 0)
            {
                _logger.LogDebug("Processing {Count} transactions", count);

                try
                {
                    await Task.WhenAll(list.Select(transaction => CheckPendingTransaction(walletService, transaction, cancellationToken)));
                }
                catch (Exception exception)
                {
                    _logger.LogError(exception, "Checking pending transactions failed: {Message}", exception.Message);
                }
            }

            await Task.Delay(_checkInterval, cancellationToken);
        }
        
        _logger.LogInformation("Ending, cancellation requested");
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Stopping");

        await Task.CompletedTask;
    }
    
    private async Task CheckPendingTransaction(WalletService walletService, Transaction transaction, CancellationToken cancellationToken = default)
    {
        try
        {
            if (!string.IsNullOrEmpty(transaction.InvoiceId))
            {
                // Receiving transaction - cancelled invoices return null, hence we need to null-check it
                var invoice = await _btcpayService.GetLightningInvoice(transaction.InvoiceId, cancellationToken);
                if (invoice == null)
                {
                    _logger.LogWarning("Unable to resolve invoice (Invoice Id = {InvoiceId}) for transaction {TransactionId}", transaction.InvoiceId, transaction.TransactionId);
                }
                else if (invoice.Status == LightningInvoiceStatus.Paid)
                {
                    var paidAt = invoice.PaidAt ?? DateTimeOffset.Now;
                    var amount = invoice.Amount ?? invoice.AmountReceived; // Zero amount invoices have amount as null value
                    var feeAmount = amount - invoice.AmountReceived;
                    await walletService.Settle(transaction, amount, invoice.AmountReceived, feeAmount, paidAt);
                }
            }
            else
            {
                // Sending transaction - cancelled payments return null, hence we need to null-check it
                var bolt11 = walletService.ParsePaymentRequest(transaction.PaymentRequest);
                var paymentHash = bolt11.PaymentHash?.ToString();
                var payment = await _btcpayService.GetLightningPayment(paymentHash, cancellationToken);
                
                if (payment == null)
                {
                    var isInflight = transaction.IsPending && transaction.CreatedAt > DateTimeOffset.Now - _inflightDelay;
                    if (!isInflight)
                    {
                        _logger.LogWarning("Unable to resolve payment (Payment Hash = {PaymentHash}) for transaction {TransactionId}", paymentHash, transaction.TransactionId);
                    }
                }
                else switch (payment.Status)
                {
                    case LightningPaymentStatus.Complete:
                    {
                        var paidAt = payment.CreatedAt ?? DateTimeOffset.Now;
                        var originalAmount = payment.TotalAmount - payment.FeeAmount;
                        await walletService.Settle(transaction, originalAmount, payment.TotalAmount * -1, payment.FeeAmount, paidAt);
                        break;
                    }
                    case LightningPaymentStatus.Failed:
                        _logger.LogWarning("Failed payment (Payment Hash = {PaymentHash}) for transaction {TransactionId} - invalidating transaction", paymentHash, transaction.TransactionId);
                        await walletService.Invalidate(transaction);
                        break;
                    case LightningPaymentStatus.Unknown:
                    case LightningPaymentStatus.Pending:
                    default:
                        _logger.LogDebug("Transaction {TransactionId} status: {Status}", transaction.TransactionId, payment.Status.ToString());
                        break;
                }
            }
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Checking pending transaction {TransactionId} failed: {Message}", transaction.TransactionId, exception.Message);
        }
    }
}
