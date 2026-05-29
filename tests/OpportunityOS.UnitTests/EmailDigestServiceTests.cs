using Microsoft.Extensions.Logging.Abstractions;
using OpportunityOS.Application.Digest;
using OpportunityOS.Domain.Enums;
using OpportunityOS.UnitTests.Fakes;
using Xunit;

namespace OpportunityOS.UnitTests;

public sealed class EmailDigestServiceTests
{
    private static EmailDigestService Build(FakeDigestStore store, FakeEmailSender sender) =>
        new(store, sender, NullLogger<EmailDigestService>.Instance);

    [Fact]
    public async Task Send_WithNoRelevantOpportunities_RecordsRun_DoesNotSend()
    {
        var store = new FakeDigestStore(); // empty
        var sender = new FakeEmailSender();

        var result = await Build(store, sender).SendDailyDigestAsync(60, CancellationToken.None);

        Assert.False(result.Sent);
        Assert.Equal(0, sender.SentCount);
        var run = Assert.Single(store.Runs);
        Assert.Equal(ExecutionRunStatus.Succeeded, run.Status); // recorded without error
    }

    [Fact]
    public async Task Send_WithItems_AndConfiguredSender_SendsAndRecordsSuccess()
    {
        var store = new FakeDigestStore(new[] { FakeEmailSender.Item(90, "Nubank"), FakeEmailSender.Item(72) });
        var sender = new FakeEmailSender(isConfigured: true);

        var result = await Build(store, sender).SendDailyDigestAsync(60, CancellationToken.None);

        Assert.True(result.Sent);
        Assert.Equal(2, result.ItemCount);
        Assert.Equal(1, sender.SentCount);
        Assert.Contains("Nubank", sender.LastHtml);
        Assert.Equal(ExecutionRunStatus.Succeeded, store.Runs.Single().Status);
    }

    [Fact]
    public async Task Send_WhenSenderNotConfigured_DoesNotSend()
    {
        var store = new FakeDigestStore(new[] { FakeEmailSender.Item(90) });
        var sender = new FakeEmailSender(isConfigured: false);

        var result = await Build(store, sender).SendDailyDigestAsync(60, CancellationToken.None);

        Assert.False(result.Sent);
        Assert.Equal(0, sender.SentCount);
        Assert.Contains("SMTP", result.Reason);
    }

    [Fact]
    public async Task Send_WhenSenderThrows_RecordsFailure_DoesNotBubble()
    {
        var store = new FakeDigestStore(new[] { FakeEmailSender.Item(90) });
        var sender = new FakeEmailSender(isConfigured: true, throwOnSend: true);

        var result = await Build(store, sender).SendDailyDigestAsync(60, CancellationToken.None);

        Assert.False(result.Sent);
        Assert.Equal(ExecutionRunStatus.Failed, store.Runs.Single().Status);
    }

    [Fact]
    public async Task Preview_DoesNotSend_AndIncludesCounts()
    {
        var store = new FakeDigestStore(new[]
        {
            FakeEmailSender.Item(95, "Nubank", "Strategic"),
            FakeEmailSender.Item(80, "Stone", "Prioritize")
        });
        var sender = new FakeEmailSender();

        var preview = await Build(store, sender).BuildPreviewAsync(60, CancellationToken.None);

        Assert.Equal(2, preview.Total);
        Assert.Equal(1, preview.StrategicCount);
        Assert.Equal(1, preview.PrioritizeCount);
        Assert.Contains("Opportunity OS", preview.Markdown);
        Assert.Equal(0, sender.SentCount); // preview never sends
    }

    [Fact]
    public void Renderer_WithNoItems_ProducesEmptyNotice()
    {
        var preview = DigestRenderer.Render(Array.Empty<OpportunityDigestItem>());
        Assert.Equal(0, preview.Total);
        Assert.Contains("Nenhuma oportunidade", preview.Markdown);
    }

    [Fact]
    public async Task Preview_GreetsTheRecipientByFirstName_InSecondPerson()
    {
        var store = new FakeDigestStore(new[] { FakeEmailSender.Item(90) }) { CandidateName = "Nícolas Serrano" };
        var preview = await Build(store, new FakeEmailSender()).BuildPreviewAsync(60, CancellationToken.None);

        // Markdown keeps accents raw; HTML encodes them but still greets in 2nd person.
        Assert.Contains("Olá, Nícolas!", preview.Markdown);
        Assert.Contains("suas oportunidades", preview.Html);
        Assert.Contains("suas oportunidades", preview.Markdown);
    }
}
