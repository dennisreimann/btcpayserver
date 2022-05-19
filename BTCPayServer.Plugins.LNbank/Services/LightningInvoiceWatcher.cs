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
                _logger.LogInformation("Processing {Count} transactions", count);

                try
                {
                    await Task.WhenAll(list.Select(transaction => CheckPendingTransaction(walletService, transaction, cancellationToken)));
                }
                catch (Exception exception)
                {
                    _logger.LogError(exception, "Checking pending transactions failed: {Message}", exception.Message);
                }
            }

            await Task.Delay(TimeSpan.FromSeconds(5), cancellationToken);
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
                    _logger.LogInformation("Unable to resolve invoice (Invoice Id = {InvoiceId}) for transaction {TransactionId} - invalidating transaction", transaction.InvoiceId, transaction.TransactionId);
                    
                    var result = await walletService.Invalidate(transaction);

                    _logger.LogInformation(
                        // ReSharper disable once TemplateIsNotCompileTimeConstantProblem
                        result ? "Invalidated transaction {TransactionId}" : "Invalidating transaction {TransactionId} failed",
                        transaction.TransactionId);
                }
                else if (invoice.Status == LightningInvoiceStatus.Paid)
                {
                    var paidAt = invoice.PaidAt ?? DateTimeOffset.Now;
                    var feeAmount = invoice.Amount - invoice.AmountReceived;
                    var result = await walletService.Settle(transaction, invoice.Amount, invoice.AmountReceived, feeAmount, paidAt);

                    _logger.LogInformation(
                        // ReSharper disable once TemplateIsNotCompileTimeConstantProblem
                        result ? "Settled transaction {TransactionId}" : "Settling transaction {TransactionId} failed",
                        transaction.TransactionId);
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
                    _logger.LogInformation("Unable to resolve payment (Payment Hash = {PaymentHash}) for transaction {TransactionId} - invalidating transaction", paymentHash, transaction.TransactionId);
                    
                    var result = await walletService.Invalidate(transaction);

                    _logger.LogInformation(
                        // ReSharper disable once TemplateIsNotCompileTimeConstantProblem
                        result ? "Invalidated transaction {TransactionId}" : "Invalidating transaction {TransactionId} failed",
                        transaction.TransactionId);
                }
                else if (payment.Status == LightningPaymentStatus.Complete)
                {
                    var paidAt = payment.CreatedAt ?? DateTimeOffset.Now;
                    var originalAmount = payment.TotalAmount - payment.FeeAmount;
                    var result = await walletService.Settle(transaction, originalAmount, payment.TotalAmount * -1, payment.FeeAmount, paidAt);

                    _logger.LogInformation(
                        // ReSharper disable once TemplateIsNotCompileTimeConstantProblem
                        result ? "Settled transaction {TransactionId}" : "Settling transaction {TransactionId} failed",
                        transaction.TransactionId);
                }
                else if (payment.Status == LightningPaymentStatus.Failed)
                {
                    var result = await walletService.Cancel(transaction);

                    _logger.LogInformation(
                        // ReSharper disable once TemplateIsNotCompileTimeConstantProblem
                        result ? "Cancelled transaction {TransactionId}" : "Cancelling transaction {TransactionId} failed",
                        transaction.TransactionId);
                }
                else
                {
                    _logger.LogInformation("Transaction {TransactionId} status: {Status}", transaction.TransactionId, payment.Status.ToString());
                }
            }
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Checking pending transaction {TransactionId} failed: {Message}", transaction.TransactionId, exception.Message);
        }
    }
}
