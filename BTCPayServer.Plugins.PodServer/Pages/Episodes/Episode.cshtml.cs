using BTCPayServer.Data;
using BTCPayServer.Abstractions.Constants;
using BTCPayServer.Client;
using BTCPayServer.Plugins.PodServer.Data.Models;
using BTCPayServer.Plugins.PodServer.Services.Podcasts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace BTCPayServer.Plugins.PodServer.Pages.Episodes;

[Authorize(AuthenticationSchemes = AuthenticationSchemes.Cookie, Policy = Policies.CanViewProfile)]
public class EpisodeModel : BasePageModel
{
    public Podcast Podcast { get; set; }
    public Episode Episode { get; set; }

    public EpisodeModel(UserManager<ApplicationUser> userManager,
        PodcastService podcastService) : base(userManager, podcastService) {}

    public async Task<IActionResult> OnGet(string podcastId, string episodeId)
    {
        Podcast = await PodcastService.GetPodcast(new PodcastsQuery {
            UserId = UserId,
            PodcastId = podcastId,
            IncludeEpisodes = true
        });
        if (Podcast == null) return NotFound();
        
        Episode = await PodcastService.GetEpisode(new EpisodesQuery {
            PodcastId = podcastId,
            EpisodeId = episodeId,
            IncludePodcast = true,
            IncludeContributions = true,
            IncludeEnclosures = true,
            IncludeSeason = true
        });
        if (Episode == null) return NotFound();
        
        return Page();
    }
}
