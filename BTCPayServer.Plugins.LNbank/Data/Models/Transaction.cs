using System;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using BTCPayServer.Lightning;

namespace BTCPayServer.Plugins.LNbank.Data.Models;

public class Transaction
{
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public string TransactionId { get; set; }
    public string InvoiceId { get; set; }
    public string WalletId { get; set; }

    [Required]
    public LightMoney Amount { get; set; }
    [DisplayName("Settled amount")]
    public LightMoney AmountSettled { get; set; }
    public LightMoney RoutingFee { get; set; }
    public string Description { get; set; }
    [DisplayName("Payment Request")]
    [Required]
    public string PaymentRequest { get; set; }
    [DisplayName("Creation date")]
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    [DisplayName("Expiry")]
    public DateTimeOffset ExpiresAt { get; set; }
    [DisplayName("Payment date")]
    public DateTimeOffset? PaidAt { get; set; }
    public Wallet Wallet { get; set; }
    public string ExplicitStatus { get; set; }

    private const string StatusSettled = "settled";
    private const string StatusPaid = "paid";
    private const string StatusUnpaid = "unpaid";
    private const string StatusExpired = "expired";
    private const string StatusCancelled = "cancelled";
    
    public string Status
    {
        get
        {
            if (!string.IsNullOrEmpty(ExplicitStatus))
            {
                return ExplicitStatus;
            }
            if (AmountSettled != null)
            {
                return PaidAt == null ? StatusPaid : StatusSettled;
            }
            if (ExpiresAt <= DateTimeOffset.UtcNow)
            {
                return StatusExpired;
            }
            return StatusUnpaid;
        }
    }

    public LightningInvoiceStatus LightningInvoiceStatus
    {
        get => Status switch
        {
            StatusSettled => LightningInvoiceStatus.Paid,
            StatusPaid => LightningInvoiceStatus.Paid,
            StatusUnpaid => LightningInvoiceStatus.Unpaid,
            StatusExpired => LightningInvoiceStatus.Expired,
            _ => throw new NotSupportedException($"'{Status}' cannot be mapped to any LightningInvoiceStatus")
        };
    }

    public bool IsSettled => Status == StatusSettled;
    public bool IsPaid => Status == StatusPaid;
    public bool IsUnpaid => Status != StatusPaid;
    public bool IsExpired => Status == StatusExpired;
    public bool IsCancelled  => Status == StatusCancelled;
    public bool IsOverpaid => (IsPaid || IsSettled) && AmountSettled > Amount;
    public bool IsPaidPartially => (IsPaid || IsSettled) && AmountSettled < Amount;

    public DateTimeOffset Date => PaidAt ?? CreatedAt;

    public bool SetCancelled()
    {
        if (IsUnpaid || IsExpired) return false;
        ExplicitStatus = StatusCancelled;
        return true;
    }
    
    public bool SetSettled(LightMoney amount, LightMoney amountSettled, LightMoney routingFee, DateTimeOffset date)
    {
        if (IsSettled) return false;
        Amount = amount;
        AmountSettled = amountSettled;
        RoutingFee = routingFee;
        PaidAt = date;
        return true;
    }

    public bool HasRoutingFee => RoutingFee != null && RoutingFee > 0;
}
