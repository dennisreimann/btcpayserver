using System.Xml;
using BTCPayServer.Plugins.PodServer.Data.Models;
using BTCPayServer.Plugins.PodServer.Services.Podcasts;

namespace BTCPayServer.Plugins.PodServer.Services.Feeds;

public class FeedImporter
{
    private readonly PodcastService _podcastService;

    public FeedImporter(PodcastService podcastService)
    {
        _podcastService = podcastService;
    }

    public async Task<Podcast> Import(IFormFile rssFile)
    {
        if (!rssFile.ContentType.EndsWith("xml"))
        {
            throw new Exception($"Invalid RSS file: Content type {rssFile.ContentType} does not match XML.");
        }

        using var reader = new StreamReader(rssFile.OpenReadStream());
        var rss = await reader.ReadToEndAsync();

        XmlDocument doc = new();
        doc.LoadXml(rss);

        var channel = doc.SelectSingleNode("/rss/channel");
        if (channel == null)
        {
            throw new Exception("Invalid RSS file: Channel information missing.");
        }

        var title = channel["title"]?.InnerText;
        var description = channel["description"]?.InnerText;
        var url = channel["link"]?.InnerText;
        var language = channel["language"]?.InnerText;
        var category = channel["itunes:category"]?.Attributes["text"]?.Value;
        var imageUrl = channel["image"]?["url"]?.InnerText;
        var owner = channel["itunes:owner"]?["itunes:name"]?.InnerText;
        var email = channel["itunes:owner"]?["itunes:email"]?.InnerText;
        
        // TODO:
        // - Import image from URL
        // - Value info
        
        var podcast = new Podcast
        {
            Title = title,
            Description = description,
            Language = language,
            Url = url,
            Category = category,
            Email = email,
            Owner = owner
        };
        
        // Episodes
        var items = channel.SelectNodes("item");

        await _podcastService.AddOrUpdatePodcast(podcast);

        return podcast;
    }
}
