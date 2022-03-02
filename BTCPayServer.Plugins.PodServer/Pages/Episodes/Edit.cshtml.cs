using BTCPayServer.Abstractions.Constants;
using BTCPayServer.Abstractions.Contracts;
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
    private readonly IFileService _fileService;
    public Podcast Podcast { get; set; }
    public Episode Episode { get; set; }
    public IFormFile ImageFile { get; set; }

    public EditModel(UserManager<ApplicationUser> userManager,
        PodcastService podcastService, IFileService fileService) : base(userManager, podcastService)
    {
        _fileService = fileService;
    }

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
        
        if (ImageFile != null)
        {
            // delete existing image
            if (!string.IsNullOrEmpty(Episode.ImageFileId))
            {
                await _fileService.RemoveFile(Episode.ImageFileId, UserId);
            }
            // add new image
            var storedFile = await _fileService.AddFile(ImageFile, UserId);
            Episode.ImageFileId = storedFile.Id;
        }

        if (!await TryUpdateModelAsync(
            Episode, 
            "episode",
            e => e.Title,
            e => e.Description,
            e => e.Number,
            e => e.ImageFileId,
            e => e.PublishedAt))
        {
            return Page();
        }
        
        Episode.LastUpdatedAt = DateTimeOffset.UtcNow; 
        await PodcastService.AddOrUpdateEpisode(Episode);
        TempData[WellKnownTempData.SuccessMessage] = "Episode successfully updated.";
        
        return RedirectToPage("./Episode", new { podcastId = Podcast.PodcastId, episodeId = Episode.EpisodeId });
    }
}
