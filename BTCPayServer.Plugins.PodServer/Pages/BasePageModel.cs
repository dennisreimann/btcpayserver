using BTCPayServer.Data;
using BTCPayServer.Plugins.PodServer.Services.Podcasts;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.RazorPages;

// https://dotnetstories.com/blog/How-to-implement-a-custom-base-class-for-razor-views-in-ASPNET-Core-en-7106773524?o=rss
namespace BTCPayServer.Plugins.PodServer.Pages;

public abstract class BasePageModel : PageModel
{
    private readonly UserManager<ApplicationUser> _userManager;
    protected readonly PodcastService PodcastService;
    protected string UserId => _userManager.GetUserId(User);
        
    protected BasePageModel(
        UserManager<ApplicationUser> userManager,
        PodcastService podcastService)
    {
        _userManager = userManager;
        PodcastService = podcastService;
    }
}
