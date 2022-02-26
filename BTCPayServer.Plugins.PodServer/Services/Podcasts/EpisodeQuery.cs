namespace BTCPayServer.Plugins.PodServer.Services.Podcasts;

public class EpisodeQuery
{
    public string PodcastId { get; set; }
    public string EpisodeId { get; set; }

    public string SeasonId { get; set; }
    public bool IncludePodcast { get; set; }
}
