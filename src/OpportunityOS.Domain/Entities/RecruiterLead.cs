using OpportunityOS.Domain.Enums;

namespace OpportunityOS.Domain.Entities;

/// <summary>
/// A recruiter contact added manually by the user. The system never scrapes
/// LinkedIn — it only stores links the user provides.
/// </summary>
public sealed class RecruiterLead
{
    public Guid Id { get; private set; }
    public Guid CompanyId { get; private set; }
    public string FullName { get; private set; } = string.Empty;
    public string? RoleTitle { get; private set; }
    public string? LinkedInUrl { get; private set; }
    public string? Email { get; private set; }
    public RecruiterLeadSource Source { get; private set; }
    public string? Notes { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }

    private RecruiterLead() { }

    public RecruiterLead(
        Guid companyId, string fullName, string? roleTitle, string? linkedInUrl,
        string? email, RecruiterLeadSource source, string? notes)
    {
        Id = Guid.NewGuid();
        CompanyId = companyId;
        FullName = fullName;
        RoleTitle = roleTitle;
        LinkedInUrl = linkedInUrl;
        Email = email;
        Source = source;
        Notes = notes;
        CreatedAtUtc = DateTime.UtcNow;
    }

    public void Update(string fullName, string? roleTitle, string? linkedInUrl, string? email, string? notes)
    {
        FullName = fullName;
        RoleTitle = roleTitle;
        LinkedInUrl = linkedInUrl;
        Email = email;
        Notes = notes;
    }
}
