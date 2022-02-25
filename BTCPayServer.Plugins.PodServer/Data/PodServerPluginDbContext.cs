using BTCPayServer.Plugins.PodServer.Data.Models;
using Microsoft.EntityFrameworkCore;

namespace BTCPayServer.Plugins.PodServer.Data;

public class PodServerPluginDbContext : DbContext
{
    private readonly bool _designTime;

    public PodServerPluginDbContext(DbContextOptions<PodServerPluginDbContext> options, bool designTime = false)
        : base(options)
    {
        _designTime = designTime;
    }
        
    public DbSet<Podcast> Podcasts { get; set; }
    public DbSet<Episode> Episodes { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.HasDefaultSchema("BTCPayServer.Plugins.PodServer");
        if (Database.IsSqlite() && !_designTime)
        {
            // SQLite does not have proper support for DateTimeOffset via Entity Framework Core, see the limitations
            // here: https://docs.microsoft.com/en-us/ef/core/providers/sqlite/limitations#query-limitations
            // To work around this, when the Sqlite database provider is used, all model properties of type DateTimeOffset
            // use the DateTimeOffsetToBinaryConverter
            // Based on: https://github.com/aspnet/EntityFrameworkCore/issues/10784#issuecomment-415769754
            // This only supports millisecond precision, but should be sufficient for most use cases.
            foreach (var entityType in modelBuilder.Model.GetEntityTypes())
            {
                var properties = entityType.ClrType.GetProperties()
                    .Where(p => p.PropertyType == typeof(DateTimeOffset));
                foreach (var property in properties)
                {
                    modelBuilder
                        .Entity(entityType.Name)
                        .Property(property.Name)
                        .HasConversion(
                            new Microsoft.EntityFrameworkCore.Storage.ValueConversion.
                                DateTimeOffsetToBinaryConverter());
                }
            }
        }
            
        modelBuilder.Entity<Podcast>().HasIndex(o => o.UserId);
        modelBuilder.Entity<Season>().HasIndex(o => o.PodcastId);
        modelBuilder.Entity<Episode>().HasIndex(o => o.PodcastId);
        
        modelBuilder
            .Entity<Contribution>()
            .HasOne(c => c.Person)
            .WithMany(p => p.Contributions)
            .OnDelete(DeleteBehavior.Cascade);
        
        modelBuilder
            .Entity<Enclosure>()
            .HasOne(e => e.Episode)
            .WithMany(p => p.Enclosures)
            .OnDelete(DeleteBehavior.Cascade);
            
        modelBuilder
            .Entity<Episode>()
            .HasOne(o => o.Podcast)
            .WithMany(w => w.Episodes)
            .OnDelete(DeleteBehavior.Cascade);
            
        modelBuilder
            .Entity<Person>()
            .HasOne(o => o.Podcast)
            .WithMany(w => w.People)
            .OnDelete(DeleteBehavior.Cascade);
            
        modelBuilder
            .Entity<Season>()
            .HasOne(o => o.Podcast)
            .WithMany(w => w.Seasons)
            .OnDelete(DeleteBehavior.Cascade);
            
    }
}
