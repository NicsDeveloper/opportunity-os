using OpportunityOS.Application.Digest;
using OpportunityOS.Domain.Entities;

namespace OpportunityOS.UnitTests.Fakes;

public sealed class FakeDigestStore : IDigestStore
{
    private readonly List<OpportunityDigestItem> _items;
    public List<ExecutionRun> Runs { get; } = new();

    public FakeDigestStore(IEnumerable<OpportunityDigestItem>? items = null) =>
        _items = items?.ToList() ?? new();

    public Task<IReadOnlyList<OpportunityDigestItem>> GetDigestItemsAsync(int minScore, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<OpportunityDigestItem>>(
            _items.Where(i => i.OverallScore >= minScore).ToList());

    public Task SaveExecutionRunAsync(ExecutionRun run, CancellationToken ct)
    {
        Runs.Add(run);
        return Task.CompletedTask;
    }
}

public sealed class FakeEmailSender : IEmailSender
{
    private readonly bool _throw;
    public FakeEmailSender(bool isConfigured = true, bool throwOnSend = false)
    {
        IsConfigured = isConfigured;
        _throw = throwOnSend;
    }

    public bool IsConfigured { get; }
    public int SentCount { get; private set; }
    public string? LastSubject { get; private set; }
    public string? LastHtml { get; private set; }

    public Task SendAsync(string subject, string htmlBody, CancellationToken ct)
    {
        if (_throw) throw new InvalidOperationException("smtp boom");
        SentCount++;
        LastSubject = subject;
        LastHtml = htmlBody;
        return Task.CompletedTask;
    }

    public static OpportunityDigestItem Item(int score, string company = "Acme", string rec = "Prioritize") =>
        new(company, "Senior Backend (.NET)", "https://example.com/j", score, rec,
            "rationale", "linkedin msg", "cover", "cv notes",
            new List<string> { "Stack .NET" }, new List<string> { "inglês" });
}
