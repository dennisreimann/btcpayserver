using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BTCPayServer.Plugins.PodServer.Data.Models;

public class Season
{
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    [DisplayName("Season ID")]
    public string SeasonId { get; set; }
    
    // Relations
    [Required]
    public string PodcastId { get; set; }
    public Podcast Podcast { get; set; }
    
    // Properties
    [Required]
    public int Number { get; set; }

    [MaxLength(128)]
    public string Name { get; set; }
}
