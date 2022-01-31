using System.Threading.Tasks;
using BTCPayServer.Abstractions.Constants;
using BTCPayServer.Client;
using BTCPayServer.Data;
using BTCPayServer.Plugins.LNbank.Data.Models;
using BTCPayServer.Plugins.LNbank.Services.Wallets;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore.Metadata.Internal;

namespace BTCPayServer.Plugins.LNbank.Pages.Wallets;

[Authorize(AuthenticationSchemes = AuthenticationSchemes.Cookie, Policy = Policies.CanViewProfile)]
public class CreateModel : BasePageModel
{
    public Wallet Wallet { get; set; }
    [FromQuery]
    public string ReturnUrl { get; set; }

    public CreateModel(
        UserManager<ApplicationUser> userManager, 
        WalletService walletService) : base(userManager, walletService) {}

    public IActionResult OnGet()
    {
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid) return Page();

        Wallet = new Wallet
        {
            UserId = UserId
        };

        if (!await TryUpdateModelAsync(Wallet, "wallet", w => w.Name)) return Page();
            
        await WalletService.AddOrUpdateWallet(Wallet);
        
        if (!string.IsNullOrEmpty(ReturnUrl) && Url.IsLocalUrl(ReturnUrl))
        {
            return Redirect(ReturnUrl + $"?LNbankWallet={Wallet.WalletId}");
        }
        
        return RedirectToPage("./Index", new { walletId = Wallet.WalletId });
    }
}
