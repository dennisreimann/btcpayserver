using BTCPayServer.Abstractions.Contracts;
using BTCPayServer.Plugins.PodServer.Data.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Query;

namespace BTCPayServer.Plugins.PodServer.Services.Podcasts;

public class PodcastService
{
    private readonly ILogger _logger;
    private readonly IFileService _fileService;
    private readonly PodServerPluginDbContextFactory _dbContextFactory;

    public PodcastService(
        ILogger<PodcastService> logger,
        IFileService fileService,
        PodServerPluginDbContextFactory dbContextFactory)
    {
        _logger = logger;
        _fileService = fileService;
        _dbContextFactory = dbContextFactory;
    }

    public async Task<IEnumerable<Podcast>> GetPodcasts(PodcastsQuery query)
    {
        await using var dbContext = _dbContextFactory.CreateContext();
        return await FilterPodcasts(dbContext.Podcasts.AsQueryable(), query).ToListAsync();
    }

    private IQueryable<Podcast> FilterPodcasts(IQueryable<Podcast> queryable, PodcastsQuery query)
    {
        if (query.UserId != null)
        {
            queryable = queryable.Where(podcast => query.UserId.Contains(podcast.UserId));
        }

        if (query.PodcastId != null)
        {
            queryable = queryable.Where(podcast => query.PodcastId.Contains(podcast.PodcastId));
        }

        if (query.IncludeEpisodes)
        {
            queryable = queryable.Include(p => p.Episodes).AsNoTracking();
        }

        if (query.IncludeSeasons)
        {
            queryable = queryable.Include(p => p.Seasons).AsNoTracking();
        }

        if (query.IncludePeople)
        {
            queryable = queryable.Include(p => p.People).AsNoTracking();
        }

        return queryable;
    }

    public async Task<Podcast> GetPodcast(PodcastQuery query)
    {
        await using var dbContext = _dbContextFactory.CreateContext();
        var podcastQuery = new PodcastsQuery
        {
            UserId = query.UserId is null ? null : new[] { query.UserId },
            PodcastId = query.PodcastId is null ? null : new[] { query.PodcastId },
            IncludeSeasons = query.IncludeSeasons,
            IncludeEpisodes = query.IncludeEpisodes,
            IncludePeople = query.IncludePeople,
        };
        return await FilterPodcasts(dbContext.Podcasts.AsQueryable(), podcastQuery).FirstOrDefaultAsync();
    }
    
    public async Task<Podcast> AddOrUpdatePodcast(Podcast podcast)
    {
        await using var dbContext = _dbContextFactory.CreateContext();

        EntityEntry entry;
        if (string.IsNullOrEmpty(podcast.PodcastId))
        {
            entry = await dbContext.Podcasts.AddAsync(podcast);
        }
        else
        {
            entry = dbContext.Update(podcast);
        }
        await dbContext.SaveChangesAsync();

        return (Podcast)entry.Entity;
    }

    public async Task RemovePodcast(Podcast podcast)
    {
        if (!string.IsNullOrEmpty(podcast.ImageFileId))
        {
            await _fileService.RemoveFile(podcast.ImageFileId, null);
        }
        await using var dbContext = _dbContextFactory.CreateContext();
        dbContext.Podcasts.Remove(podcast);
        await dbContext.SaveChangesAsync();
    }

    public async Task<IEnumerable<Episode>> GetEpisodes(EpisodesQuery query)
    {
        await using var dbContext = _dbContextFactory.CreateContext();
        var queryable = dbContext.Episodes.AsQueryable();

        if (query.PodcastId != null) query.IncludePodcast = true;

        if (query.PodcastId != null)
        {
            queryable = queryable.Where(e => e.PodcastId == query.PodcastId);
        }

        if (query.OnlyPublished)
        {
            queryable = queryable.Where(e => e.PublishedAt >= DateTime.UtcNow);
        }
        
        return await queryable.OrderByDescending(t => t.PublishedAt).ToListAsync();
    }

    public async Task<Episode> GetEpisode(EpisodeQuery query)
    {
        await using var dbContext = _dbContextFactory.CreateContext();
        IQueryable<Episode> queryable = dbContext.Episodes.AsQueryable();
        
        if (query.PodcastId != null)
        {
            var podcastQuery = new PodcastQuery
            { 
                PodcastId = query.PodcastId,
                IncludeEpisodes = true
            };

            var podcast = await GetPodcast(podcastQuery);
            if (podcast == null) return null;

            queryable = podcast.Episodes.AsQueryable();
        }
        
        if (query.IncludePodcast)
        {
            queryable = queryable.Include(e => e.Podcast).AsNoTracking();
        }
        
        if (query.ForEditing)
        {
            queryable = queryable
                .Include(e => e.Enclosures)
                .Include(e => e.Contributors);
        }
        else
        {
            queryable = queryable
                .Include(e => e.Enclosures).AsNoTracking()
                .Include(e => e.Contributors).AsNoTracking();
        }

        return queryable.FirstOrDefault();
    }
    
    public async Task<Episode> AddOrUpdateEpisode(Episode episode)
    {
        await using var dbContext = _dbContextFactory.CreateContext();

        EntityEntry entry;
        if (string.IsNullOrEmpty(episode.EpisodeId))
        {
            entry = await dbContext.Episodes.AddAsync(episode);
        }
        else
        {
            entry = dbContext.Update(episode);
        }
        await dbContext.SaveChangesAsync();

        return (Episode)entry.Entity;
    }

    public async Task RemoveEpisode(Episode episode)
    {
        if (!string.IsNullOrEmpty(episode.ImageFileId))
        {
            await _fileService.RemoveFile(episode.ImageFileId, null);
        }
        
        // TODO: Delete associated enclosures
        
        await using var dbContext = _dbContextFactory.CreateContext();
        dbContext.Episodes.Remove(episode);
        await dbContext.SaveChangesAsync();
    }

    private async Task<IEnumerable<Person>> GetPeople(PeopleQuery query)
    {
        await using var dbContext = _dbContextFactory.CreateContext();
        var queryable = dbContext.People.AsQueryable();

        if (query.PodcastId != null) query.IncludePodcast = true;

        if (query.PodcastId != null)
        {
            queryable = queryable.Where(e => e.PodcastId == query.PodcastId);
        }

        return await queryable.ToListAsync();
    }

    public async Task<Person> GetPerson(PersonQuery query)
    {
        await using var dbContext = _dbContextFactory.CreateContext();
        IQueryable<Person> queryable = dbContext.People.AsQueryable();
        
        if (query.PodcastId != null)
        {
            var podcastQuery = new PodcastQuery
            { 
                PodcastId = query.PodcastId,
                IncludePeople = true
            };

            var podcast = await GetPodcast(podcastQuery);
            if (podcast == null) return null;

            queryable = podcast.People.AsQueryable();
        }
        
        if (query.IncludePodcast)
        {
            queryable = queryable.Include(e => e.Podcast).AsNoTracking();
        }

        return queryable.FirstOrDefault();
    }
    
    public async Task<Person> AddOrUpdatePerson(Person person)
    {
        await using var dbContext = _dbContextFactory.CreateContext();

        EntityEntry entry;
        if (string.IsNullOrEmpty(person.PersonId))
        {
            entry = await dbContext.People.AddAsync(person);
        }
        else
        {
            entry = dbContext.Update(person);
        }
        await dbContext.SaveChangesAsync();

        return (Person)entry.Entity;
    }

    public async Task RemovePerson(Person person)
    {
        if (!string.IsNullOrEmpty(person.ImageFileId))
        {
            await _fileService.RemoveFile(person.ImageFileId, null);
        }
        
        await using var dbContext = _dbContextFactory.CreateContext();
        dbContext.People.Remove(person);
        await dbContext.SaveChangesAsync();
    }

    public async Task<IEnumerable<Season>> GetSeasons(SeasonsQuery query)
    {
        await using var dbContext = _dbContextFactory.CreateContext();
        var queryable = dbContext.Seasons.AsQueryable();

        if (query.PodcastId != null) query.IncludePodcast = true;

        if (query.PodcastId != null)
        {
            queryable = queryable.Where(e => e.PodcastId == query.PodcastId);
        }

        return await queryable.ToListAsync();
    }

    public async Task<Season> GetSeason(SeasonQuery query)
    {
        await using var dbContext = _dbContextFactory.CreateContext();
        IQueryable<Season> queryable = dbContext.Seasons.AsQueryable();
        
        if (query.PodcastId != null)
        {
            var podcastQuery = new PodcastQuery
            { 
                PodcastId = query.PodcastId,
                IncludeSeasons = true
            };

            var podcast = await GetPodcast(podcastQuery);
            if (podcast == null) return null;

            queryable = podcast.Seasons.AsQueryable();
        }
        
        if (query.IncludePodcast)
        {
            queryable = queryable.Include(e => e.Podcast).AsNoTracking();
        }

        return queryable.FirstOrDefault();
    }

    
    public async Task<Season> AddOrUpdateSeason(Season season)
    {
        await using var dbContext = _dbContextFactory.CreateContext();

        EntityEntry entry;
        if (string.IsNullOrEmpty(season.SeasonId))
        {
            entry = await dbContext.Seasons.AddAsync(season);
        }
        else
        {
            entry = dbContext.Update(season);
        }
        await dbContext.SaveChangesAsync();

        return (Season)entry.Entity;
    }

    public async Task RemoveSeason(Season season)
    {
        await using var dbContext = _dbContextFactory.CreateContext();
        dbContext.Seasons.Remove(season);
        await dbContext.SaveChangesAsync();
    }
}
