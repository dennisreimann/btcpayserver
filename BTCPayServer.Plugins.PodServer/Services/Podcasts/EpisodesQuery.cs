namespace BTCPayServer.Plugins.PodServer.Services.Podcasts;

public class EpisodesQuery
{
    public string PodcastId { get; set; }
    public string EpisodeId { get; set; }
    public string SeasonId { get; set; }
    public bool IncludePodcast { get; set; }
    public bool OnlyPublished { get; set; }
}
