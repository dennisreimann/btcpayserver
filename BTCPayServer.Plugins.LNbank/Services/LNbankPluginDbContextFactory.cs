using BTCPayServer.Abstractions.Contracts;
using BTCPayServer.Abstractions.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Options;

namespace BTCPayServer.Plugins.LNbank
{
    
    public class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<LNbankPluginDbContext>
    {
        public LNbankPluginDbContext CreateDbContext(string[] args)
        {

            var builder = new DbContextOptionsBuilder<LNbankPluginDbContext>();

            builder.UseSqlite("Data Source=temp.db");

            return new LNbankPluginDbContext(builder.Options, true);
        }
    }

    public class LNbankPluginDbContextFactory : BaseDbContextFactory<LNbankPluginDbContext>
    {
        public LNbankPluginDbContextFactory(IOptions<DatabaseOptions> options) : base(options, "BTCPayServer.Plugins.LNbank")
        {
        }

        public override LNbankPluginDbContext CreateContext()
        {
            var builder = new DbContextOptionsBuilder<LNbankPluginDbContext>();
            ConfigureBuilder(builder);
            return new LNbankPluginDbContext(builder.Options);
            
        }
    }
}
