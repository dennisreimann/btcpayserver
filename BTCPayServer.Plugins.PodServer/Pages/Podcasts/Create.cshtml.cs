using BTCPayServer.Abstractions.Constants;
using BTCPayServer.Client;
using BTCPayServer.Data;
using BTCPayServer.Plugins.PodServer.Data.Models;
using BTCPayServer.Plugins.PodServer.Services.Feeds;
using BTCPayServer.Plugins.PodServer.Services.Podcasts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace BTCPayServer.Plugins.PodServer.Pages.Podcasts;

[Authorize(AuthenticationSchemes = AuthenticationSchemes.Cookie, Policy = Policies.CanViewProfile)]
public class CreateModel : BasePageModel
{
    public Podcast Podcast { get; set; }

    public CreateModel(UserManager<ApplicationUser> userManager,
        PodcastService podcastService) : base(userManager, podcastService) {}

    public IActionResult OnGet()
    {
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid) return Page();

        Podcast = new Podcast
        {
            UserId = UserId
        };

        if (!await TryUpdateModelAsync(
            Podcast, 
            "podcast",
            p => p.Title,
            p => p.Description,
            p => p.Language,
            p => p.Category))
        {
            return Page();
        }
            
        await PodcastService.AddOrUpdatePodcast(Podcast);
        
        TempData[WellKnownTempData.SuccessMessage] = "Podcast successfully created.";
        return RedirectToPage("./Index", new { podcastId = Podcast.PodcastId });
    }
    
    public async Task<IActionResult> OnPostImportAsync([FromForm] IFormFile rssFile, [FromServices] FeedImporter importer)
    {
        try
        {
            Podcast = await importer.Import(rssFile);
            Podcast.UserId = UserId;

            await PodcastService.AddOrUpdatePodcast(Podcast);

            TempData[WellKnownTempData.SuccessMessage] = "Podcast successfully imported.";
            return RedirectToPage("./Index", new { podcastId = Podcast.PodcastId });
        }
        catch (Exception exception)
        {
            TempData[WellKnownTempData.ErrorMessage] = $"Import failed: {exception.Message}";
            return Page();
        }
    }
}
