using OpportunityOS.Infrastructure.Providers;
using Xunit;

namespace OpportunityOS.UnitTests;

public sealed class SourceClassifierTests
{
    private readonly SourceClassifierService _svc = new();

    [Theory]
    [InlineData("https://boards.greenhouse.io/stone/jobs/123", "OfficialAts", 90, false)]
    [InlineData("https://acme.gupy.io/jobs/9", "OfficialAts", 80, false)]
    [InlineData("https://programathor.com.br/jobs-net", "JobBoard", 70, false)]
    public void OfficialAndBoards_GetHighConfidence_NoManualReview(string url, string type, int conf, bool manual)
    {
        var r = _svc.Classify(url, "Dev .NET", "snippet");
        Assert.Equal(type, r.SourceType.ToString());
        Assert.Equal(conf, r.SourceConfidenceScore);
        Assert.Equal(manual, r.RequiresManualValidation);
    }

    [Fact]
    public void LinkedIn_IsSocialIndexed_ManualReviewOnly()
    {
        var r = _svc.Classify("https://br.linkedin.com/jobs/view/123", "Dev .NET", null);
        Assert.Equal("SocialIndexed", r.SourceType.ToString());
        Assert.True(r.RequiresManualValidation);
        Assert.True(r.SourceConfidenceScore <= 30);
    }

    [Fact]
    public void Aggregator_AppearsButFlagged()
    {
        var r = _svc.Classify("https://br.indeed.com/q-net-vagas.html", "Dev .NET", "snippet");
        Assert.Equal("Aggregator", r.SourceType.ToString());
        Assert.True(r.RequiresManualValidation);
    }

    [Fact]
    public void GenericResult_WithoutCompanyHint_RequiresManualReview()
    {
        var r = _svc.Classify("https://random-site.com/page", null, null);
        Assert.True(r.RequiresManualValidation);
        Assert.True(r.SourceConfidenceScore <= 20);
    }
}
