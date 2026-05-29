using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace OpportunityOS.Infrastructure.Persistence;

/// <summary>
/// Used by `dotnet ef migrations` at design time. Reads the connection string
/// from the EFCORE_CONNECTION env var, otherwise falls back to local defaults.
/// </summary>
public sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<OpportunityOsDbContext>
{
    public OpportunityOsDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("EFCORE_CONNECTION")
            ?? "Host=localhost;Port=5432;Database=opportunity_os;Username=postgres;Password=postgres";

        var options = new DbContextOptionsBuilder<OpportunityOsDbContext>()
            .UseNpgsql(connectionString)
            .Options;

        return new OpportunityOsDbContext(options);
    }
}
