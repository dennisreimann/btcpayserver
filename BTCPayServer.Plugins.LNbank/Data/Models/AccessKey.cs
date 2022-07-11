using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;

namespace BTCPayServer.Plugins.LNbank.Data.Models;

public enum AccessLevel
{
    ReadOnly,
    Invoice,
    Send,
    Admin
}

public class AccessKey
{
    [Key]
    public string Key { get; set; }
    
    // Relations
    [DisplayName("Wallet ID")]
    public string WalletId { get; set; }
    public Wallet Wallet { get; set; }

    [DisplayName("User ID")]
    public string UserId { get; set; }
    
    // Properties
    public AccessLevel Level { get; set; }
    
    internal static void OnModelCreating(ModelBuilder builder)
    {
        builder
            .Entity<AccessKey>()
            .HasIndex(o => o.WalletId);
        
        builder
            .Entity<AccessKey>()
            .HasOne(o => o.Wallet)
            .WithMany(w => w.AccessKeys)
            .OnDelete(DeleteBehavior.Cascade);
        
        builder
            .Entity<AccessKey>()
            .HasIndex(o => o.UserId);
        
        builder.Entity<AccessKey>()
            .Property(e => e.Level)
            .HasConversion<string>()
            .HasDefaultValue(AccessLevel.Admin);
    }
}
