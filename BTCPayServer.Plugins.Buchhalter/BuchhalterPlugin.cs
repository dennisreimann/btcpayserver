using System;
using BTCPayServer.Abstractions.Contracts;
using BTCPayServer.Abstractions.Models;
using BTCPayServer.Plugins.Buchhalter.Data;
using BTCPayServer.Plugins.Buchhalter.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace BTCPayServer.Plugins.Buchhalter
{
    public class TestPlugin : BaseBTCPayServerPlugin
    {
        public override string Name { get; } = "Buchhalter";
        public override string Identifier { get; } = "BTCPayServer.Plugins.Buchhalter";
        public override string Description { get; } = "Additional export options for wallets and invoices. Primarily for tax filings in Germany.";
        public override IBTCPayServerPlugin.PluginDependency[] Dependencies { get; } = new[]
        {
            new IBTCPayServerPlugin.PluginDependency
            {
                Identifier = nameof(BTCPayServer),
                Condition = ">=1.4.8.0"
            }
        };

        public override void Execute(IServiceCollection services)
        {
            //services.AddSingleton<IUIExtension>(new UIExtension("BuchhalterNavExtension", "header-nav"));
            services.AddSingleton<BuchhalterPluginDbContextFactory>();
            services.AddDbContext<BuchhalterPluginDbContext>((provider, o) =>
            {
                var factory = provider.GetRequiredService<BuchhalterPluginDbContextFactory>();
                factory.ConfigureBuilder(o);
            });
        }

        public override void Execute(IApplicationBuilder applicationBuilder, IServiceProvider applicationBuilderApplicationServices)
        {
            base.Execute(applicationBuilder, applicationBuilderApplicationServices);
            applicationBuilderApplicationServices.GetService<BuchhalterPluginDbContextFactory>().CreateContext().Database.Migrate();
        }
    }
}
