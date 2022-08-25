using Microsoft.EntityFrameworkCore;

namespace BTCPayServer.Plugins.PodServer.Data.Models;

public enum EditorRole
{
    Admin,
    Editor
}

public class Editor
{
    public Editor(string userId, string podcastId, EditorRole role)
    {
        UserId = userId;
        PodcastId = podcastId;
        Role = role;
    }

    // Relations
    public string UserId { get; set; }

    public string PodcastId { get; set; }
    public Podcast Podcast { get; set; }
    
    // Properties
    public EditorRole Role { get; set; }
    
    internal static void OnModelCreating(ModelBuilder builder)
    {
        builder
            .Entity<Editor>()
            .HasKey(t => new { t.UserId, t.PodcastId });
        
        builder
            .Entity<Editor>()
            .HasOne(e => e.Podcast)
            .WithMany(p => p.Editors)
            .OnDelete(DeleteBehavior.Cascade);
        
        builder.Entity<Editor>()
            .Property(e => e.Role)
            .HasConversion<string>();
    }
}
