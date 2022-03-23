using BTCPayServer.Plugins.PodServer.Services.Background;
using BTCPayServer.Plugins.PodServer.Services.Imports;
using BTCPayServer.Plugins.PodServer.Services.Podcasts;

namespace BTCPayServer.Plugins.PodServer.Extensions;

public static class AppExtensions
{
    public static void AddAppServices(this IServiceCollection services)
    {
        services.AddHostedService<TaskQueueService>();
        services.AddSingleton<PodcastService>();
        services.AddSingleton<ImportService>();
        services.AddSingleton<FeedImporter>();
        services.AddSingleton<ITaskQueue>(ctx => new TaskQueue(10));
    }
}
