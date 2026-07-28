using Microsoft.EntityFrameworkCore;
using OpportunityOS.Application.Auth;
using OpportunityOS.Domain.Entities;
using OpportunityOS.Infrastructure.Persistence;
using Xunit;

namespace OpportunityOS.UnitTests;

/// <summary>
/// The workspace-scoped <see cref="EfCurrentCandidateProfileProvider"/> is the isolation choke point:
/// it resolves only profiles in the caller's workspace and rejects (throws) an explicit foreign id.
/// </summary>
public sealed class WorkspaceProfileProviderTests
{
    private static OpportunityOsDbContext NewDb() =>
        new(new DbContextOptionsBuilder<OpportunityOsDbContext>()
            .UseInMemoryDatabase("wsp-" + Guid.NewGuid()).Options);

    private sealed class FakeUser : ICurrentUserContext
    {
        private readonly Guid? _workspaceId;
        public FakeUser(Guid? workspaceId) => _workspaceId = workspaceId;
        public Guid? UserId => _workspaceId is null ? null : Guid.NewGuid();
        public bool IsAuthenticated => _workspaceId is not null;
        public Task<Guid?> GetWorkspaceIdAsync(CancellationToken ct) => Task.FromResult(_workspaceId);
    }

    private static CandidateProfile NewProfile(Guid workspaceId, string label, bool isDefault = false)
    {
        var p = new CandidateProfile(label, "headline", "summary", "loc", "Pleno", "pt-BR");
        p.AssignWorkspace(workspaceId);
        if (isDefault) p.SetDefault(true);
        return p;
    }

    [Fact]
    public async Task ResolveId_NoExplicitId_ReturnsWorkspaceDefault()
    {
        var ws = Guid.NewGuid();
        await using var db = NewDb();
        var def = NewProfile(ws, "Default", isDefault: true);
        db.CandidateProfiles.AddRange(NewProfile(ws, "Other"), def);
        // A profile in ANOTHER workspace must never be picked.
        db.CandidateProfiles.Add(NewProfile(Guid.NewGuid(), "Foreign default", isDefault: true));
        await db.SaveChangesAsync();

        var provider = new EfCurrentCandidateProfileProvider(db, new FakeUser(ws));
        var resolved = await provider.ResolveIdAsync(null, default);

        Assert.Equal(def.Id, resolved);
    }

    [Fact]
    public async Task ResolveId_OwnedExplicitId_IsReturned()
    {
        var ws = Guid.NewGuid();
        await using var db = NewDb();
        var mine = NewProfile(ws, "Mine");
        db.CandidateProfiles.Add(mine);
        await db.SaveChangesAsync();

        var provider = new EfCurrentCandidateProfileProvider(db, new FakeUser(ws));
        Assert.Equal(mine.Id, await provider.ResolveIdAsync(mine.Id, default));
    }

    [Fact]
    public async Task ResolveId_ForeignExplicitId_Throws()
    {
        var ws = Guid.NewGuid();
        await using var db = NewDb();
        var foreign = NewProfile(Guid.NewGuid(), "Foreign");
        db.CandidateProfiles.Add(foreign);
        await db.SaveChangesAsync();

        var provider = new EfCurrentCandidateProfileProvider(db, new FakeUser(ws));
        await Assert.ThrowsAsync<ForbiddenProfileAccessException>(
            () => provider.ResolveIdAsync(foreign.Id, default));
    }

    [Fact]
    public async Task Get_ForeignExplicitId_Throws()
    {
        var ws = Guid.NewGuid();
        await using var db = NewDb();
        var foreign = NewProfile(Guid.NewGuid(), "Foreign");
        db.CandidateProfiles.Add(foreign);
        await db.SaveChangesAsync();

        var provider = new EfCurrentCandidateProfileProvider(db, new FakeUser(ws));
        await Assert.ThrowsAsync<ForbiddenProfileAccessException>(
            () => provider.GetAsync(foreign.Id, default));
    }

    [Fact]
    public async Task NoWorkspace_NullId_ReturnsNull_ButExplicitId_Throws()
    {
        await using var db = NewDb();
        db.CandidateProfiles.Add(NewProfile(Guid.NewGuid(), "Someone else's"));
        await db.SaveChangesAsync();

        var anonymous = new EfCurrentCandidateProfileProvider(db, new FakeUser(null));
        Assert.Null(await anonymous.ResolveIdAsync(null, default));
        await Assert.ThrowsAsync<ForbiddenProfileAccessException>(
            () => anonymous.ResolveIdAsync(Guid.NewGuid(), default));
    }
}
