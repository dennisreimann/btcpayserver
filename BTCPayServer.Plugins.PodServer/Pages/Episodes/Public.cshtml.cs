using BTCPayServer.Data;
using BTCPayServer.Plugins.PodServer.Data.Models;
using BTCPayServer.Plugins.PodServer.Services.Podcasts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace BTCPayServer.Plugins.PodServer.Pages.Episodes;

[AllowAnonymous]
public class PublicModel : BasePageModel
{
    public Podcast Podcast { get; set; }
    public Episode Episode { get; set; }

    public PublicModel(UserManager<ApplicationUser> userManager,
        PodcastService podcastService) : base(userManager, podcastService) {}

    public async Task<IActionResult> OnGetAsync(string episodeId)
    {
        Episode = await PodcastService.GetEpisode(new EpisodesQuery {
            EpisodeId = episodeId,
            IncludePodcast = true
        });
        if (!Episode.IsPublished) return NotFound();
        
        Podcast = Episode.Podcast;

        return Page();
    }
}
