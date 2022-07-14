using System;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;
using BTCPayServer.Abstractions.Constants;
using BTCPayServer.Client;
using BTCPayServer.Data;
using BTCPayServer.Lightning;
using BTCPayServer.Plugins.LNbank.Data.Models;
using BTCPayServer.Plugins.LNbank.Exceptions;
using BTCPayServer.Plugins.LNbank.Services.Wallets;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace BTCPayServer.Plugins.LNbank.Pages.Wallets;

[Authorize(AuthenticationSchemes = AuthenticationSchemes.Cookie, Policy = Policies.CanViewProfile)]
public class SendModel : BasePageModel
{
    private readonly ILogger _logger;
    
    public Wallet Wallet { get; set; }
    
    public BOLT11PaymentRequest Bolt11 { get; set; }
    
    [BindProperty]
    [DisplayName("Payment Request")]
    [Required]
    public string PaymentRequest { get; set; }
    
    [BindProperty]
    [DisplayName("Amount in sats")]
    [Range(1, 2100000000000)]
    public long ExplicitAmount { get; set; }
    
    [BindProperty]
    public string Description { get; set; }

    public bool ValidationFailed { get; set; }

    public SendModel(
        ILogger<SendModel> logger,
        UserManager<ApplicationUser> userManager,
        WalletRepository walletRepository,
        WalletService walletService) : base(userManager, walletRepository, walletService)
    {
        _logger = logger;
    }

    public async Task<IActionResult> OnGet(string walletId)
    {
        Wallet = await GetWallet(UserId, walletId);
        if (Wallet == null) return NotFound();

        return Page();
    }

    public async Task<IActionResult> OnPostDecodeAsync(string walletId)
    {
        Wallet = await GetWallet(UserId, walletId);
        if (Wallet == null) return NotFound();
        if (!ModelState.IsValid) return Page();

        try
        {
            PaymentRequest = PaymentRequest.Trim();
            Bolt11 = WalletService.ParsePaymentRequest(PaymentRequest);
            Description = Bolt11.ShortDescription;
            await WalletService.ValidatePaymentRequest(PaymentRequest);
        }
        catch (Exception exception)
        {
            ValidationFailed = exception is PaymentRequestValidationException;
            TempData[WellKnownTempData.ErrorMessage] = exception.Message;
        }

        return Page();
    }

    public async Task<IActionResult> OnPostConfirmAsync(string walletId)
    {
        Wallet = await GetWallet(UserId, walletId);
        if (Wallet == null) return NotFound();
        if (!ModelState.IsValid) return Page();

        PaymentRequest = PaymentRequest.Trim();
        Bolt11 = WalletService.ParsePaymentRequest(PaymentRequest);

        try
        {
            var explicitAmount = Bolt11.MinimumAmount == LightMoney.Zero ? LightMoney.Satoshis(ExplicitAmount) : null;
            var transaction = await WalletService.Send(Wallet, Bolt11, PaymentRequest, Description, explicitAmount);
            TempData[WellKnownTempData.SuccessMessage] = transaction.IsPending
                ? "Payment successfully sent, awaiting settlement."
                : "Payment successfully sent and settled.";
            return RedirectToPage("./Wallet", new { walletId });
        }
        catch (Exception exception)
        {
            const string message = "Payment failed";
            _logger.LogError(exception, message);

            ValidationFailed = exception is PaymentRequestValidationException;
            TempData[WellKnownTempData.ErrorMessage] = string.IsNullOrEmpty(exception.Message)
                ? message
                : exception.Message;
        }

        return Page();
    }
}
