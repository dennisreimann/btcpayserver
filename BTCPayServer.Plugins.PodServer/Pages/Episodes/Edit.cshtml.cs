using BTCPayServer.Abstractions.Constants;
using BTCPayServer.Abstractions.Contracts;
using BTCPayServer.Abstractions.Extensions;
using BTCPayServer.Client;
using BTCPayServer.Data;
using BTCPayServer.Plugins.PodServer.Data.Models;
using BTCPayServer.Plugins.PodServer.Services.Podcasts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.StaticFiles;

namespace BTCPayServer.Plugins.PodServer.Pages.Episodes;

[Authorize(AuthenticationSchemes = AuthenticationSchemes.Cookie, Policy = Policies.CanViewProfile)]
public class EditModel : BasePageModel
{
    private readonly IFileService _fileService;
    public Episode Episode { get; set; }
    public IFormFile ImageFile { get; set; }
    public IFormFile EnclosureFile { get; set; }

    public EditModel(UserManager<ApplicationUser> userManager,
        PodcastService podcastService, IFileService fileService) : base(userManager, podcastService)
    {
        _fileService = fileService;
    }

    public async Task<IActionResult> OnGet(string podcastId, string episodeId)
    {
        Episode = await PodcastService.GetEpisode(new EpisodeQuery {
            PodcastId = podcastId,
            EpisodeId = episodeId,
        });
        if (Episode == null) return NotFound();
        
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(string podcastId, string episodeId)
    {
        Episode = await PodcastService.GetEpisode(new EpisodeQuery {
            PodcastId = podcastId,
            EpisodeId = episodeId,
            ForEditing = true
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
            try
            {
                var storedFile = await _fileService.AddFile(ImageFile, UserId);
                Episode.ImageFileId = storedFile.Id;
            }
            catch (Exception e)
            {
                TempData[WellKnownTempData.ErrorMessage] = $"Could not save image: {e.Message}";
            }
        }
        
        if (EnclosureFile != null)
        {
            // delete existing image
            // TODO: Remove and allow for multiple enclosures
            if (Episode.Enclosures.Any())
            {
                foreach (var enclosure in Episode.Enclosures)
                {
                    await _fileService.RemoveFile(enclosure.FileId, UserId);
                    Episode.Enclosures.Remove(enclosure);
                }
            }
            // add new enclosure
            try
            {
                var storedFile = await _fileService.AddFile(EnclosureFile, UserId);
                var enclosure = new Enclosure
                {
                    FileId = storedFile.Id,
                    Type = EnclosureFile.ContentType,
                    Length = EnclosureFile.Length,
                };
                Episode.Enclosures.Add(enclosure);
            }
            catch (Exception e)
            {
                TempData[WellKnownTempData.ErrorMessage] = $"Could not save media file: {e.Message}";
            }
        }

        if (!await TryUpdateModelAsync(
            Episode, 
            "episode",
            e => e.Title,
            e => e.Description,
            e => e.Number,
            e => e.ImageFileId,
            e => e.PublishedAt,
            e => e.Enclosures))
        {
            return Page();
        }
        
        Episode.LastUpdatedAt = DateTimeOffset.UtcNow; 
        await PodcastService.AddOrUpdateEpisode(Episode);
        if (TempData[WellKnownTempData.ErrorMessage] is null)
        {
            TempData[WellKnownTempData.SuccessMessage] = "Episode successfully updated.";
        }
        
        return RedirectToPage("./Episode", new { podcastId = Episode.PodcastId, episodeId = Episode.EpisodeId });
    }
}
