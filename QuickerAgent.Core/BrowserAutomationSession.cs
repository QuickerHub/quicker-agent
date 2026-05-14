using Microsoft.Extensions.Logging;
using Microsoft.Playwright;

namespace QuickerAgent.Core;

/// <summary>
/// Connects to an existing Chromium over CDP and performs Quicker web login on a page.
/// </summary>
public sealed class BrowserAutomationSession
{
    private readonly QuickerWebLoginService _loginService;
    private readonly ILogger<BrowserAutomationSession> _logger;

    public BrowserAutomationSession(
        QuickerWebLoginService loginService,
        ILogger<BrowserAutomationSession> logger)
    {
        _loginService = loginService;
        _logger = logger;
    }

    public async Task<bool> ConnectAndLoginAsync(
        string cdpUrl,
        string email,
        string password,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(cdpUrl);

        using var playwright = await Playwright.CreateAsync().ConfigureAwait(false);
        await using var browser = await playwright.Chromium.ConnectOverCDPAsync(cdpUrl).ConfigureAwait(false);

        var context = browser.Contexts.Count > 0
            ? browser.Contexts[0]
            : await browser.NewContextAsync().ConfigureAwait(false);

        var page = context.Pages.Count > 0
            ? context.Pages[0]
            : await context.NewPageAsync().ConfigureAwait(false);

        page.SetDefaultTimeout(30_000);
        page.SetDefaultNavigationTimeout(30_000);

        _logger.LogInformation("Connected over CDP; running login flow.");
        return await _loginService.LoginAsync(page, email, password, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Verifies CDP connectivity without performing login.
    /// </summary>
    public async Task<bool> TryConnectAsync(string cdpUrl, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(cdpUrl);

        try
        {
            using var playwright = await Playwright.CreateAsync().ConfigureAwait(false);
            await using var browser = await playwright.Chromium
                .ConnectOverCDPAsync(cdpUrl, new BrowserTypeConnectOverCDPOptions { Timeout = 8_000 })
                .ConfigureAwait(false);

            _ = browser.Version;
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "CDP connect check failed.");
            return false;
        }
    }
}
