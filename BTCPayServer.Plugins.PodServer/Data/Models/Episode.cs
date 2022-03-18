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
    
    [Required]
    public string Description { get; set; }
    
    [DisplayName("Publish date")]
    public DateTimeOffset? PublishedAt { get; set; }
    
    [DisplayName("Last update")]
    public DateTimeOffset LastUpdatedAt { get; set; }
    
    public string ImageFileId { get; set; }
    
    [Range(1, int.MaxValue)]
    public int? Number { get; set; }
    
    // Relations
    [Required]
    public string PodcastId { get; set; }
    public Podcast Podcast { get; set; }
    
    public string SeasonId { get; set; }
    public Season Season { get; set; }
    
    public ICollection<Contribution> Contributors { get; set; } = new List<Contribution>();
    public ICollection<Enclosure> Enclosures { get; set; } = new List<Enclosure>();
    
    public bool IsPublished
    {
        get => PublishedAt >= DateTime.UtcNow;
    }
}
