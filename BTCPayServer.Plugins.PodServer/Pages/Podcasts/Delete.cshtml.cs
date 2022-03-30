using BTCPayServer.Abstractions.Constants;
using BTCPayServer.Client;
using BTCPayServer.Data;
using BTCPayServer.Plugins.PodServer.Data.Models;
using BTCPayServer.Plugins.PodServer.Services.Podcasts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace BTCPayServer.Plugins.PodServer.Pages.Podcasts;

[Authorize(AuthenticationSchemes = AuthenticationSchemes.Cookie, Policy = Policies.CanViewProfile)]
public class DeleteModel : BasePageModel
{
    public Podcast Podcast { get; set; }

    public DeleteModel(UserManager<ApplicationUser> userManager,
        PodcastService podcastService) : base(userManager, podcastService) {}

    public async Task<IActionResult> OnGet(string podcastId)
    {
        Podcast = await PodcastService.GetPodcast(new PodcastQuery {
            UserId = UserId,
            PodcastId = podcastId
        });
        if (Podcast == null) return NotFound();
        
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(string podcastId)
    {
        Podcast = await PodcastService.GetPodcast(new PodcastQuery {
            UserId = UserId,
            PodcastId = podcastId,
            IncludeEpisodes = true,
            IncludePeople = true,
            IncludeSeasons = true,
            IncludeContributions = true
        });
        if (Podcast == null) return NotFound();

        await PodcastService.RemovePodcast(Podcast);
        TempData[WellKnownTempData.SuccessMessage] = "Podcast removed.";

        return RedirectToPage("./Index");
    }
}
