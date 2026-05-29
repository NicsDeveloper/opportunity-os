using OpportunityOS.Application.Normalization;
using Xunit;

namespace OpportunityOS.UnitTests;

public sealed class JobNormalizerTests
{
    private static readonly JobNormalizer Normalizer = new();

    [Fact]
    public void Normalize_ExtractsSeniorityWorkModeSkillsAndDomains()
    {
        var result = Normalizer.Normalize(
            title: "Senior Backend Engineer",
            location: "Remote",
            descriptionText: "Build payment and PIX platforms with .NET, C#, Kafka and AWS. Remote position.",
            declaredLanguage: null);

        Assert.Equal("Senior", result.Seniority);
        Assert.Equal("Remote", result.WorkMode);
        Assert.Contains(".NET", result.Skills);
        Assert.Contains("Kafka", result.Skills);
        Assert.Contains("AWS", result.Skills);
        Assert.Contains("Payments", result.Domains);
        Assert.Contains("PIX", result.Domains);
    }

    [Fact]
    public void Normalize_DetectsPortugueseSeniorityAndMode()
    {
        var result = Normalizer.Normalize(
            title: "Desenvolvedor Backend Pleno",
            location: "Híbrido - São Paulo",
            descriptionText: "Vaga para desenvolvedor com experiência em pagamentos e mensageria.",
            declaredLanguage: null);

        Assert.Equal("MidLevel", result.Seniority);
        Assert.Equal("Hybrid", result.WorkMode);
        Assert.Equal("pt-BR", result.Language);
    }

    [Fact]
    public void Normalize_DeclaredLanguageTakesPrecedence()
    {
        var result = Normalizer.Normalize("Engineer", "Remote", "We are hiring developers.", "en");
        Assert.Equal("en", result.Language);
    }
}
