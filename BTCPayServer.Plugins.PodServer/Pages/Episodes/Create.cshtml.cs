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
public class CreateModel : BasePageModel
{
    public Podcast Podcast { get; set; }
    public Episode Episode { get; set; }

    public CreateModel(UserManager<ApplicationUser> userManager,
        PodcastService podcastService) : base(userManager, podcastService) {}

    public IActionResult OnGet(string podcastId)
    {
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(string podcastId)
    {
        if (!ModelState.IsValid) return Page();

        Podcast = await PodcastService.GetPodcast(new PodcastQuery { UserId = UserId, PodcastId = podcastId });
        
        Episode = new Episode
        {
            PodcastId = Podcast.PodcastId
        };

        if (!await TryUpdateModelAsync(
            Episode, 
            "episode",
            e => e.Title,
            e => e.Description))
        {
            return Page();
        }
            
        await PodcastService.AddOrUpdateEpisode(Episode);
        
        TempData[WellKnownTempData.SuccessMessage] = "Episode successfully created.";
        return RedirectToPage("./Episode", new { podcastId = Podcast.PodcastId, episodeId = Episode.EpisodeId });
    }
}