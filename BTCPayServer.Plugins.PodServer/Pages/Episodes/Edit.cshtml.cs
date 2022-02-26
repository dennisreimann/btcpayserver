using BTCPayServer.Abstractions.Constants;
using BTCPayServer.Client;
using BTCPayServer.Data;
using BTCPayServer.Plugins.PodServer.Data.Models;
using BTCPayServer.Plugins.PodServer.Services.Podcasts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace BTCPayServer.Plugins.PodServer.Pages.Episodes;

[Authorize(AuthenticationSchemes = AuthenticationSchemes.Cookie, Policy = Policies.CanViewProfile)]
public class EditModel : BasePageModel
{
    public Podcast Podcast { get; set; }
    public Episode Episode { get; set; }

    public EditModel(UserManager<ApplicationUser> userManager,
        PodcastService podcastService) : base(userManager, podcastService) {}

    public async Task<IActionResult> OnGet(string podcastId, string episodeId)
    {
        Podcast = await PodcastService.GetPodcast(new PodcastQuery {
            UserId = UserId,
            PodcastId = podcastId
        });
        if (Podcast == null) return NotFound();
        
        Episode = await PodcastService.GetEpisode(new EpisodeQuery {
            PodcastId = podcastId,
            EpisodeId = episodeId
        });
        if (Episode == null) return NotFound();
        
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(string podcastId, string episodeId)
    {
        Podcast = await PodcastService.GetPodcast(new PodcastQuery {
            UserId = UserId,
            PodcastId = podcastId
        });
        if (Podcast == null) return NotFound();
        
        Episode = await PodcastService.GetEpisode(new EpisodeQuery {
            PodcastId = podcastId,
            EpisodeId = episodeId
        });
        if (Episode == null) return NotFound();
        
        if (!ModelState.IsValid) return Page();

        if (!await TryUpdateModelAsync(
            Episode, 
            "episode",
            p => p.Title,
            p => p.Description))
        {
            return Page();
        }
        
        TempData[WellKnownTempData.SuccessMessage] = "Episode successfully updated.";
        await PodcastService.AddOrUpdateEpisode(Episode);
        
        return RedirectToPage("./Episode", new { podcastId = Podcast.PodcastId, episodeId = Episode.EpisodeId });
    }
}
