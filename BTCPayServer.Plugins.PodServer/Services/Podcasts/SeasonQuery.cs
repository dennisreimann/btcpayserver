namespace BTCPayServer.Plugins.PodServer.Services.Podcasts;

public class SeasonQuery
{
    public string PodcastId { get; set; }
    public bool IncludePodcast { get; set; }
    public string SeasonId { get; set; }
}
