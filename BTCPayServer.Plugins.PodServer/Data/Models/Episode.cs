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
    
    public string Description { get; set; }
    
    public string ImageUrl { get; set; }
    
    public int Number { get; set; }
    
    // Relations
    [Required]
    public string PodcastId { get; set; }
    public Podcast Podcast { get; set; }
    
    public string SeasonId { get; set; }
    public Season Season { get; set; }
    
    public ICollection<Contribution> Contributors { get; set; }
    public ICollection<Enclosure> Enclosures { get; set; }

}
