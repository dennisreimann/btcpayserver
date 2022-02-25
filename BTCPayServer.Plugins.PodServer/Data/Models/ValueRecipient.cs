using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace BTCPayServer.Plugins.PodServer.Data.Models;

[Owned]
public class ValueRecipient
{
    // Properties
    [Required]
    public string Type { get; set; }
    
    [Required]
    public string Address { get; set; }
    
    public string CustomKey { get; set; }
    
    public string CustomValue { get; set; }
}
