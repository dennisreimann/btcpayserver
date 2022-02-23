using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using BTCPayServer.Lightning;

namespace BTCPayServer.Plugins.LNbank.Data.Models;

public class Wallet
{
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    [DisplayName("Wallet ID")]
    public string WalletId { get; set; }

    [DisplayName("User ID")] public string UserId { get; set; }
    [Required]
    public string Name { get; set; }
    [DisplayName("Creation date")]
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public ICollection<Transaction> Transactions { get; set; } = new List<Transaction>();

    public LightMoney Balance
    {
        get => Transactions
                .Where(t => t.AmountSettled != null)
                .Aggregate(new LightMoney(0), (total, t) => total + t.AmountSettled);
    }

    public ICollection<AccessKey> AccessKeys { get; set; } = new List<AccessKey>();

    public bool IsSoftDeleted { get; set; }
}
