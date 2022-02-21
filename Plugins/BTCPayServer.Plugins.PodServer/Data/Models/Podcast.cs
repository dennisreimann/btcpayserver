using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BTCPayServer.Plugins.PodServer.Data.Models;

public class Podcast
{
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    [DisplayName("Podcast ID")]
    public string PodcastId { get; set; }

    [DisplayName("User ID")] public string UserId { get; set; }
    [Required]
    public string Name { get; set; }

    public List<Episode> Episodes { get; set; } = new List<Episode>();
}
