using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace BTCPayServer.Plugins.PodServer.Data.Models;

public class Contribution
{
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    [DisplayName("Contribution ID")]
    public string ContributionId { get; set; }
    
    // Properties
    public string Role { get; set; }
    
    [Required]
    [Range(1, int.MaxValue)]
    public int Split { get; set; }
    
    // Relations
    [Required]
    public string PodcastId { get; set; }
    public Podcast Podcast { get; set; }
    
    [Required]
    public string PersonId { get; set; }
    public Person Person { get; set; }
    
    public string EpisodeId { get; set; }
    public Episode Episode { get; set; }
    
    internal static void OnModelCreating(ModelBuilder builder)
    {
        builder
            .Entity<Contribution>()
            .HasIndex("PodcastId", "EpisodeId", "PersonId")
            .IsUnique();
    
        builder
            .Entity<Contribution>()
            .HasOne(e => e.Podcast)
            .WithMany(p => p.Contributions)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
