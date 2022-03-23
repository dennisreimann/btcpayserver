using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BTCPayServer.Plugins.PodServer.Data.Models;

public class Person
{
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    [DisplayName("Person ID")]
    public string PersonId { get; set; }
    
    // Relations
    [Required]
    public string PodcastId { get; set; }
    public Podcast Podcast { get; set; }

    public ICollection<Contribution> Contributions { get; set; } = new List<Contribution>();
    
    // Properties
    [Required]
    public string Name { get; set; }
    
    public string Url { get; set; }
    
    public string ImageFileId { get; set; }
    
    public ValueRecipient ValueRecipient { get; set; }
}
