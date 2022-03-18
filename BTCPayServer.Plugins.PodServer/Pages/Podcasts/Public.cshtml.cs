using BTCPayServer.Data;
using BTCPayServer.Plugins.PodServer.Data.Models;
using BTCPayServer.Plugins.PodServer.Services.Podcasts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace BTCPayServer.Plugins.PodServer.Pages.Podcasts;

[AllowAnonymous]
public class PublicModel : BasePageModel
{
    public Podcast Podcast { get; set; }
    public IEnumerable<Episode> Episodes { get; set; }

    public PublicModel(UserManager<ApplicationUser> userManager,
        PodcastService podcastService) : base(userManager, podcastService) {}

    public async Task<IActionResult> OnGetAsync(string podcastId)
    {
        Podcast = await PodcastService.GetPodcast(new PodcastQuery {
            PodcastId = podcastId
        });
        if (Podcast == null) return NotFound();

        Episodes = await PodcastService.GetEpisodes(new EpisodesQuery
        {
            PodcastId = podcastId, 
            OnlyPublished = true
        });

        return Page();
    }
}
