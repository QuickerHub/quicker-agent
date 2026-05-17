using Microsoft.Extensions.Logging;
using Microsoft.Playwright;

namespace QuickerAgent.Core;

/// <summary>
/// Persistent browser profile with optional login refresh.
/// </summary>
public sealed class QuickerBrowserSession : IAsyncDisposable
{
  private readonly IBrowserContext _context;
  private readonly QuickerWebLoginService _loginService;
  private readonly ILogger _logger;
  private IPage? _page;

  private QuickerBrowserSession(
    IBrowserContext context,
    QuickerBrowserLaunchInfo launchInfo,
    QuickerWebLoginService loginService,
    ILogger logger)
  {
    _context = context;
    LaunchInfo = launchInfo;
    _loginService = loginService;
    _logger = logger;
  }

  public QuickerBrowserLaunchInfo LaunchInfo { get; }

  public static async Task<QuickerBrowserSession> CreateAsync(
    IPlaywright playwright,
    QuickerAgentSettings agentSettings,
    QuickerWebLoginService loginService,
    ILogger logger,
    CancellationToken cancellationToken = default)
  {
    var (context, launchInfo) = await QuickerBrowserLauncher
      .LaunchPersistentAsync(playwright, agentSettings, logger, cancellationToken)
      .ConfigureAwait(false);

    return new QuickerBrowserSession(context, launchInfo, loginService, logger);
  }

  public async Task<IPage> GetPageAsync(CancellationToken cancellationToken = default)
  {
    _ = cancellationToken;

    if (_page is not null && !_page.IsClosed)
    {
      return _page;
    }

    _page = _context.Pages.FirstOrDefault(p => !p.IsClosed) ?? await _context.NewPageAsync().ConfigureAwait(false);
    _page.SetDefaultTimeout(60_000);
    _page.SetDefaultNavigationTimeout(60_000);
    return _page;
  }

  public async Task EnsureLoggedInAsync(
    string email,
    string password,
    CancellationToken cancellationToken = default)
  {
    var page = await GetPageAsync(cancellationToken).ConfigureAwait(false);

    if (await _loginService.IsLoggedInAsync(page, cancellationToken).ConfigureAwait(false))
    {
      _logger.LogInformation("Reusing saved browser session (profile cookies).");
      return;
    }

    _logger.LogInformation("Session missing or expired; performing login.");
    var loggedIn = await _loginService.LoginAsync(page, email, password, cancellationToken).ConfigureAwait(false);
    if (!loggedIn)
    {
      throw new InvalidOperationException("Quicker login did not complete.");
    }
  }

  public async ValueTask DisposeAsync()
  {
    await _context.DisposeAsync().ConfigureAwait(false);
  }
}
