using OpportunityOS.Application.Import;
using Xunit;

namespace OpportunityOS.UnitTests;

/// <summary>Detector + deterministic parser + draft mapper for the LinkedIn PDF importer.</summary>
public sealed class LinkedInImportTests
{
    private static readonly LinkedInPdfProfileDetector Detector = new();
    private static readonly LinkedInProfilePdfParser Parser = new();
    private static readonly LinkedInProfileToCandidateProfileDraftMapper Mapper = new();

    private static Contracts.LinkedInProfileImportDto Parse()
    {
        var detection = Detector.Detect(LinkedInPdfFixture.Text);
        return Parser.Parse(LinkedInPdfFixture.Text, detection, out _);
    }

    [Fact] // #1
    public void Detector_AcceptsLinkedInPdf()
    {
        var r = Detector.Detect(LinkedInPdfFixture.Text);
        Assert.True(r.IsLikelyLinkedInProfilePdf);
        Assert.True(r.Confidence >= 70, $"confidence was {r.Confidence}");
    }

    [Fact] // #2
    public void Detector_RejectsNonLinkedInPdf()
    {
        var r = Detector.Detect(LinkedInPdfFixture.NotLinkedIn);
        Assert.False(r.IsLikelyLinkedInProfilePdf);
        Assert.True(r.Confidence < 50);
    }

    [Fact] // #3, #4
    public void Parser_ExtractsEmailAndLinkedInUrl()
    {
        var p = Parse();
        Assert.Equal("alex.backend@example.com", p.Email);
        Assert.Contains("linkedin.com/in/alex-backend-example", p.LinkedInUrl);
    }

    [Fact] // #5
    public void Parser_ExtractsNameHeadlineLocation()
    {
        var p = Parse();
        Assert.Equal("Alex Backend Developer", p.FullName);
        Assert.Equal("Desenvolvedor .Net | Node | AWS", p.Headline);
        Assert.Equal("São Paulo, Brasil", p.Location);
    }

    [Fact] // #6
    public void Parser_ExtractsSummary()
    {
        var p = Parse();
        Assert.Contains("microsserviços", p.Summary, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Itaú", p.Summary); // summary must stop before Experiência
    }

    [Fact] // #7
    public void Parser_ExtractsSkills()
    {
        var p = Parse();
        Assert.Contains(".NET Core", p.Skills);
        Assert.Contains("xUnit", p.Skills);
        Assert.Contains("RabbitMQ", p.Skills);
    }

    [Fact] // #8
    public void Parser_ExtractsExperiencesWithCompanyTitlePeriod()
    {
        var p = Parse();
        Assert.Equal(2, p.Experiences.Count);
        var itau = p.Experiences[0];
        Assert.Equal("Itaú Unibanco", itau.Company);
        Assert.Equal("Desenvolvedor .Net Sênior", itau.Title);
        Assert.False(string.IsNullOrWhiteSpace(itau.DurationText));
        Assert.Equal("Vindi", p.Experiences[1].Company);
    }

    [Fact] // #9
    public void Parser_ExtractsEducation()
    {
        var p = Parse();
        Assert.Contains(p.Education, e => e.Institution == "Estácio");
    }

    [Fact] // #10
    public void Mapper_CoreSkillsAreCorrect()
    {
        var draft = Mapper.Map(Parse());
        Assert.Contains(".NET", draft.CoreSkills);
        Assert.Contains("C#", draft.CoreSkills);
    }

    [Fact] // #11
    public void Mapper_InfersSeniorityForFivePlusYears()
    {
        var draft = Mapper.Map(Parse());
        Assert.Equal("Pleno/Sênior", draft.Seniority);
    }

    [Fact] // #12
    public void Mapper_InfersFintechDomains()
    {
        var draft = Mapper.Map(Parse());
        Assert.Contains("Pagamentos", draft.Domains);
        Assert.Contains("Open Finance", draft.Domains);
        Assert.Contains("Banking", draft.Domains);
    }

    [Fact] // #13 — the credibility rule
    public void Mapper_DoesNotInferJavaFromJavaScript()
    {
        var draft = Mapper.Map(Parse());
        Assert.DoesNotContain("Java", draft.CoreSkills);
        Assert.DoesNotContain("Java", draft.SecondarySkills);
        Assert.Contains("JavaScript", draft.CoreSkills.Concat(draft.SecondarySkills));
    }

    [Fact] // #14
    public void Mapper_DotNetCoreMapsToDotNetFamily()
    {
        var draft = Mapper.Map(Parse());
        Assert.Contains(".NET", draft.CoreSkills); // ".NET Core" alias → .NET (a core family of this profile)
    }

    [Fact] // company with abbreviation period must NOT be flagged as prose
    public void Parser_KeepsCompanyEndingInAbbreviation()
    {
        var text =
            """
            Alex Backend Developer
            Desenvolvedor .Net | Node | AWS
            Brasil
            Resumo
            Backend com .NET e AWS por vários anos em pagamentos e Open Finance.
            Experiência
            americanas s.a.
            Caixa
            março de 2016 - março de 2016 (1 mês)
            Atendimento ao cliente no caixa.
            Formação acadêmica
            Estácio
            Contato
            a@b.com
            www.linkedin.com/in/alex
            """;
        var p = Parser.Parse(text, Detector.Detect(text), out _);
        var exp = Assert.Single(p.Experiences);
        Assert.Equal("americanas s.a.", exp.Company);
        Assert.Equal("Caixa", exp.Title);
    }

    [Fact] // #15
    public void Mapper_AwsServicesMapToAwsAndCloud()
    {
        var draft = Mapper.Map(Parse());
        Assert.Contains("AWS", draft.CoreSkills);
        Assert.Contains("Cloud", draft.Domains);
    }
}
