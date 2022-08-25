using BTCPayServer.Abstractions.Constants;
using BTCPayServer.Client;
using BTCPayServer.Data;
using BTCPayServer.Plugins.PodServer.Data.Models;
using BTCPayServer.Plugins.PodServer.Services.Podcasts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace BTCPayServer.Plugins.PodServer.Pages.Contributions;

[Authorize(AuthenticationSchemes = AuthenticationSchemes.Cookie, Policy = Policies.CanViewProfile)]
public class IndexModel : BasePageModel
{
    public Podcast Podcast { get; set; }
    public IEnumerable<Contribution> Contributions { get; set; }
    public bool HasPeople { get; set; }

    public IndexModel(UserManager<ApplicationUser> userManager,
        PodcastService podcastService) : base(userManager, podcastService) {}

    public async Task<IActionResult> OnGet(string podcastId)
    {
        Podcast = await PodcastService.GetPodcast(new PodcastsQuery {
            UserId = UserId,
            PodcastId = podcastId,
            IncludePeople = true,
            IncludeContributions = true
        });
        
        Contributions = Podcast.Contributions.OrderByDescending(c => c.Person.Name);
        HasPeople = Podcast.People.Any();
        
        return Page();
    }
}
