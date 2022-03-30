using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BTCPayServer.Plugins.PodServer.Data.Models;

public class Podcast
{
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    [DisplayName("Podcast ID")]
    public string PodcastId { get; set; }

    // Relations
    [DisplayName("User ID")]
    public string UserId { get; set; }
    
    public ICollection<Season> Seasons { get; set; }

    public ICollection<Episode> Episodes { get; set; } = new List<Episode>();
    
    public ICollection<Person> People { get; set; } = new List<Person>();
    public ICollection<Contribution> Contributions { get; set; } = new List<Contribution>();
    public ICollection<Import> Imports { get; set; } = new List<Import>();
    
    // Properties
    [Required]
    public string Title { get; set; }
    
    [Required]
    public string Description { get; set; }
    
    [Required]
    public string Language { get; set; }
    public string Category { get; set; }
    
    public string ImageFileId { get; set; }
    
    public string Owner { get; set; }
    
    public string Email { get; set; }
    
    [DisplayName("Website URL")]
    public string Url { get; set; }
}
