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
public class DeleteModel : BasePageModel
{
    public Contribution Contribution { get; set; }

    public DeleteModel(UserManager<ApplicationUser> userManager,
        PodcastService podcastService) : base(userManager, podcastService) {}

    public async Task<IActionResult> OnGet(string podcastId, string contributionId)
    {
        Contribution = await GetContribution(podcastId, contributionId);
        if (Contribution == null) return NotFound();

        return Page();
    }

    public async Task<IActionResult> OnPostAsync(string podcastId, string contributionId)
    {
        Contribution = await GetContribution(podcastId, contributionId);
        if (Contribution == null) return NotFound();

        await PodcastService.RemoveContribution(Contribution);
        TempData[WellKnownTempData.SuccessMessage] = "Contribution removed.";

        return RedirectToPage("./Index", new { podcastId = Contribution.PodcastId });
    }

    private async Task<Contribution> GetContribution(string podcastId, string contributionId)
    {
        return await PodcastService.GetContribution(new ContributionsQuery {
            PodcastId = podcastId,
            ContributionId = contributionId
        });
    }
}
