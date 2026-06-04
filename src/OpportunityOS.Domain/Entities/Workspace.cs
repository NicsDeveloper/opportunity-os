namespace OpportunityOS.Domain.Entities;

/// <summary>
/// A user's private container for their personal data (candidate profiles, feedback, drafts,
/// applications). In the Auth Workspace MVP each user owns exactly one workspace. Holds the
/// owning user's id as a plain <see cref="Guid"/> (no navigation) so the Domain stays free of the
/// ASP.NET Identity dependency that <c>AppUser</c> carries.
/// </summary>
public sealed class Workspace
{
    public Guid Id { get; private set; }
    public Guid UserId { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime UpdatedAtUtc { get; private set; }

    // Required by EF Core materialization.
    private Workspace() { }

    public Workspace(Guid userId, string name)
    {
        Id = Guid.NewGuid();
        UserId = userId;
        Name = string.IsNullOrWhiteSpace(name) ? "Meu workspace" : name;
        CreatedAtUtc = DateTime.UtcNow;
        UpdatedAtUtc = CreatedAtUtc;
    }

    public void Rename(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return;
        Name = name;
        UpdatedAtUtc = DateTime.UtcNow;
    }
}
