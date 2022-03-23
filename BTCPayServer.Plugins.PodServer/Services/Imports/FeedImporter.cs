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
        var import = await _importService.CreateImport(podcast.PodcastId, rss);
        await _taskQueue.QueueAsync(cancellationToken => Import(import.ImportId, cancellationToken));

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
                        var person = await GetPersonByPersonTag(podcast.PodcastId, elem);
                        var isNew = string.IsNullOrEmpty(person.PersonId);
                        if (isNew) await _podcastService.AddOrUpdatePerson(person);
                        log += $"channel/podcast:person -> {(isNew ? "Added" : "Existed")}: '{person.Name}'\n";
                    }
                }

                // Value info
                var podcastValue = channel["podcast:value"];
                log += "channel/podcast:value -> Found tag\n";
                if (podcastValue != null)
                {
                    var recipients = podcastValue.GetElementsByTagName("podcast:valueRecipient");
                    log += $"channel/podcast:value/podcast:valueRecipient -> {recipients?.Count ?? 0} tags\n";
                
                    foreach (XmlElement elem in recipients)
                    {
                        var person = await GetPersonByValueRecipientTag(podcast.PodcastId, elem);
                        if (person == null) continue; // might be null in case of fee entry
                        
                        var isNew = string.IsNullOrEmpty(person.PersonId);
                        if (isNew) await _podcastService.AddOrUpdatePerson(person);
                        log += $"channel/podcast:value/podcast:valueRecipient -> {(isNew ? "Added" : "Existed")}: Person '{person.Name}'\n";
                        
                        var contribution = await GetContributionByValueRecipientTag(podcast.PodcastId, null, person.PersonId, elem);
                        var isNewC = string.IsNullOrEmpty(contribution.ContributionId);
                        if (isNewC) await _podcastService.AddOrUpdateContribution(contribution);
                        log += $"channel/podcast:value/podcast:valueRecipient -> {(isNew ? "Added" : "Existed")}: Contribution by '{person.Name}' with split '{contribution.Split}'\n";
                    }
                }
            
                // Episodes
                var items = channel.SelectNodes("item");
                log += $"channel/item -> {items?.Count ?? 0} tags\n";
                if (items != null)
                {
                    foreach (XmlElement elem in items)
                    {
                        var episode = await GetEpisodeByItemTag(podcast.PodcastId, elem);
                        if (episode == null) continue;
                    
                        var contribution = new Contribution
                        {
                            Split = int.Parse(elem.GetAttribute("split")),
                            PodcastId = podcast.PodcastId,
                            EpisodeId = episode.EpisodeId,
                            PersonId = person.PersonId,
                        };
                        await _podcastService.AddOrUpdateContribution(contribution);
                    }
                }
            
                // Finish
                status = ImportStatus.Succeeded;
            }
            catch (Exception exception)
            {
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
        nsmgr.AddNamespace("podcast", "https://podcastindex.org/namespace/1.0");

        return (channel, nsmgr);
    }

    private async Task<Person> GetPersonByPersonTag(string podcastId, XmlElement elem)
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
            imageFile = await _importService.DownloadFile(new Uri(imageUrl), null);
        }
            
        return new Person
        {
            Name = name, 
            Url = url,
            ImageFileId = imageFile?.Id,
            PodcastId = podcastId
        };
    }

    private async Task<Person> GetPersonByValueRecipientTag(string podcastId, XmlElement elem)
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
    
    private async Task<Contribution> GetContributionByValueRecipientTag(string podcastId, string episodeId, string personId, XmlElement elem)
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
    

    private async Task<Episode> GetEpisodeByItemTag(string podcastId, XmlElement item)
    {
        var guid = item["guid"]?.InnerText;
        
        var query = new EpisodesQuery { ImportGuid = guid, PodcastId = podcastId };
        var episode = await _podcastService.GetEpisode(query);
        if (episode == null)
        {
            Enclosure enclosure = null;
            if (item["enclosure"] != null && !string.IsNullOrEmpty(item["enclosure"].GetAttribute("url")))
            {
                IStoredFile enclosureFile = await _importService.DownloadFile(new Uri(item["enclosure"].GetAttribute("url")), null);
                enclosure = new Enclosure
                {
                    FileId = enclosureFile.Id,
                    Type = item["enclosure"].GetAttribute("type"),
                    Title = item["enclosure"].GetAttribute("title"),
                    Length = int.Parse(item["enclosure"].GetAttribute("length"))
                };
            }
            
            var imageUrl = item["itunes:image"]?.InnerText;
            IStoredFile imageFile = null;
            if (!string.IsNullOrEmpty(imageUrl))
            {
                imageFile = await _importService.DownloadFile(new Uri(imageUrl), null);
            }

            episode = new Episode
            {
                PodcastId = podcastId,
                Title = item.GetAttribute("title"), 
                Description = item.GetAttribute("description"),
                PublishedAt = item.GetAttribute("pubDate"),
                ImportGuid = guid,
                ImageFileId = imageFile?.Id,
                Enclosures = enclosurenew List<Enclosure>() { enclosure }
            };
            _logger.LogInformation("Value recipient: Adding person {Name}", person.Name);
            await _podcastService.AddOrUpdateEpisode(episode);
        }
        else
        {
            _logger.LogInformation("Value recipient: Skipping existing person {Name}", person.Name);
        }

        return person;
    }
}
