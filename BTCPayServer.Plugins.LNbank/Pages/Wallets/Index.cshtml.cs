using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BTCPayServer.Data;
using BTCPayServer.Plugins.LNbank.Data.Models;
using BTCPayServer.Plugins.LNbank.Services.Wallets;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore.Internal;

namespace BTCPayServer.Plugins.LNbank.Pages.Wallets
{
    public class IndexModel : BasePageModel
    {
        public IEnumerable<Wallet> Wallets { get; set; }
        public Wallet SelectedWallet { get; set; }
        public IEnumerable<Transaction> Transactions { get; set; }

        public IndexModel(
            UserManager<ApplicationUser> userManager, 
            WalletService walletService) : base(userManager, walletService) {}

        public async Task OnGetAsync(string walletId)
        {
            Wallets = await WalletService.GetWallets(new WalletsQuery {
                UserId = UserId,
                IncludeTransactions = true
            });

            var list = Wallets.ToList();
            if (!list.Any())
            {
                RedirectToRoute("./Create");
            }
            else if (walletId != null)
            {
                SelectedWallet = list.FirstOrDefault(w => w.WalletId == walletId) ?? list.First();
                Transactions = SelectedWallet?.Transactions.OrderByDescending(t => t.CreatedAt);
            }
        }
    }
}