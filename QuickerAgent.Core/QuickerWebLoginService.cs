using Microsoft.Extensions.Logging;
using Microsoft.Playwright;

namespace QuickerAgent.Core;

/// <summary>
/// Logs into getquicker.net using the same selectors as quicker_build_net QuickerDependencyService.
/// </summary>
public sealed class QuickerWebLoginService
{
    public const string LoginUrl = "https://getquicker.net/Identity/Account/Login";

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

    public async Task<bool> LoginAsync(IPage page, string email, string password, CancellationToken cancellationToken = default)
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

            await page.WaitForFunctionAsync(
                    "window.location.href.toLowerCase().indexOf('login') === -1",
                    new PageWaitForFunctionOptions { Timeout = 10_000 })
                .ConfigureAwait(false);

            _logger.LogInformation("Login successful.");
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Login failed.");
            return false;
        }
    }
}
