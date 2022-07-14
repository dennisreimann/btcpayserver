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
using Microsoft.Extensions.Logging;

namespace BTCPayServer.Plugins.LNbank.Pages.Wallets;

[Authorize(AuthenticationSchemes = AuthenticationSchemes.Cookie, Policy = Policies.CanViewProfile)]
public class ReceiveModel : BasePageModel
{
    private readonly ILogger _logger;
    
    public Wallet Wallet { get; set; }
    
    [BindProperty]
    public string Description { get; set; }
    
    [BindProperty] 
    [DisplayName("Attach description to payment request")]
    public bool AttachDescription { get; set; }
    
    [BindProperty]
    [DisplayName("Amount in sats")]
    [Required]
    [Range(0, 2100000000000)]
    public long Amount { get; set; }
    
    [BindProperty]
    [DisplayName("Add routing hints for private channels")]
    public bool PrivateRouteHints { get; set; }

    public ReceiveModel(
        ILogger<ReceiveModel> logger,
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

    public async Task<IActionResult> OnPostAsync(string walletId)
    {
        Wallet = await GetWallet(UserId, walletId);
        if (Wallet == null) return NotFound();
        if (!ModelState.IsValid) return Page();

        try
        {
            var amount = LightMoney.Satoshis(Amount).MilliSatoshi;
            var transaction = await WalletService.Receive(Wallet, amount, Description, AttachDescription, PrivateRouteHints, null);
            return RedirectToPage("/Transactions/Details", new { walletId, transaction.TransactionId });
        }
        catch (Exception exception)
        {
            const string message = "Invoice creation failed";
            _logger.LogError(exception, message);

            TempData[WellKnownTempData.ErrorMessage] = string.IsNullOrEmpty(exception.Message)
                ? message
                : exception.Message;
        }

        return Page();
    }
}
