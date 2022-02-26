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
public class EditModel : BasePageModel
{
    public Podcast Podcast { get; set; }

    public EditModel(UserManager<ApplicationUser> userManager,
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
            PodcastId = podcastId
        });
        if (Podcast == null) return NotFound();
        
        if (!ModelState.IsValid) return Page();

        if (!await TryUpdateModelAsync(
                Podcast, 
                "podcast",
                p => p.Title,
                p => p.Description,
                p => p.Language,
                p => p.Category,
                p => p.Owner,
                p => p.Email,
                p => p.Url,
                p => p.MainImage))
        {
            return Page();
        }
        
        TempData[WellKnownTempData.SuccessMessage] = "Podcast successfully updated.";
        await PodcastService.AddOrUpdatePodcast(Podcast);
        
        return RedirectToPage("./Podcast", new { podcastId = Podcast.PodcastId });
    }
}
