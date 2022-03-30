using System.Xml;
using BTCPayServer.Abstractions.Contracts;
using BTCPayServer.Plugins.PodServer.Data.Models;
using BTCPayServer.Plugins.PodServer.Services.Background;
using BTCPayServer.Plugins.PodServer.Services.Podcasts;

namespace BTCPayServer.Plugins.PodServer.Services.Imports;

public class FeedImporter
{
    private readonly ILogger<FeedImporter> _logger;
    private readonly ImportService _importService;
    private readonly PodcastService _podcastService;
    private readonly ITaskQueue _taskQueue;
    
    public FeedImporter(
        ILogger<FeedImporter> logger,
        ImportService importService,
        PodcastService podcastService,
        ITaskQueue taskQueue)
    {
        _logger = logger;
        _importService = importService;
        _podcastService = podcastService;
        _taskQueue = taskQueue;
    }

    public async Task<Podcast> CreatePodcast(string rss, string userId)
    {
        (XmlNode channel, _) = GetChannel(rss);

        var title = channel["title"]?.InnerText;
        var description = channel["description"]?.InnerText;
        var url = channel["link"]?.InnerText;
        var language = channel["language"]?.InnerText;
        var category = channel["itunes:category"]?.Attributes["text"]?.Value;
        var imageUrl = channel["image"]?["url"]?.InnerText;
        var owner = channel["itunes:owner"]?["itunes:name"]?.InnerText;
        var email = channel["itunes:owner"]?["itunes:email"]?.InnerText;
        
        IStoredFile imageFile = null;
        if (!string.IsNullOrEmpty(imageUrl))
        {
            imageFile = await _importService.DownloadFile(new Uri(imageUrl), userId);
        }
        
        var podcast = new Podcast
        {
            UserId = userId,
            Title = title,
            Description = description,
            Language = language,
            Url = url,
            Category = category,
            Email = email,
            Owner = owner,
            ImageFileId = imageFile?.Id
        };

        // Create podcast and import job
        await _podcastService.AddOrUpdatePodcast(podcast);
        var import = await _importService.CreateImport(rss, podcast.PodcastId, userId);
        await _taskQueue.QueueAsync(cancellationToken => Import(import.ImportId, cancellationToken));
        //await Import(import.ImportId, new CancellationToken());

        return podcast;
    }

    public async ValueTask Import(string importId, CancellationToken cancellationToken)
    {
        var import = await _importService.GetImport(importId);

        if (import.Status != ImportStatus.Created)
        {
            throw new Exception($"Unexpected import status: {import.Status}");
        }
        
        var podcast = await _podcastService.GetPodcast(new PodcastQuery
        {
            PodcastId = import.PodcastId,
            IncludeEpisodes = true,
            IncludePeople = true,
            IncludeSeasons = true
        });

        var log = $"New import: {DateTime.UtcNow}\n";
        var status = ImportStatus.Running;
        await _importService.UpdateStatus(import, status, log);
        _logger.LogInformation("Starting import for podcast {Id} ({Title})", podcast.PodcastId, podcast.Title);
        
        while (!cancellationToken.IsCancellationRequested && status == ImportStatus.Running)
        {
            log = "";
            try
            {
                (XmlNode channel, XmlNamespaceManager nsmgr) = GetChannel(import.Raw);
                
                // People
                var people = channel.SelectNodes("podcast:person", nsmgr);
                log += $"channel/podcast:person -> {people?.Count ?? 0} tags\n";
                if (people != null)
                {
                    foreach (XmlElement elem in people)
                    {
                        var person = await GetPersonByPersonTag(elem, podcast.PodcastId, import.UserId);
                        var isNew = string.IsNullOrEmpty(person.PersonId);
                        if (isNew) await _podcastService.AddOrUpdatePerson(person);
                        log += $"channel/podcast:person -> {(isNew ? "Added" : "Existed")}: '{person.Name}'\n";
                    }
                }

                // Value info
                var podcastValue = channel["podcast:value"];
                if (podcastValue != null)
                {
                    log += "channel/podcast:value -> Found tag\n";
                    var recipients = podcastValue.GetElementsByTagName("podcast:valueRecipient");
                    log += $"channel/podcast:value/podcast:valueRecipient -> {recipients?.Count ?? 0} tags\n";
                
                    foreach (XmlElement elem in recipients)
                    {
                        var person = await GetPersonByValueRecipientTag(elem, podcast.PodcastId);
                        if (person == null) continue; // might be null in case of fee entry
                        
                        var isNew = string.IsNullOrEmpty(person.PersonId);
                        if (isNew) await _podcastService.AddOrUpdatePerson(person);
                        log += $"channel/podcast:value/podcast:valueRecipient -> {(isNew ? "Added" : "Existed")}: Person '{person.Name}'\n";
                        
                        var contribution = await GetContributionByValueRecipientTag(elem, podcast.PodcastId, null, person.PersonId);
                        var isNewC = string.IsNullOrEmpty(contribution.ContributionId);
                        if (isNewC) await _podcastService.AddOrUpdateContribution(contribution);
                        log += $"channel/podcast:value/podcast:valueRecipient -> {(isNewC ? "Added" : "Existed")}: Contribution by '{person.Name}' with split '{contribution.Split}'\n";
                    }
                }
                
                // Seasons
                var seasons = channel.SelectNodes("//itunes:season | //podcast:season", nsmgr);
                log += $"channel/itunes|podcast:season -> {seasons?.Count ?? 0} tags\n";
                if (seasons != null)
                {
                    foreach (XmlElement elem in seasons)
                    {
                        var season = await GetSeasonByTag(elem, podcast.PodcastId);
                        var isNew = string.IsNullOrEmpty(season.SeasonId);
                        if (isNew) await _podcastService.AddOrUpdateSeason(season);
                        log += $"channel/itunes|podcast:season -> {(isNew ? "Added" : "Existed")}: Season '{season.Number}'\n";
                    }
                }
            
                // Episodes
                var items = channel.SelectNodes("item");
                log += $"channel/item -> {items?.Count ?? 0} tags\n";
                if (items != null)
                {
                    foreach (XmlElement elem in items)
                    {
                        var episode = await GetEpisodeByItemTag(elem, podcast.PodcastId, import.UserId);
                        var isNew = string.IsNullOrEmpty(episode.EpisodeId);
                        if (isNew) await _podcastService.AddOrUpdateEpisode(episode);
                        log += $"channel/item({episode.ImportGuid}) -> {(isNew ? "Added" : "Existed")}: Episode '{episode.Title}'\n";
                        
                        // Value info
                        var episodeValue = elem["podcast:value"];
                        if (episodeValue != null)
                        {
                            log += $"channel/item({episode.ImportGuid})/podcast:value -> Found tag\n";
                            var recipients = episodeValue.GetElementsByTagName("podcast:valueRecipient");
                            log += $"channel/item({episode.ImportGuid})/podcast:value/podcast:valueRecipient -> {recipients?.Count ?? 0} tags\n";
                
                            foreach (XmlElement el in recipients)
                            {
                                var person = await GetPersonByValueRecipientTag(el, podcast.PodcastId);
                                if (person == null) continue; // might be null in case of fee entry
                        
                                var isNewP = string.IsNullOrEmpty(person.PersonId);
                                if (isNewP) await _podcastService.AddOrUpdatePerson(person);
                                log += $"channel/item({episode.ImportGuid})/podcast:value/podcast:valueRecipient -> {(isNewP ? "Added" : "Existed")}: Person '{person.Name}'\n";
                        
                                var contribution = await GetContributionByValueRecipientTag(el, podcast.PodcastId, episode.EpisodeId, person.PersonId);
                                var isNewC = string.IsNullOrEmpty(contribution.ContributionId);
                                if (isNewC) await _podcastService.AddOrUpdateContribution(contribution);
                                log += $"channel/item({episode.ImportGuid})/podcast:value/podcast:valueRecipient -> {(isNewC ? "Added" : "Existed")}: Contribution by '{person.Name}' with split '{contribution.Split}'\n";
                            }
                        }
                    }
                }
            
                // Finish
                status = ImportStatus.Succeeded;
            }
            catch (Exception exception)
            {
                log += $"Error: {exception.Message}";
                _logger.LogError(exception, "Error importing podcast {Id} ({Title})", podcast.PodcastId, podcast.Title);

                status = ImportStatus.Failed;
            }
        }

        if (cancellationToken.IsCancellationRequested)
        {
            status = ImportStatus.Cancelled;
        }
        
        _logger.LogInformation("{Status} import for podcast {Id} ({Title})", status, podcast.PodcastId, podcast.Title);
        await _importService.UpdateStatus(import, status, $"{log}\n---\n");
    }

    private static (XmlNode, XmlNamespaceManager) GetChannel(string rss)
    {
        XmlDocument doc = new();
        doc.LoadXml(rss);

        var channel = doc.SelectSingleNode("/rss/channel");
        if (channel == null)
        {
            throw new Exception("Invalid RSS: Channel information missing.");
        }
        
        var nsmgr = new XmlNamespaceManager(doc.NameTable);
        nsmgr.AddNamespace("itunes", "http://www.itunes.com/dtds/podcast-1.0.dtd");
        nsmgr.AddNamespace("podcast", "https://podcastindex.org/namespace/1.0");

        return (channel, nsmgr);
    }

    private async Task<Season> GetSeasonByTag(XmlElement elem, string podcastId)
    {
        var name = elem.GetAttribute("name");
        var number = int.Parse(elem.InnerText);
        var query = new SeasonsQuery { Number = number, PodcastId = podcastId };
        return await _podcastService.GetSeason(query) ?? new Season
        {
            Name = name,
            Number = number,
            PodcastId = podcastId
        };
    }

    private async Task<Person> GetPersonByPersonTag(XmlElement elem, string podcastId, string userId)
    {
        var url = elem.GetAttribute("href");
        var imageUrl = elem.GetAttribute("img");
        var name = elem.InnerText;
        
        var query = new PeopleQuery { Name = name, PodcastId = podcastId };
        var person = await _podcastService.GetPerson(query);
        if (person != null) return person;
        
        IStoredFile imageFile = null;
        if (!string.IsNullOrEmpty(imageUrl))
        {
            imageFile = await _importService.DownloadFile(new Uri(imageUrl), userId);
        }
            
        return new Person
        {
            Name = name, 
            Url = url,
            ImageFileId = imageFile?.Id,
            PodcastId = podcastId
        };
    }

    private async Task<Person> GetPersonByValueRecipientTag(XmlElement elem, string podcastId)
    {
        var fee = elem.GetAttribute("fee") == "true";
        if (fee) return null;
        
        var name = elem.GetAttribute("name");
        var query = new PeopleQuery { Name = name, PodcastId = podcastId };
        return await _podcastService.GetPerson(query) ?? new Person
        {
            Name = name, 
            PodcastId = podcastId,
            ValueRecipient = new ValueRecipient
            {
                Type = elem.GetAttribute("type"),
                Address = elem.GetAttribute("address"),
                CustomKey = elem.GetAttribute("customKey"),
                CustomValue = elem.GetAttribute("customValue")
            }
        };
    }
    
    private async Task<Contribution> GetContributionByValueRecipientTag(XmlElement elem, string podcastId, string episodeId, string personId)
    {
        var query = new ContributionsQuery { PodcastId = podcastId, EpisodeId = episodeId, PersonId = personId };
        return await _podcastService.GetContribution(query) ?? new Contribution
        {
            Split = int.Parse(elem.GetAttribute("split")),
            PodcastId = podcastId,
            EpisodeId = episodeId,
            PersonId = personId
        };
    }
    
    private async Task<Episode> GetEpisodeByItemTag(XmlElement item, string podcastId, string userId)
    {
        var guid = item["guid"]?.InnerText;
        var query = new EpisodesQuery { ImportGuid = guid, PodcastId = podcastId };
        var episode = await _podcastService.GetEpisode(query);
        if (episode != null) return episode;
        
        // Enclosure
        Enclosure enclosure = null;
        if (item["enclosure"] != null && !string.IsNullOrEmpty(item["enclosure"].GetAttribute("url")))
        {
            IStoredFile enclosureFile = await _importService.DownloadFile(new Uri(item["enclosure"].GetAttribute("url")), userId);
            enclosure = new Enclosure
            {
                FileId = enclosureFile.Id,
                Type = item["enclosure"].GetAttribute("type"),
                Title = item["enclosure"].HasAttribute("title") ? item["enclosure"].GetAttribute("title") : null,
                Length = int.Parse(item["enclosure"].GetAttribute("length"))
            };
        }
            
        // Image
        var imageUrl = item["itunes:image"]?.GetAttribute("href");
        IStoredFile imageFile = null;
        if (!string.IsNullOrEmpty(imageUrl))
        {
            imageFile = await _importService.DownloadFile(new Uri(imageUrl), userId);
        }

        // Season
        Season season = null;
        if (item["podcast:season"] != null)
        {
            season = await GetSeasonByTag(item["podcast:season"], podcastId);
        }
        else if (item["itunes:season"] != null)
        {
            season = await GetSeasonByTag(item["itunes:season"], podcastId);
        }
        
        // Dates
        var pubDate = item["pubDate"]?.InnerText;
        var publishedAt = string.IsNullOrEmpty(pubDate) ? DateTimeOffset.Now : DateTimeOffset.Parse(pubDate);

        episode = new Episode
        {
            PodcastId = podcastId,
            Title = item["title"]?.InnerText, 
            Description = item["description"]?.InnerText,
            LastUpdatedAt = DateTimeOffset.UtcNow,
            Number = int.Parse(item["itunes:episode"]?.InnerText ?? "0"),
            PublishedAt = publishedAt,
            ImportGuid = guid,
            ImageFileId = imageFile?.Id,
            SeasonId = season?.SeasonId,
            Enclosures = new List<Enclosure> { enclosure }
        };
        
        return episode;
    }
}
