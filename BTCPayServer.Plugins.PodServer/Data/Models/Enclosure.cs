using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BTCPayServer.Plugins.PodServer.Data.Models;

public class Enclosure
{
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    [DisplayName("Enclosure ID")]
    public string EnclosureId { get; set; }
    
    // Relations
    [Required]
    public string EpisodeId { get; set; }
    public Episode Episode { get; set; }
    
    // Properties
    [Required]
    public string Url { get; set; }
    
    [Required]
    public string Type { get; set; }
    
    public string Title { get; set; }
    
    public int Length { get; set; }
    
    public bool IsAlternate { get; set; }
}