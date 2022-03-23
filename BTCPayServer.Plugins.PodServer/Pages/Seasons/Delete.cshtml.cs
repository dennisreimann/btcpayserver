using BTCPayServer.Abstractions.Constants;
using BTCPayServer.Client;
using BTCPayServer.Data;
using BTCPayServer.Plugins.PodServer.Data.Models;
using BTCPayServer.Plugins.PodServer.Services.Podcasts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace BTCPayServer.Plugins.PodServer.Pages.Seasons;

[Authorize(AuthenticationSchemes = AuthenticationSchemes.Cookie, Policy = Policies.CanViewProfile)]
public class DeleteModel : BasePageModel
{
    public Season Season { get; set; }

    public DeleteModel(UserManager<ApplicationUser> userManager,
        PodcastService podcastService) : base(userManager, podcastService) {}

    public async Task<IActionResult> OnGet(string podcastId, string seasonId)
    {
        Season = await PodcastService.GetSeason(new SeasonsQuery {
            PodcastId = podcastId,
            SeasonId = seasonId
        });
        if (Season == null) return NotFound();

        return Page();
    }

    public async Task<IActionResult> OnPostAsync(string podcastId, string seasonId)
    {
        Season = await PodcastService.GetSeason(new SeasonsQuery {
            PodcastId = podcastId,
            SeasonId = seasonId
        });
        if (Season == null) return NotFound();

        await PodcastService.RemoveSeason(Season);
        TempData[WellKnownTempData.SuccessMessage] = "Season removed.";

        return RedirectToPage("./Index", new { podcastId = Season.PodcastId });
    }
}
