using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OpportunityOS.Application.Matching;
using OpportunityOS.Application.Normalization;
using OpportunityOS.Infrastructure.Persistence;

namespace OpportunityOS.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration config)
    {
        var connectionString = config.GetConnectionString("DefaultConnection")
            ?? "Host=localhost;Port=5432;Database=opportunity_os;Username=postgres;Password=postgres";

        services.AddDbContext<OpportunityOsDbContext>(options =>
            options.UseNpgsql(connectionString));

        services.AddScoped<IJobNormalizer, JobNormalizer>();
        services.AddScoped<IMatchEngine, HeuristicMatchEngine>();

        return services;
    }
}
