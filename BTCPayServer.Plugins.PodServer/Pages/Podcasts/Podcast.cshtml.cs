using BTCPayServer.Data;
using BTCPayServer.Abstractions.Constants;
using BTCPayServer.Client;
using BTCPayServer.Plugins.PodServer.Data.Models;
using BTCPayServer.Plugins.PodServer.Services.Podcasts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace BTCPayServer.Plugins.PodServer.Pages.Podcasts;

[Authorize(AuthenticationSchemes = AuthenticationSchemes.Cookie, Policy = Policies.CanViewProfile)]
public class PodcastModel : BasePageModel
{
    public Podcast Podcast { get; set; }
    public IEnumerable<Episode> Episodes { get; set; }

    public PodcastModel(UserManager<ApplicationUser> userManager,
        PodcastService podcastService) : base(userManager, podcastService) {}

    public async Task<IActionResult> OnGetAsync(string podcastId)
    {
        Podcast = await PodcastService.GetPodcast(new PodcastQuery {
            UserId = UserId,
            PodcastId = podcastId,
            IncludeEpisodes = true
        });
        
        Episodes = Podcast.Episodes.OrderByDescending(t => t.PublishedAt);
        
        return Page();
    }
}
