using System.Linq;
using System.Threading.Tasks;
using BTCPayServer.Abstractions.Constants;
using BTCPayServer.Client;
using BTCPayServer.Data;
using BTCPayServer.Plugins.LNbank.Data.Models;
using BTCPayServer.Plugins.LNbank.Services.Wallets;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace BTCPayServer.Plugins.LNbank.Pages.Transactions;

[Authorize(AuthenticationSchemes = AuthenticationSchemes.Cookie, Policy = Policies.CanViewProfile)]
public class EditModel : BasePageModel
{
    public Wallet Wallet { get; set; }
    public Transaction Transaction { get; set; }

    public EditModel(
        UserManager<ApplicationUser> userManager, 
        WalletService walletService) : base(userManager, walletService) {}

    public async Task<IActionResult> OnGetAsync(string walletId, string transactionId)
    {
        Wallet = await GetWallet(UserId, walletId);
        if (Wallet == null) return NotFound();
        
        Transaction = Wallet.Transactions.FirstOrDefault(t => t.TransactionId == transactionId);

        if (Transaction == null) return NotFound();

        return Page();
    }

    public async Task<IActionResult> OnPostAsync(string walletId, string transactionId)
    {
        Wallet = await GetWallet(UserId, walletId);
        if (Wallet == null) return NotFound();
        
        Transaction = await WalletService.GetTransaction(new TransactionQuery
        {
            UserId = UserId,
            WalletId = Wallet.WalletId,
            TransactionId = transactionId
        });

        if (!ModelState.IsValid) return Page();
        if (Transaction == null) return NotFound();

        if (await TryUpdateModelAsync(Transaction, "transaction", t => t.Description))
        {
            await WalletService.UpdateTransaction(Transaction);
            return RedirectToPage("/Wallets/Wallet", new { Wallet.WalletId });
        }

        return Page();
    }
}
