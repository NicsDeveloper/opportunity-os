namespace OpportunityOS.Application.Discovery;

/// <summary>
/// Renders a page's HTML after executing JavaScript (for SPA career boards).
/// Implemented in Infrastructure with Playwright; returns null when rendering
/// isn't available (e.g. browser not installed) so callers fall back to plain HTTP.
/// </summary>
public interface IPageRenderer
{
    bool IsAvailable { get; }
    Task<string?> RenderAsync(string url, CancellationToken cancellationToken);
}
