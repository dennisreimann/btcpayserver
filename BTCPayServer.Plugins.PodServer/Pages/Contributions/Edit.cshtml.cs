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
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.StaticFiles;

namespace BTCPayServer.Plugins.PodServer.Pages.Contributions;

[Authorize(AuthenticationSchemes = AuthenticationSchemes.Cookie, Policy = Policies.CanViewProfile)]
public class EditModel : BasePageModel
{
    public Podcast Podcast { get; set; }
    public Contribution Contribution { get; set; }

    public EditModel(UserManager<ApplicationUser> userManager,
        PodcastService podcastService, IFileService fileService) : base(userManager, podcastService) {}

    public async Task<IActionResult> OnGet(string podcastId, string contributionId)
    {
        Podcast = await GetPodcast(podcastId);
        if (Podcast == null) return NotFound();

        if (!Podcast.People.Any())
        {
            TempData[WellKnownTempData.ErrorMessage] = "You need to add a person first, in order to create their contributions.";
            return RedirectToPage("/Person/Create", new { podcastId = Podcast.PodcastId });
        }
        
        if (contributionId == null)
        {
            Contribution = new Contribution
            {
                PodcastId = podcastId
            };
        }
        else
        {
            Contribution = await GetContribution(podcastId, contributionId);
            if (Contribution == null) return NotFound();
        }
        
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(string podcastId, string contributionId)
    {
        Podcast = await GetPodcast(podcastId);
        if (Podcast == null) return NotFound();
        
        var isNew = contributionId == null;
        if (isNew)
        {
            Contribution = new Contribution { PodcastId = podcastId };
        }
        else
        {
            Contribution = await GetContribution(podcastId, contributionId);
            if (Contribution == null) return NotFound();
        }

        if (!ModelState.IsValid) return Page();
        
        if (await TryUpdateModelAsync(Contribution, 
                "contribution",
                c => c.PersonId,
                c => c.Role, 
                c => c.Split))
        {
            await PodcastService.AddOrUpdateContribution(Contribution);
            if (TempData[WellKnownTempData.ErrorMessage] is null)
            {
                TempData[WellKnownTempData.SuccessMessage] = $"Contribution successfully {(isNew ? "created" : "updated")}.";
            }
        
            return RedirectToPage("./Index", new { podcastId = Contribution.PodcastId });
        }
        
        return Page();
    }

    private async Task<Podcast> GetPodcast(string podcastId)
    {
        return await PodcastService.GetPodcast(new PodcastsQuery {
            UserId = UserId,
            PodcastId = podcastId,
            IncludePeople = true,
            IncludeContributions = true
        });
    }

    private async Task<Contribution> GetContribution(string podcastId, string contributionId)
    {
        return await PodcastService.GetContribution(new ContributionsQuery {
            PodcastId = podcastId,
            ContributionId = contributionId,
        });
    }
}
