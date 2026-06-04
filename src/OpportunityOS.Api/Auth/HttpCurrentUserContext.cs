using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using OpportunityOS.Application.Auth;
using OpportunityOS.Infrastructure.Persistence;

namespace OpportunityOS.Api.Auth;

/// <summary>
/// Resolves the authenticated caller and their workspace from the current HTTP request.
/// Scoped: the workspace lookup is memoized for the lifetime of the request.
/// </summary>
public sealed class HttpCurrentUserContext : ICurrentUserContext
{
    private readonly IHttpContextAccessor _http;
    private readonly OpportunityOsDbContext _db;
    private Guid? _cachedWorkspaceId;
    private bool _workspaceResolved;

    public HttpCurrentUserContext(IHttpContextAccessor http, OpportunityOsDbContext db)
    {
        _http = http;
        _db = db;
    }

    public Guid? UserId
    {
        get
        {
            var raw = _http.HttpContext?.User?.FindFirstValue(ClaimTypes.NameIdentifier);
            return Guid.TryParse(raw, out var id) ? id : null;
        }
    }

    public bool IsAuthenticated => UserId is not null;

    public async Task<Guid?> GetWorkspaceIdAsync(CancellationToken cancellationToken)
    {
        if (_workspaceResolved) return _cachedWorkspaceId;

        if (UserId is { } userId)
            _cachedWorkspaceId = await _db.Workspaces
                .Where(w => w.UserId == userId)
                .Select(w => (Guid?)w.Id)
                .FirstOrDefaultAsync(cancellationToken);

        _workspaceResolved = true;
        return _cachedWorkspaceId;
    }
}
