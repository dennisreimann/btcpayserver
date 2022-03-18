namespace BTCPayServer.Plugins.PodServer.Services.Podcasts;

public class PodcastQuery
{
    public string UserId { get; set; }
    public string PodcastId { get; set; }    
    public bool IncludeSeasons { get; set; }
    public bool IncludeEpisodes { get; set; }
    public bool IncludePeople { get; set; }
}
