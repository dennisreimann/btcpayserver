using System.Threading;
using System.Threading.Tasks;
using BTCPayServer.Abstractions.Contracts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;

namespace BTCPayServer.Plugins.LNbank.Services;

public class LNbankPluginMigrationRunner : IHostedService
{
    public class LNbankPluginDataMigrationHistory
    {
        public bool ExtendedAccessKeysWithUserId { get; set; }
    }
    
    private readonly LNbankPluginDbContextFactory _dbContextFactory;
    private readonly ISettingsRepository _settingsRepository;

    public LNbankPluginMigrationRunner(LNbankPluginDbContextFactory testPluginDbContextFactory, ISettingsRepository settingsRepository)
    {
        _dbContextFactory = testPluginDbContextFactory;
        _settingsRepository = settingsRepository;
    }
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var settings = await _settingsRepository.GetSettingAsync<LNbankPluginDataMigrationHistory>() ??
                       new LNbankPluginDataMigrationHistory();
        
        await using var ctx = _dbContextFactory.CreateContext();
        await using var dbContext = _dbContextFactory.CreateContext();
        await ctx.Database.MigrateAsync(cancellationToken: cancellationToken);
        
        if (!settings.ExtendedAccessKeysWithUserId)
        {
            var accessKeys = dbContext.AccessKeys.Include(a => a.Wallet).AsNoTracking();
            foreach (var accessKey in accessKeys)
            {
                accessKey.UserId = accessKey.Wallet.UserId;
                dbContext.Update(accessKey);
            }
            await dbContext.SaveChangesAsync(cancellationToken);
        
            settings.ExtendedAccessKeysWithUserId = true;
            await _settingsRepository.UpdateSetting(settings);
        }
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}
