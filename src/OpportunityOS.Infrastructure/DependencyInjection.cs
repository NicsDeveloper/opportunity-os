using System.Net.Http.Headers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OpportunityOS.Application.AI;
using OpportunityOS.Application.Digest;
using OpportunityOS.Application.Discovery;
using OpportunityOS.Application.Matching;
using OpportunityOS.Application.Normalization;
using OpportunityOS.Application.Pipeline;
using OpportunityOS.Infrastructure.Ai;
using OpportunityOS.Infrastructure.Email;
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

        services.AddScoped<IOpportunityStore, EfOpportunityStore>();
        services.AddScoped<IOpportunityPipeline, OpportunityPipeline>();

        var emailOptions = new EmailOptions();
        config.GetSection("Email").Bind(emailOptions);
        services.AddSingleton(emailOptions);
        services.AddScoped<IEmailSender, SmtpEmailSender>();
        services.AddScoped<IDigestStore, EfDigestStore>();
        services.AddScoped<IEmailDigestService, EmailDigestService>();

        if (config.GetValue("FeatureFlags:EnableGreenhouseProvider", true))
            services.AddHttpClient<IJobSourceProvider, GreenhouseJobSourceProvider>(ConfigureClient);

        if (config.GetValue("FeatureFlags:EnableLeverProvider", true))
            services.AddHttpClient<IJobSourceProvider, LeverJobSourceProvider>(ConfigureClient);

        if (config.GetValue("FeatureFlags:EnableGupyProvider", true))
            services.AddHttpClient<IJobSearchProvider, GupyJobSearchProvider>(ConfigureClient);

        AddAiCopilot(services, config);

        return services;
    }

    private static void AddAiCopilot(IServiceCollection services, IConfiguration config)
    {
        RegisterLlmProvider(services, config);

        services.AddScoped<IPromptExecutionLogStore, EfPromptExecutionLogStore>();
        services.AddScoped<IJobUnderstandingService, JobUnderstandingService>();
        services.AddScoped<ICandidateFitAnalysisService, CandidateFitAnalysisService>();
        services.AddScoped<IOutreachDraftService, OutreachDraftService>();
        services.AddScoped<ICvTailoringSuggestionService, CvTailoringSuggestionService>();
        services.AddScoped<ICareerInsightService, CareerInsightService>();
    }

    /// <summary>
    /// Selects the LLM provider. Disabled -> not-configured fake (forces heuristic
    /// fallback). Otherwise honours an explicit <c>Llm:Provider</c> (Anthropic|OpenAI|Fake)
    /// or auto-detects by the first available API key (Anthropic, then OpenAI),
    /// falling back to the deterministic fake when no key is present.
    /// </summary>
    private static void RegisterLlmProvider(IServiceCollection services, IConfiguration config)
    {
        if (!config.GetValue("FeatureFlags:EnableLlmAnalysis", true))
        {
            services.AddSingleton<ILlmProvider>(_ => new FakeLlmProvider(isConfigured: false));
            return;
        }

        var anthropicKey = config["Anthropic:ApiKey"];
        var openAiKey = config["OpenAI:ApiKey"];
        var preference = (config["Llm:Provider"] ?? "auto").Trim().ToLowerInvariant();

        var useAnthropic = preference == "anthropic"
            || (preference == "auto" && !string.IsNullOrWhiteSpace(anthropicKey));
        var useOpenAi = preference == "openai"
            || (preference == "auto" && string.IsNullOrWhiteSpace(anthropicKey) && !string.IsNullOrWhiteSpace(openAiKey));

        if (useAnthropic)
        {
            services.AddSingleton(new AnthropicOptions
            {
                ApiKey = anthropicKey ?? string.Empty,
                Model = config["Anthropic:Model"] ?? "claude-sonnet-4-6"
            });
            services.AddHttpClient<ILlmProvider, AnthropicLlmProvider>(c => c.Timeout = TimeSpan.FromSeconds(60));
        }
        else if (useOpenAi)
        {
            services.AddSingleton(new OpenAiOptions
            {
                ApiKey = openAiKey ?? string.Empty,
                Model = config["OpenAI:Model"] ?? "gpt-4.1-mini"
            });
            services.AddHttpClient<ILlmProvider, OpenAiLlmProvider>(c => c.Timeout = TimeSpan.FromSeconds(60));
        }
        else
        {
            // No key (or Llm:Provider=Fake): deterministic fake that still returns valid JSON.
            services.AddSingleton<ILlmProvider>(_ => new FakeLlmProvider());
        }
    }

    private static void ConfigureClient(HttpClient client)
    {
        client.Timeout = TimeSpan.FromSeconds(20);
        client.DefaultRequestHeaders.UserAgent.ParseAdd(UserAgent);
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
    }
}
