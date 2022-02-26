using BTCPayServer.Plugins.PodServer.Services.Podcasts;

namespace BTCPayServer.Plugins.PodServer.Extensions;

public static class AppExtensions
{
    public static void AddAppServices(this IServiceCollection services)
    {
        services.AddSingleton<PodcastService>();
    }
}
