using Microsoft.Extensions.Logging;
using Microsoft.Playwright;

namespace QuickerAgent.Core;

/// <summary>
/// Logs into getquicker.net using the same selectors as quicker_build_net QuickerDependencyService.
/// </summary>
public sealed class QuickerWebLoginService
{
  public const string LoginUrl = "https://getquicker.net/Identity/Account/Login";
  private const string HomeUrl = "https://getquicker.net/";

  private static class XPaths
  {
    public const string EmailInput = "//*[@id=\"Input_Email\"]";
    public const string PasswordInput = "//*[@id=\"Input_Password\"]";
    public const string LoginButton = "//button[@type=\"submit\" and text()=\"登录\"]";
  }

  private readonly ILogger<QuickerWebLoginService> _logger;

  public QuickerWebLoginService(ILogger<QuickerWebLoginService> logger)
  {
    _logger = logger;
  }

  /// <summary>
  /// Returns true when the current page state indicates an authenticated session.
  /// Navigates to the site home when checking from an unknown page.
  /// </summary>
  public async Task<bool> IsLoggedInAsync(IPage page, CancellationToken cancellationToken = default)
  {
    _ = cancellationToken;

    try
    {
      await page
        .GotoAsync(HomeUrl, new PageGotoOptions { Timeout = 30_000, WaitUntil = WaitUntilState.DOMContentLoaded })
        .ConfigureAwait(false);

      return !await IsLoginPageAsync(page).ConfigureAwait(false);
    }
    catch (Exception ex)
    {
      _logger.LogDebug(ex, "Could not verify login state; treating as logged out.");
      return false;
    }
  }

  /// <summary>
  /// Returns true when the current URL or visible controls indicate the login page.
  /// Does not navigate away from the current page.
  /// </summary>
  public async Task<bool> IsLoginPageAsync(IPage page)
  {
    if (IsLoginUrl(page.Url))
    {
      return true;
    }

    try
    {
      var emailField = page.Locator(XPaths.EmailInput).First;
      return await emailField.IsVisibleAsync().ConfigureAwait(false);
    }
    catch
    {
      return false;
    }
  }

  public async Task<bool> LoginAsync(
    IPage page,
    string email,
    string password,
    CancellationToken cancellationToken = default)
  {
    ArgumentException.ThrowIfNullOrEmpty(email);
    ArgumentException.ThrowIfNullOrEmpty(password);

    try
    {
      _logger.LogInformation("Navigating to login page...");
      await page.GotoAsync(LoginUrl, new PageGotoOptions { Timeout = 30_000 }).ConfigureAwait(false);

      _logger.LogInformation("Filling login form...");
      await page.FillAsync(XPaths.EmailInput, email).ConfigureAwait(false);
      await page.FillAsync(XPaths.PasswordInput, password).ConfigureAwait(false);

      _logger.LogInformation("Submitting login form...");
      await page.ClickAsync(XPaths.LoginButton).ConfigureAwait(false);

      await page
        .WaitForFunctionAsync(
          "window.location.href.toLowerCase().indexOf('login') === -1",
          new PageWaitForFunctionOptions { Timeout = 15_000 })
        .ConfigureAwait(false);

      if (IsLoginUrl(page.Url))
      {
        _logger.LogWarning("Still on login page after submit.");
        return false;
      }

      _logger.LogInformation("Login successful.");
      return true;
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Login failed.");
      return false;
    }
  }

  private static bool IsLoginUrl(string url) =>
    url.Contains("/Account/Login", StringComparison.OrdinalIgnoreCase)
    || url.Contains("/Identity/Account/Login", StringComparison.OrdinalIgnoreCase);
}
