using System.Net.Http.Headers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OpportunityOS.Application.AI;
using OpportunityOS.Application.Discovery;
using OpportunityOS.Application.Matching;
using OpportunityOS.Application.Normalization;
using OpportunityOS.Infrastructure.Ai;
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

        AddAiCopilot(services, config);

        return services;
    }

    private static void AddAiCopilot(IServiceCollection services, IConfiguration config)
    {
        var llmEnabled = config.GetValue("FeatureFlags:EnableLlmAnalysis", true);
        var apiKey = config["OpenAI:ApiKey"];

        if (!llmEnabled)
        {
            // Disabled: provider reports not-configured, so services use heuristic fallback.
            services.AddSingleton<ILlmProvider>(_ => new FakeLlmProvider(isConfigured: false));
        }
        else if (!string.IsNullOrWhiteSpace(apiKey))
        {
            var options = new OpenAiOptions
            {
                ApiKey = apiKey!,
                Model = config["OpenAI:Model"] ?? "gpt-4.1-mini"
            };
            services.AddSingleton(options);
            services.AddHttpClient<ILlmProvider, OpenAiLlmProvider>(c => c.Timeout = TimeSpan.FromSeconds(60));
        }
        else
        {
            // No API key: deterministic fake provider (still returns valid JSON).
            services.AddSingleton<ILlmProvider>(_ => new FakeLlmProvider());
        }

        services.AddScoped<IPromptExecutionLogStore, EfPromptExecutionLogStore>();
        services.AddScoped<IJobUnderstandingService, JobUnderstandingService>();
        services.AddScoped<ICandidateFitAnalysisService, CandidateFitAnalysisService>();
        services.AddScoped<IOutreachDraftService, OutreachDraftService>();
        services.AddScoped<ICvTailoringSuggestionService, CvTailoringSuggestionService>();
        services.AddScoped<ICareerInsightService, CareerInsightService>();
    }

    private static void ConfigureClient(HttpClient client)
    {
        client.Timeout = TimeSpan.FromSeconds(20);
        client.DefaultRequestHeaders.UserAgent.ParseAdd(UserAgent);
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
    }
}
