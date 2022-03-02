using System;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;
using BTCPayServer.Abstractions.Constants;
using BTCPayServer.Client;
using BTCPayServer.Data;
using BTCPayServer.Lightning;
using BTCPayServer.Plugins.LNbank.Data.Models;
using BTCPayServer.Plugins.LNbank.Services.Wallets;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace BTCPayServer.Plugins.LNbank.Pages.Wallets;

[Authorize(AuthenticationSchemes = AuthenticationSchemes.Cookie, Policy = Policies.CanViewProfile)]
public class ReceiveModel : BasePageModel
{
    public Wallet Wallet { get; set; }
    [BindProperty]
    public string Description { get; set; }
    [BindProperty] 
    [DisplayName("Attach description to payment request")]
    public bool AttachDescription { get; set; }
    [BindProperty]
    [DisplayName("Amount in sats")]
    [Required]
    [Range(1, 2100000000000)]
    public long Amount { get; set; }
    [BindProperty]
    [DisplayName("Add routing hints for private channels")]
    public bool PrivateRouteHints { get; set; }

    public ReceiveModel(
        UserManager<ApplicationUser> userManager, 
        WalletService walletService) : base(userManager, walletService) {}
        
    public async Task<IActionResult> OnGet(string walletId)
    {
        Wallet = await WalletService.GetWallet(new WalletQuery {
            UserId = UserId,
            WalletId = walletId,
            IncludeTransactions = true
        });

        if (Wallet == null) return NotFound();

        return Page();
    }

    public async Task<IActionResult> OnPostAsync(string walletId)
    {
        Wallet = await WalletService.GetWallet(new WalletQuery {
            UserId = UserId,
            WalletId = walletId,
            IncludeTransactions = true
        });

        if (Wallet == null) return NotFound();
        if (!ModelState.IsValid) return Page();

        try
        {
            var amount = LightMoney.Satoshis(Amount).MilliSatoshi;
            var transaction = await WalletService.Receive(Wallet, amount, Description, AttachDescription, PrivateRouteHints);
            return RedirectToPage("/Transaction/Details", new { walletId, transaction.TransactionId });
        }
        catch (Exception exception)
        {
            TempData[WellKnownTempData.ErrorMessage] = string.IsNullOrEmpty(exception.Message)
                ? "Invoice creation failed."
                : exception.Message;
        }

        return Page();
    }
}