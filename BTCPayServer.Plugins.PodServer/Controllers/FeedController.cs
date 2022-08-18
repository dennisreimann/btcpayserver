using System.ServiceModel.Syndication;
using System.Text;
using System.Xml;
using BTCPayServer.Plugins.PodServer.Data.Models;
using BTCPayServer.Plugins.PodServer.Services.Podcasts;
using Microsoft.AspNetCore.Mvc;

namespace BTCPayServer.Plugins.PodServer.Controllers;

public class FeedController : Controller
{
    private readonly PodcastService _podcastService;
    
    public FeedController(PodcastService podcastService)
    {
        _podcastService = podcastService;
    }
    
    // https://hamedfathi.me/a-professional-asp.net-core-rss/
    [ResponseCache(Duration = 1200)]
    [Produces("application/rss+xml")]
    [HttpGet("/plugins/podserver/podcast/{podcastId}/feed")]
    public async Task<IActionResult> Feed(string podcastId)
    {
        var podcast = await _podcastService.GetPodcast(new PodcastsQuery {
            PodcastId = podcastId
        });
        if (podcast == null) return NotFound();

        var episodes = (await _podcastService.GetEpisodes(new EpisodesQuery
        {
            PodcastId = podcastId, 
            OnlyPublished = true
        })).ToList();

        var lastUpdated = episodes.FirstOrDefault()?.LastUpdatedAt ?? DateTimeOffset.Now;
        
        // https://docs.microsoft.com/en-us/dotnet/api/system.servicemodel.syndication.syndicationfeed
        var feed = new SyndicationFeed(podcast.Title, podcast.Description, new Uri(podcast.Url), podcast.PodcastId, lastUpdated)
        {
            Copyright = new TextSyndicationContent($"{DateTime.Now.Year} Hamed Fathi"),
            Items = (from episode in episodes 
                let episodeUrl = Url.PageLink("Episode", null, new { episodeId = episode.EpisodeId }, HttpContext.Request.Scheme) 
                let title = episode.Title 
                let description = episode.Description 
                select new SyndicationItem(title, description, new Uri(episodeUrl), episode.EpisodeId, episode.LastUpdatedAt)).ToList()
        };
        
        await using var stream = await WriteFeedToStream(feed);
        return File(stream.ToArray(), "application/rss+xml;charset=utf-8");
    }
    
    private static async Task<MemoryStream> WriteFeedToStream(SyndicationFeed feed) 
    {
        using var stream = new MemoryStream();
        var xmlWriter = XmlWriter.Create(stream, new XmlWriterSettings
        {
            Encoding = Encoding.UTF8,
            NewLineHandling = NewLineHandling.Entitize,
            NewLineOnAttributes = true,
            Indent = true,
            Async = true
        });
        
        var rssFormatter = new Rss20FeedFormatter(feed, false);
        rssFormatter.WriteTo(xmlWriter);
        await xmlWriter.FlushAsync();

        return stream;
    }
}
