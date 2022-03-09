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
                _logger.LogDebug("Processing {Count} transactions", count);

                await Task.WhenAll(list.Select(transaction => CheckPendingTransaction(walletService, transaction, cancellationToken)));
            }

            await Task.Delay(5_000, cancellationToken);
        }
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
                // Receiving transaction
                var invoice = await _btcpayService.GetLightningInvoice(transaction.InvoiceId, cancellationToken);
                if (invoice.Status == LightningInvoiceStatus.Paid)
                {
                    var paidAt = invoice.PaidAt ?? DateTimeOffset.Now;
                    var result = await walletService.Settle(transaction, invoice.Amount, invoice.AmountReceived, invoice.Amount - invoice.AmountReceived, paidAt);

                    _logger.LogInformation(
                        // ReSharper disable once TemplateIsNotCompileTimeConstantProblem
                        result ? "Settled transaction {TransactionId}" : "Settling transaction {TransactionId} failed",
                        transaction.TransactionId);
                }
            }
            else
            {
                // TODO: Sending transaction
                /*
                var bolt11 = walletService.ParsePaymentRequest(transaction.PaymentRequest);
                var payment = await _btcpayService.TrackPayment(bolt11.Hash, cancellationToken);
                
                _logger.LogInformation("Checking pending transaction {TransactionId}", transaction.TransactionId);
                */
            }
        }
        catch (Exception exception)
        {
            _logger.LogError("Checking pending transaction {TransactionId} failed: {Message}", transaction.TransactionId, exception.Message);
        }
    }
}
