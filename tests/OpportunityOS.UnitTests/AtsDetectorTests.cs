using OpportunityOS.Infrastructure.Providers;
using Xunit;

namespace OpportunityOS.UnitTests;

public sealed class AtsDetectorTests
{
    [Fact]
    public void Detect_Greenhouse_FromEmbeddedLink()
    {
        var html = "<a href=\"https://boards.greenhouse.io/acmepay\">Careers</a>";
        var r = AtsDetector.Detect(html);
        Assert.NotNull(r);
        Assert.Equal("Greenhouse", r!.Ats);
        Assert.Equal("acmepay", r.Token);
        Assert.True(r.ProviderSupported);
    }

    [Fact]
    public void Detect_Lever_FromEmbeddedLink()
    {
        var r = AtsDetector.Detect("...<a href='https://jobs.lever.co/acme'>jobs</a>...");
        Assert.Equal("Lever", r!.Ats);
        Assert.Equal("acme", r.Token);
    }

    [Fact]
    public void Detect_Gupy_FromSubdomain()
    {
        var r = AtsDetector.Detect("window.location='https://luminiit.gupy.io/'");
        Assert.Equal("Gupy", r!.Ats);
        Assert.Equal("luminiit", r.Token);
    }

    [Fact]
    public void Detect_Workday_FromHost_NotYetSupported()
    {
        var r = AtsDetector.Detect("<iframe src=\"https://acme.wd5.myworkdayjobs.com/careers\"></iframe>");
        Assert.Equal("Workday", r!.Ats);
        Assert.False(r.ProviderSupported); // detected but no fetch provider yet
    }

    [Fact]
    public void Detect_ReturnsNull_WhenNoAtsPresent()
    {
        Assert.Null(AtsDetector.Detect("<html><body>Sobre nós</body></html>"));
    }

    [Fact]
    public void ExtractCareersLinks_FindsAndResolvesRelative()
    {
        var html = "<a href=\"/carreiras\">Vagas</a><a href=\"https://x.com/about\">Sobre</a>";
        var links = AtsDetector.ExtractCareersLinks(html, "https://x.com/").ToList();
        Assert.Contains("https://x.com/carreiras", links);
        Assert.DoesNotContain(links, l => l.Contains("/about"));
    }
}
