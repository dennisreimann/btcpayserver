using BTCPayServer.Abstractions.Constants;
using BTCPayServer.Abstractions.Contracts;
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
    private readonly IFileService _fileService;
    public Podcast Podcast { get; set; }
    public IFormFile ImageFile { get; set; }

    public EditModel(UserManager<ApplicationUser> userManager,
        PodcastService podcastService, IFileService fileService) : base(userManager, podcastService)
    {
        _fileService = fileService;
    }

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
        
        if (ImageFile != null)
        {
            // delete existing image
            if (!string.IsNullOrEmpty(Podcast.ImageFileId))
            {
                await _fileService.RemoveFile(Podcast.ImageFileId, UserId);
            }
            // add new image
            var storedFile = await _fileService.AddFile(ImageFile, UserId);
            Podcast.ImageFileId = storedFile.Id;
        }

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
                p => p.ImageFileId))
        {
            return Page();
        }
        
        await PodcastService.AddOrUpdatePodcast(Podcast);
        TempData[WellKnownTempData.SuccessMessage] = "Podcast successfully updated.";
        
        return RedirectToPage("./Podcast", new { podcastId = Podcast.PodcastId });
    }
}
