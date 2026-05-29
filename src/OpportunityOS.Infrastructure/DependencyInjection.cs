using System.Net.Http.Headers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OpportunityOS.Application.Discovery;
using OpportunityOS.Application.Matching;
using OpportunityOS.Application.Normalization;
using OpportunityOS.Infrastructure.Persistence;
using OpportunityOS.Infrastructure.Providers;

namespace OpportunityOS.Infrastructure;

public static class DependencyInjection
{
    // Clear, identifying User-Agent (spec §20: providers must identify themselves).
    private const string UserAgent = "OpportunityOS/1.0 (+https://github.com/; personal job radar)";

    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration config)
    {
        var connectionString = config.GetConnectionString("DefaultConnection")
            ?? "Host=localhost;Port=5432;Database=opportunity_os;Username=postgres;Password=postgres";

        services.AddDbContext<OpportunityOsDbContext>(options =>
            options.UseNpgsql(connectionString));

        services.AddScoped<IJobNormalizer, JobNormalizer>();
        services.AddScoped<IMatchEngine, HeuristicMatchEngine>();

        services.AddScoped<IDiscoveryStore, EfDiscoveryStore>();
        services.AddScoped<IJobDiscoveryService, JobDiscoveryService>();

        if (config.GetValue("FeatureFlags:EnableGreenhouseProvider", true))
            services.AddHttpClient<IJobSourceProvider, GreenhouseJobSourceProvider>(ConfigureClient);

        if (config.GetValue("FeatureFlags:EnableLeverProvider", true))
            services.AddHttpClient<IJobSourceProvider, LeverJobSourceProvider>(ConfigureClient);

        return services;
    }

    private static void ConfigureClient(HttpClient client)
    {
        client.Timeout = TimeSpan.FromSeconds(20);
        client.DefaultRequestHeaders.UserAgent.ParseAdd(UserAgent);
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
    }
}
