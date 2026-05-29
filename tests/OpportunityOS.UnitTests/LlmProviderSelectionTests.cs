using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OpportunityOS.Application.AI;
using OpportunityOS.Infrastructure;
using OpportunityOS.Infrastructure.Ai;
using Xunit;

namespace OpportunityOS.UnitTests;

public sealed class LlmProviderSelectionTests
{
    private static ILlmProvider Resolve(Dictionary<string, string?> settings)
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(settings).Build();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddInfrastructure(config);
        return services.BuildServiceProvider().GetRequiredService<ILlmProvider>();
    }

    [Fact]
    public void NoApiKey_ResolvesConfiguredFake()
    {
        var p = Resolve(new());
        Assert.IsType<FakeLlmProvider>(p);
        Assert.True(p.IsConfigured); // returns canned JSON
    }

    [Fact]
    public void LlmDisabled_ResolvesNotConfiguredFake()
    {
        var p = Resolve(new() { ["FeatureFlags:EnableLlmAnalysis"] = "false" });
        Assert.IsType<FakeLlmProvider>(p);
        Assert.False(p.IsConfigured); // forces heuristic fallback
    }

    [Fact]
    public void AnthropicKeyPresent_ResolvesAnthropicProvider()
    {
        var p = Resolve(new() { ["Anthropic:ApiKey"] = "sk-ant-test" });
        Assert.IsType<AnthropicLlmProvider>(p);
        Assert.Equal("claude-sonnet-4-6", p.ModelName);
    }

    [Fact]
    public void OpenAiKeyPresent_ResolvesOpenAiProvider()
    {
        var p = Resolve(new() { ["OpenAI:ApiKey"] = "sk-openai-test" });
        Assert.IsType<OpenAiLlmProvider>(p);
    }

    [Fact]
    public void ExplicitPreference_OverridesAutoDetection()
    {
        // Anthropic key present, but OpenAI explicitly requested.
        var p = Resolve(new()
        {
            ["Anthropic:ApiKey"] = "sk-ant-test",
            ["OpenAI:ApiKey"] = "sk-openai-test",
            ["Llm:Provider"] = "OpenAI"
        });
        Assert.IsType<OpenAiLlmProvider>(p);
    }
}
