using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BTCPayServer.Data;
using BTCPayServer.Abstractions.Constants;
using BTCPayServer.Client;
using BTCPayServer.Plugins.LNbank.Data.Models;
using BTCPayServer.Plugins.LNbank.Services.Wallets;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace BTCPayServer.Plugins.LNbank.Pages.Wallets;

[Authorize(AuthenticationSchemes = AuthenticationSchemes.Cookie, Policy = Policies.CanViewProfile)]
public class IndexModel : BasePageModel
{
    public IEnumerable<Wallet> Wallets { get; set; }
    public Wallet SelectedWallet { get; set; }
    public IEnumerable<Transaction> Transactions { get; set; }

    public IndexModel(
        UserManager<ApplicationUser> userManager, 
        WalletService walletService) : base(userManager, walletService) {}

    public async Task<IActionResult> OnGetAsync(string walletId)
    {
        Wallets = await WalletService.GetWallets(new WalletsQuery
        {
            UserId = new[] { UserId }, IncludeTransactions = true
        });

        var list = Wallets.ToList();
        if (!list.Any())
        {
            return RedirectToRoute("./Create");
        }
        
        if (walletId == null && list.Count == 1)
        {
            return RedirectToPage("./Index", new { list.First().WalletId });
        }
        
        if (walletId != null)
        {
            SelectedWallet = list.FirstOrDefault(w => w.WalletId == walletId);
            if (SelectedWallet == null)
            {
                return NotFound();
            }
            Transactions = SelectedWallet.Transactions.OrderByDescending(t => t.CreatedAt);
        }
        
        return Page();
    }
}
