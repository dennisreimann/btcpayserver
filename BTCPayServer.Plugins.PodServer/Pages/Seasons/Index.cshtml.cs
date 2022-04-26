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
public class IndexModel : BasePageModel
{
    public Podcast Podcast { get; set; }
    public IEnumerable<Season> Seasons { get; set; }

    public IndexModel(UserManager<ApplicationUser> userManager,
        PodcastService podcastService) : base(userManager, podcastService) {}

    public async Task<IActionResult> OnGet(string podcastId)
    {
        Podcast = await PodcastService.GetPodcast(new PodcastsQuery {
            UserId = UserId,
            PodcastId = podcastId,
            IncludeSeasons = true
        });
        
        Seasons = Podcast.Seasons.OrderByDescending(p => p.Number);
        
        return Page();
    }
}
