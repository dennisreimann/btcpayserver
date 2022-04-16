using BTCPayServer.Abstractions.Contracts;
using BTCPayServer.Abstractions.Models;
using BTCPayServer.Plugins.Buchhalter.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Options;

namespace BTCPayServer.Plugins.Buchhalter.Services
{

    public class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<BuchhalterPluginDbContext>
    {
        public BuchhalterPluginDbContext CreateDbContext(string[] args)
        {

            var builder = new DbContextOptionsBuilder<BuchhalterPluginDbContext>();

            builder.UseSqlite("Data Source=temp.db");

            return new BuchhalterPluginDbContext(builder.Options, true);
        }
    }

    public class BuchhalterPluginDbContextFactory : BaseDbContextFactory<BuchhalterPluginDbContext>
    {
        public BuchhalterPluginDbContextFactory(IOptions<DatabaseOptions> options) : base(options, "BTCPayServer.Plugins.Buchhalter")
        {
        }

        public override BuchhalterPluginDbContext CreateContext()
        {
            var builder = new DbContextOptionsBuilder<BuchhalterPluginDbContext>();
            ConfigureBuilder(builder);
            return new BuchhalterPluginDbContext(builder.Options);

        }
    }
}
