using BTCPayServer.Data;
using BTCPayServer.Plugins.PodServer.Data.Models;
using BTCPayServer.Plugins.PodServer.Services.Podcasts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace BTCPayServer.Plugins.PodServer.Pages.Public;

[AllowAnonymous]
public class PublicPodcastModel : BasePageModel
{
    public Podcast Podcast { get; set; }
    public IEnumerable<Episode> Episodes { get; set; }

    public PublicPodcastModel(UserManager<ApplicationUser> userManager,
        PodcastService podcastService) : base(userManager, podcastService) {}

    public async Task<IActionResult> OnGetAsync(string podcastSlug)
    {
        Podcast = await PodcastService.GetPodcast(new PodcastsQuery {
            Slug = podcastSlug
        });
        if (Podcast == null) return NotFound();

        Episodes = await PodcastService.GetEpisodes(new EpisodesQuery
        {
            PodcastId = Podcast.PodcastId, 
            OnlyPublished = true
        });

        return Page();
    }
}
