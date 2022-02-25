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

    public ICollection<Episode> Episodes { get; set; }
    
    public ICollection<Person> People { get; set; }
    
    // Properties
    [Required]
    public string Title { get; set; }
    
    [Required]
    public string Description { get; set; }
    
    [Required]
    public string Language { get; set; }
    
    [Required]
    public string MainImage { get; set; }
    
    public string Owner { get; set; }
    
    public string Email { get; set; }
    
    public string Url { get; set; }
}
