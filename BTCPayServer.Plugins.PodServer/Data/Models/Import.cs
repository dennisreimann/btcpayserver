using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BTCPayServer.Plugins.PodServer.Data.Models;

public class Import
{
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    [DisplayName("Import ID")]
    public string ImportId { get; set; }
    
    // Properties
    [Required]
    public string Raw { get; set; }
    
    public string Log { get; set; }

    [Required]
    public ImportStatus Status { get; set; } = ImportStatus.Created;
    
    [DisplayName("Import date")]
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    
    // Relations
    [Required]
    public string PodcastId { get; set; }
}

public enum ImportStatus
{
    Created,
    Running,
    Cancelled,
    Succeeded,
    Failed
}
