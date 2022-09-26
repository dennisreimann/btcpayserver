using System.Threading;
using System.Threading.Tasks;
using BTCPayServer.Abstractions.Contracts;
using BTCPayServer.Lightning;
using BTCPayServer.Plugins.LNbank.Services.Wallets;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;

namespace BTCPayServer.Plugins.LNbank.Services;

public class LNbankPluginMigrationRunner : IHostedService
{
    public class LNbankPluginDataMigrationHistory
    {
        public bool ExtendedAccessKeysWithUserId { get; set; }
        public bool ExtendedTransactionsWithPaymentHash { get; set; }
    }
    
    private readonly LNbankPluginDbContextFactory _dbContextFactory;
    private readonly ISettingsRepository _settingsRepository;
    private readonly WalletService _walletService;

    public LNbankPluginMigrationRunner(
        LNbankPluginDbContextFactory testPluginDbContextFactory,
        ISettingsRepository settingsRepository,
        WalletService walletService)
    {
        _dbContextFactory = testPluginDbContextFactory;
        _settingsRepository = settingsRepository;
        _walletService = walletService;
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
            var accessKeys = await dbContext.AccessKeys
                .Include(a => a.Wallet)
                .AsNoTracking()
                .ToListAsync(cancellationToken: cancellationToken);
            foreach (var accessKey in accessKeys)
            {
                accessKey.UserId = accessKey.Wallet?.UserId;
                dbContext.Update(accessKey);
            }
            await dbContext.SaveChangesAsync(cancellationToken);
        
            settings.ExtendedAccessKeysWithUserId = true;
            await _settingsRepository.UpdateSetting(settings);
        }
        
        if (!settings.ExtendedTransactionsWithPaymentHash)
        {
            var transactions = await dbContext.Transactions.ToListAsync(cancellationToken: cancellationToken);
            foreach (var transaction in transactions)
            {
                var bolt11 = _walletService.ParsePaymentRequest(transaction.PaymentRequest);
                transaction.PaymentHash = bolt11.PaymentHash?.ToString();
                dbContext.Update(transaction);
            }
            await dbContext.SaveChangesAsync(cancellationToken);
        
            settings.ExtendedTransactionsWithPaymentHash = true;
            await _settingsRepository.UpdateSetting(settings);
        }
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}
