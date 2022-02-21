using System.ComponentModel.DataAnnotations;

namespace BTCPayServer.Plugins.LNbank.Data.Models;

public class AccessKey
{
    [Key]
    public string Key { get; set; }
    public string WalletId { get; set; }
    public Wallet Wallet { get; set; }
}
