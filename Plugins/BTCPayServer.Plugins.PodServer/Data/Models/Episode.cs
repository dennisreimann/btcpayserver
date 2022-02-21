using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BTCPayServer.Plugins.PodServer.Data.Models;

public class Episode
{
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    [DisplayName("Episode ID")]
    public string EpisodeId { get; set; }
    
    [Required]
    public string Title { get; set; }
    
    public string PodcastId { get; set; }
    
    public Podcast Podcast { get; set; }
}
