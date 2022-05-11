using System.Threading.Tasks;
using BTCPayServer.Data;
using BTCPayServer.Plugins.LNbank.Data.Models;
using BTCPayServer.Plugins.LNbank.Services.Wallets;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace BTCPayServer.Plugins.LNbank.Pages.Wallets;

[AllowAnonymous]
public class ShareLNURLModel : BasePageModel
{
    public Wallet Wallet { get; set; }
    
    public ShareLNURLModel(
        UserManager<ApplicationUser> userManager,
        WalletService walletService) : base(userManager, walletService) {}
        
    public async Task<IActionResult> OnGet(string walletId)
    {
        Wallet = await WalletService.GetWallet(new WalletQuery {
            WalletId = walletId
        });

        if (Wallet == null) return NotFound();

        return Page();
    }
}
