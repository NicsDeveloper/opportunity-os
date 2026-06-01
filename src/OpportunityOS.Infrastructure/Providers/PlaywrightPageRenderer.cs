using Microsoft.Extensions.Logging;
using Microsoft.Playwright;
using OpportunityOS.Application.Discovery;

namespace OpportunityOS.Infrastructure.Providers;

/// <summary>
/// Renders JS-heavy pages with a headless Chromium (Playwright). Lazily launches a
/// single shared browser. If the browser binaries aren't installed (run
/// `pwsh bin/Debug/net10.0/playwright.ps1 install chromium`), rendering is disabled
/// gracefully and callers fall back to plain HTTP.
/// </summary>
public sealed class PlaywrightPageRenderer : IPageRenderer, IAsyncDisposable
{
    private readonly ILogger<PlaywrightPageRenderer> _logger;
    private readonly SemaphoreSlim _initLock = new(1, 1);
    private IPlaywright? _pw;
    private IBrowser? _browser;
    private bool _disabled;

    public PlaywrightPageRenderer(ILogger<PlaywrightPageRenderer> logger) => _logger = logger;

    public bool IsAvailable => !_disabled;

    public async Task<string?> RenderAsync(string url, CancellationToken ct)
    {
        var browser = await EnsureBrowserAsync();
        if (browser is null) return null;

        IPage? page = null;
        try
        {
            page = await browser.NewPageAsync();
            await page.GotoAsync(url, new PageGotoOptions { Timeout = 20000, WaitUntil = WaitUntilState.NetworkIdle });
            return await page.ContentAsync();
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Playwright render failed for {Url}", url);
            return null;
        }
        finally
        {
            if (page is not null) await page.CloseAsync();
        }
    }

    private async Task<IBrowser?> EnsureBrowserAsync()
    {
        if (_disabled) return null;
        if (_browser is not null) return _browser;
        await _initLock.WaitAsync();
        try
        {
            if (_browser is not null) return _browser;
            if (_disabled) return null;
            _pw = await Playwright.CreateAsync();
            _browser = await _pw.Chromium.LaunchAsync(new BrowserTypeLaunchOptions { Headless = true });
            _logger.LogInformation("Playwright Chromium ready (JS rendering enabled).");
            return _browser;
        }
        catch (Exception ex)
        {
            _disabled = true;
            _logger.LogWarning("Playwright unavailable ({Msg}); falling back to HTTP-only crawl. " +
                "Install with: pwsh src/OpportunityOS.Api/bin/Debug/net10.0/playwright.ps1 install chromium", ex.Message);
            return null;
        }
        finally { _initLock.Release(); }
    }

    public async ValueTask DisposeAsync()
    {
        if (_browser is not null) await _browser.DisposeAsync();
        _pw?.Dispose();
    }
}
