using System.Net.Http.Headers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using OpportunityOS.Application.AI;
using OpportunityOS.Application.Auth;
using OpportunityOS.Application.Bacen;
using OpportunityOS.Application.Digest;
using OpportunityOS.Application.Discovery;
using OpportunityOS.Application.Import;
using OpportunityOS.Application.Matching;
using OpportunityOS.Application.Normalization;
using OpportunityOS.Application.Pipeline;
using OpportunityOS.Application.Profiles;
using OpportunityOS.Application.Projections;
using OpportunityOS.Infrastructure.Ai;
using OpportunityOS.Infrastructure.Auth;
using OpportunityOS.Infrastructure.Bacen;
using OpportunityOS.Infrastructure.Import;
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
        // Default to the non-HTTP "system" caller (Worker/design-time); the API replaces this with an
        // HTTP-backed ICurrentUserContext, so the workspace-scoped profile provider works everywhere.
        services.TryAddScoped<ICurrentUserContext, SystemUserContext>();
        services.AddScoped<ICurrentCandidateProfileProvider, EfCurrentCandidateProfileProvider>();
        services.AddScoped<ILatestOpportunityMatchProjection, EfLatestOpportunityMatchProjection>();
        services.AddSingleton<ISourceClassifierService, SourceClassifierService>();
        services.AddSingleton<ICompanyNameResolver, CompanyNameResolver>();
        services.AddSingleton<IJobFingerprintService, JobFingerprintService>();
        services.AddSingleton<IDiscoveryRankService, DiscoveryRankService>();
        services.AddSingleton<IFeedbackLearningService, FeedbackLearningService>();

        // Link validation (HEAD-check -> expire dead postings), used by API + Worker.
        services.AddHttpClient<IJobLinkValidator, JobLinkValidator>(ConfigureClient);

        // Enriches thin search snippets with the real job-page text before scoring.
        services.AddHttpClient<IJobContentEnricher, HtmlJobContentEnricher>(ConfigureClient);

        // Headless renderer for JS-heavy career pages (shared browser); HTTP fallback if absent.
        services.AddSingleton<IPageRenderer, PlaywrightPageRenderer>();

        services.AddScoped<IDiscoveryStore, EfDiscoveryStore>();
        services.AddScoped<IJobDiscoveryService, JobDiscoveryService>();

        // Firehose (massive discovery): raw search providers, query expansion, store, service.
        services.AddSingleton<QueryExpansionService>();
        var discoveryBudget = new DiscoveryBudgetOptions();
        config.GetSection("DiscoveryBudget").Bind(discoveryBudget);
        services.AddSingleton(discoveryBudget);
        services.AddSingleton<IQueryBudgetManager, QueryBudgetManager>();
        services.AddScoped<IFirehoseStore, EfFirehoseStore>();
        services.AddScoped<IFirehoseService, FirehoseService>();
        services.AddScoped<IRawCandidatePromotionService, RawCandidatePromotionService>();
        services.AddScoped<IConsultingStore, EfConsultingStore>();
        services.AddScoped<IConsultingRadarService, ConsultingRadarService>();
        services.AddScoped<IOriginalJobResolver, OriginalJobResolver>();

        services.AddScoped<IOpportunityStore, EfOpportunityStore>();
        services.AddScoped<IOpportunityPipeline, OpportunityPipeline>();

        // LinkedIn PDF profile importer (onboarding). Deterministic parser/mapper; the LLM normalizer
        // is opt-in via feature flag (default off) and strictly conservative.
        services.AddScoped<IPdfTextExtractor, PdfPigTextExtractor>();
        services.AddScoped<ILinkedInPdfProfileDetector, LinkedInPdfProfileDetector>();
        services.AddScoped<ILinkedInProfilePdfParser, LinkedInProfilePdfParser>();
        services.AddScoped<ILinkedInProfileToCandidateProfileDraftMapper, LinkedInProfileToCandidateProfileDraftMapper>();
        if (config.GetValue("FeatureFlags:EnableLinkedInPdfLlmNormalization", false))
            services.AddScoped<IProfileImportNormalizer, LlmProfileImportNormalizer>();
        else
            services.AddScoped<IProfileImportNormalizer, PassthroughProfileImportNormalizer>();

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

        if (config.GetValue("FeatureFlags:EnableSmartRecruitersProvider", true))
            services.AddHttpClient<IJobSourceProvider, SmartRecruitersJobSourceProvider>(ConfigureClient);

        if (config.GetValue("FeatureFlags:EnableAshbyProvider", true))
            services.AddHttpClient<IJobSourceProvider, AshbyJobSourceProvider>(ConfigureClient);

        if (config.GetValue("FeatureFlags:EnableGenericCrawler", true))
            services.AddHttpClient<IJobSourceProvider, GenericCareersCrawler>(ConfigureClient);

        if (config.GetValue("FeatureFlags:EnableGupyProvider", true))
            services.AddHttpClient<IJobSearchProvider, GupyJobSearchProvider>(ConfigureClient);

        services.AddHttpClient<IAtsDetector, AtsDetector>(ConfigureClient);
        services.AddScoped<ICompanyOnboardingService, CompanyOnboardingService>();

        // Heuristic website discovery (the no-CSE default and the Google fallback).
        services.AddHttpClient<CompanyWebsiteDiscoverer>(ConfigureClient);

        var googleSearch = new GoogleSearchOptions
        {
            ApiKey = config["Search:ApiKey"] ?? string.Empty,
            SearchEngineId = config["Search:SearchEngineId"] ?? string.Empty
        };
        if (googleSearch.IsConfigured)
        {
            services.AddSingleton(googleSearch);
            // Shared daily quota across every Google CSE caller (free tier = 100/day).
            services.AddSingleton(new GoogleQuotaGuard(config.GetValue("Search:DailyQueryBudget", 90)));
            // Website discovery via Google (whole-web), heuristic fallback.
            services.AddHttpClient<GoogleWebsiteDiscoverer>(ConfigureClient);
            services.AddScoped<ICompanyWebsiteDiscoverer>(sp => sp.GetRequiredService<GoogleWebsiteDiscoverer>());
            services.AddScoped<HeuristicAtsBoardFinder>();
            // ATS board finder via Google CSE (board first, then website crawl), heuristic fallback.
            services.AddHttpClient<GoogleAtsBoardFinder>(ConfigureClient);
            services.AddScoped<IAtsBoardFinder>(sp => sp.GetRequiredService<GoogleAtsBoardFinder>());
            // Open-web job search (recent .NET postings), quota-guarded.
            if (config.GetValue("FeatureFlags:EnableGoogleWebSearch", true))
                services.AddHttpClient<IJobSearchProvider, GoogleWebJobSearchProvider>(ConfigureClient);
        }
        else
        {
            services.AddScoped<ICompanyWebsiteDiscoverer>(sp => sp.GetRequiredService<CompanyWebsiteDiscoverer>());
            services.AddScoped<HeuristicAtsBoardFinder>();
            services.AddScoped<IAtsBoardFinder>(sp => sp.GetRequiredService<HeuristicAtsBoardFinder>());
        }

        // Open-web job search via Serper.dev (real Google web index — independent of the
        // Google CSE whole-web restriction). Registered on its own key, not the CSE block.
        var serper = new SerperSearchOptions
        {
            ApiKey = config["Search:SerperApiKey"] ?? string.Empty,
            MaxQueriesPerCall = config.GetValue("Search:SerperMaxQueriesPerCall", 4),
            Freshness = config.GetValue("Search:SerperFreshness", "qdr:m") ?? "qdr:m",
            DelayMsBetweenQueries = config.GetValue("Search:SerperDelayMs", 1200),
        };
        if (serper.IsConfigured && config.GetValue("FeatureFlags:EnableSerperWebSearch", true))
        {
            services.AddSingleton(serper);
            services.AddHttpClient<IJobSearchProvider, SerperWebJobSearchProvider>(ConfigureClient);
            // Firehose raw search (verbatim queries) over the same Serper key.
            services.AddHttpClient<IRawSearchProvider, SerperRawSearchProvider>(ConfigureClient);
        }

        var bacenOptions = new BacenOptions();
        config.GetSection("Bacen").Bind(bacenOptions);
        services.AddSingleton(bacenOptions);
        services.AddHttpClient<IBacenPixParticipantsCsvProvider, BacenPixParticipantsCsvProvider>(ConfigureClient);
        services.AddScoped<IBacenStore, EfBacenStore>();
        services.AddScoped<IBacenRadarService, BacenRadarService>();

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
