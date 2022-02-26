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
public class IndexModel : BasePageModel
{
    public IEnumerable<Podcast> Podcasts { get; set; }

    public IndexModel(UserManager<ApplicationUser> userManager,
        PodcastService podcastService) : base(userManager, podcastService) {}

    public async Task<IActionResult> OnGet()
    {
        Podcasts = await PodcastService.GetPodcasts(new PodcastsQuery
        {
            UserId = new[] { UserId }
        });

        var list = Podcasts.ToList();
        if (!list.Any())
        {
            return RedirectToPage("./Create");
        }
        
        return Page();
    }
}
