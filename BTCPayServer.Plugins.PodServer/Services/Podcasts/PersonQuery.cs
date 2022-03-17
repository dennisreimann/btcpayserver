namespace BTCPayServer.Plugins.PodServer.Services.Podcasts;

public class PersonQuery
{
    public string PodcastId { get; set; }
    public bool IncludePodcast { get; set; }
    public string PersonId { get; set; }
}
