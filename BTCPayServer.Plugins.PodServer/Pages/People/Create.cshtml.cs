using BTCPayServer.Abstractions.Constants;
using BTCPayServer.Client;
using BTCPayServer.Data;
using BTCPayServer.Plugins.PodServer.Data.Models;
using BTCPayServer.Plugins.PodServer.Services.Podcasts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace BTCPayServer.Plugins.PodServer.Pages.People;

[Authorize(AuthenticationSchemes = AuthenticationSchemes.Cookie, Policy = Policies.CanViewProfile)]
public class CreateModel : BasePageModel
{
    public Podcast Podcast { get; set; }
    public Person Person { get; set; }

    public CreateModel(UserManager<ApplicationUser> userManager,
        PodcastService podcastService) : base(userManager, podcastService) {}

    public async Task<IActionResult> OnGet(string podcastId)
    {
        Podcast = await PodcastService.GetPodcast(new PodcastsQuery {
            UserId = UserId,
            PodcastId = podcastId
        });
        if (Podcast == null) return NotFound();
        
        Person = new Person
        {
            PodcastId = Podcast.PodcastId
        };
        
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(string podcastId)
    {
        if (!ModelState.IsValid) return Page();

        Podcast = await PodcastService.GetPodcast(new PodcastsQuery { UserId = UserId, PodcastId = podcastId });
        
        Person = new Person
        {
            PodcastId = Podcast.PodcastId
        };

        if (await TryUpdateModelAsync(
            Person, 
            "person",
            p => p.Name))
        {
            await PodcastService.AddOrUpdatePerson(Person);
        
            TempData[WellKnownTempData.SuccessMessage] = "Person successfully created.";
            return RedirectToPage("./Edit", new { podcastId = Podcast.PodcastId, personId = Person.PersonId });
        }
        
        return Page();
    }
}
