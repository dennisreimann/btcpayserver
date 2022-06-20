using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using BTCPayServer.Abstractions.Extensions;
using BTCPayServer.Client.Models;
using BTCPayServer.Lightning;
using BTCPayServer.Plugins.LNbank.Data.Models;
using BTCPayServer.Plugins.LNbank.Services;
using BTCPayServer.Plugins.LNbank.Services.Wallets;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace BTCPayServer.Plugins.LNbank.Controllers.API;

public class LightningController : BaseApiController
{
    private readonly BTCPayService _btcpayService;
    private readonly WalletService _walletService;

    public LightningController(
        BTCPayService btcpayService,
        WalletService walletService,
        IOptionsMonitor<IdentityOptions> identityOptions) : base(identityOptions)
    {
        _btcpayService = btcpayService;
        _walletService = walletService;
    }

    // --- Custom methods ---

    [HttpPost("invoice")]
    public async Task<IActionResult> CreateLightningInvoice(LightningInvoiceCreateRequest req)
    {
        if (Wallet == null) return this.CreateAPIError(404, "wallet-not-found", "The wallet was not found");

        try
        {
            var transaction = req.Description is null
                ? await _walletService.Receive(Wallet, req.Amount, req.DescriptionHash, req.PrivateRouteHints, req.Expiry)
                : await _walletService.Receive(Wallet, req.Amount, req.Description, true, req.PrivateRouteHints, req.Expiry);
              
            var data = ToLightningInvoiceData(transaction);
            return Ok(data);
        }
        catch (Exception exception)
        {
            return this.CreateAPIError("generic-error", exception.Message);
        }
    }

    [HttpPost("pay")]
    public async Task<IActionResult> Pay(LightningInvoicePayRequest req)
    {
        if (Wallet == null) return this.CreateAPIError(404, "wallet-not-found", "The wallet was not found");

        var paymentRequest = req.PaymentRequest;
        var bolt11 = _walletService.ParsePaymentRequest(paymentRequest);
        var isZeroAmount = bolt11.MinimumAmount == LightMoney.Zero;
        var amount = isZeroAmount ? req.Amount : null;
        if (isZeroAmount && amount == null)
        {
            ModelState.AddModelError(nameof(req.Amount), "Amount is required to pay a zero amount invoice");
            return this.CreateValidationError(ModelState);
        }

        try
        {
            // load wallet including transactions to do the balance check
            var wallet = await GetWalletWithTransactions(Wallet.WalletId);
            var transaction = await _walletService.Send(wallet, bolt11, paymentRequest, bolt11.ShortDescription, amount);
            var details = transaction.IsSettled
                ? new PayDetails { TotalAmount = transaction.Amount, FeeAmount = transaction.RoutingFee }
                : null;
            var response = new PayResponse(PayResult.Ok, details);
            return Ok(response);
        }
        catch (Exception exception)
        {
            return this.CreateAPIError("generic-error", exception.Message);
        }
    }

    [HttpGet("balance")]
    public async Task<IActionResult> GetLightningNodeBalance()
    {
        if (Wallet == null) return this.CreateAPIError(404, "wallet-not-found", "The wallet was not found");
        
        try
        {
            // load wallet including transactions to see the balance
            var wallet = await GetWalletWithTransactions(Wallet.WalletId);
            var offchain = new OffchainBalance { Local = wallet.Balance };
            var balance = new LightningNodeBalance(null, offchain);
            return Ok(balance);
        }
        catch (Exception exception)
        {
            return this.CreateAPIError("generic-error", exception.Message);
        }
    }

    // ---- General methods ---

    [HttpGet("info")]
    public async Task<ActionResult<LightningNodeInformationData>> GetLightningNodeInfo()
    {
        var info = await _btcpayService.GetLightningNodeInfo();
        return Ok(info);
    }

    [HttpGet("invoice/{invoiceId}")]
    public async Task<IActionResult> GetLightningInvoice(string invoiceId)
    {
        try
        {
            var transaction = await _walletService.GetTransaction(new TransactionQuery
            {
                UserId = UserId,
                WalletId = WalletId,
                InvoiceId = invoiceId
            });
            if (transaction == null) return this.CreateAPIError(404, "invoice-not-found", "The invoice was not found");
        
            var invoice = ToLightningInvoiceData(transaction);
            return Ok(invoice);
        }
        catch (Exception exception)
        {
            return this.CreateAPIError("generic-error", exception.Message);
        }
    }
    
    [HttpGet("payment/{paymentHash}")]
    public async Task<IActionResult> GetLightningPayment(string paymentHash)
    {
        try
        {
            var payment = await _btcpayService.GetLightningPayment(paymentHash);
            if (payment == null) return this.CreateAPIError(404, "payment-not-found", "The payment was not found");
            
            return Ok(payment);
        }
        catch (Exception exception)
        {
            return this.CreateAPIError("generic-error", exception.Message);
        }
    }
    
    [HttpDelete("invoice/{invoiceId}")]
    public async Task<ActionResult<LightningInvoiceData>> CancelLightningInvoice(string invoiceId)
    {
        await _walletService.Cancel(invoiceId);
        return Ok();
    }

    [HttpGet("channels")]
    public async Task<ActionResult<IEnumerable<LightningChannelData>>> ListLightningChannels()
    {
        var list = await _btcpayService.ListLightningChannels();
        return Ok(list);
    }

    [HttpPost("channels")]
    public async Task<ActionResult<string>> OpenLightningChannel(OpenLightningChannelRequest req)
    {
        await _btcpayService.OpenLightningChannel(req);
        return Ok();
    }

    [HttpPost("connect")]
    public async Task<ActionResult> ConnectToLightningNode(ConnectToNodeRequest req)
    {
        await _btcpayService.ConnectToLightningNode(req);
        return Ok();
    }

    [HttpPost("deposit-address")]
    public async Task<ActionResult<string>> GetLightningDepositAddress()
    {
        var address = await _btcpayService.GetLightningDepositAddress();
        return Ok(address);
    }

    private LightningInvoiceData ToLightningInvoiceData(Transaction transaction) => 
        new()
        {
            Amount = transaction.Amount,
            Id = transaction.InvoiceId,
            Status = transaction.LightningInvoiceStatus,
            AmountReceived = transaction.AmountSettled,
            PaidAt = transaction.PaidAt,
            BOLT11 = transaction.PaymentRequest,
            ExpiresAt = transaction.ExpiresAt
        };

    private async Task<Wallet> GetWalletWithTransactions(string walletId)
    {
        return await _walletService.GetWallet(new WalletQuery
        {
            WalletId = walletId,
            IncludeTransactions = true
        });
    }
}
