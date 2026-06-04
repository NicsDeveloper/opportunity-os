using OpportunityOS.Domain.Enums;

namespace OpportunityOS.Domain.Entities;

/// <summary>
/// A candidate IT consultancy / outsourcing / software house discovered automatically — the
/// kind of company that lives off hiring .NET/C# devs. Promoted to a Company when confident.
/// </summary>
public sealed class ConsultingCompanyCandidate
{
    public Guid Id { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string? WebsiteUrl { get; private set; }
    public string? LinkedInCompanyUrl { get; private set; }
    public string Country { get; private set; } = "Brazil";
    public string Source { get; private set; } = string.Empty;
    public List<string> Signals { get; private set; } = new();
    public int ConsultingConfidenceScore { get; private set; }
    public ConsultingCompanyCandidateStatus Status { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime? PromotedAtUtc { get; private set; }

    private ConsultingCompanyCandidate() { }

    public ConsultingCompanyCandidate(
        string name, string? websiteUrl, string source, IEnumerable<string> signals,
        int confidence, string country = "Brazil", string? linkedInCompanyUrl = null)
    {
        Id = Guid.NewGuid();
        Name = name.Trim();
        WebsiteUrl = websiteUrl;
        LinkedInCompanyUrl = linkedInCompanyUrl;
        Country = country;
        Source = source;
        Signals = signals?.Distinct().ToList() ?? new();
        ConsultingConfidenceScore = Math.Clamp(confidence, 0, 100);
        Status = ConsultingCompanyCandidateStatus.Candidate;
        CreatedAtUtc = DateTime.UtcNow;
    }

    public void Reinforce(IEnumerable<string> extraSignals, int confidenceDelta)
    {
        foreach (var s in extraSignals) if (!Signals.Contains(s)) Signals.Add(s);
        ConsultingConfidenceScore = Math.Clamp(ConsultingConfidenceScore + confidenceDelta, 0, 100);
    }

    public void MarkPromoted() { Status = ConsultingCompanyCandidateStatus.PromotedToCompany; PromotedAtUtc = DateTime.UtcNow; }
    public void SetStatus(ConsultingCompanyCandidateStatus status) => Status = status;
    public void SetWebsite(string url) => WebsiteUrl = url;
}
