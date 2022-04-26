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
public class IndexModel : BasePageModel
{
    private readonly IFileService _fileService;
    public IEnumerable<Podcast> Podcasts { get; set; }
    public bool IsReady { get; set; }

    public IndexModel(
        UserManager<ApplicationUser> userManager,
        PodcastService podcastService,
        IFileService fileService) : base(userManager, podcastService)
    {
        _fileService = fileService;
    }

    public async Task<IActionResult> OnGet()
    {
        
        Podcasts = await PodcastService.GetPodcasts(new PodcastsQuery
        {
            UserId = UserId
        });

        IsReady = await _fileService.IsAvailable();

        if (IsReady)
        {
            var list = Podcasts.ToList();
            if (!list.Any())
            {
                return RedirectToPage("./Create");
            }
        }
        else
        {
            TempData[WellKnownTempData.ErrorMessage] = "In order to use PodServer, a file storage must be configured. It can be set up in Server Settings > Files.";
        }

        return Page();
    }
}
