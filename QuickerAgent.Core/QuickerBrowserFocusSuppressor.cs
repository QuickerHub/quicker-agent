using System.Runtime.InteropServices;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Playwright;

namespace QuickerAgent.Core;

/// <summary>
/// Keeps headed browser automation from stealing the foreground on Windows.
/// </summary>
internal static class QuickerBrowserFocusSuppressor
{
  internal static string[] BackgroundLaunchArgs =>
  [
    "--start-minimized",
    "--window-position=-24000,-24000",
    "--no-first-run",
    "--no-default-browser-check",
  ];

  internal static async Task TrySuppressAsync(
    IBrowserContext context,
    ILogger logger,
    CancellationToken cancellationToken = default)
  {
    _ = cancellationToken;

    if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
    {
      return;
    }

    try
    {
      var page = context.Pages.Count > 0
        ? context.Pages[0]
        : await context.NewPageAsync().ConfigureAwait(false);

      var cdp = await context.NewCDPSessionAsync(page).ConfigureAwait(false);
      var targetInfo = await cdp.SendAsync("Browser.getWindowForTarget").ConfigureAwait(false);
      if (targetInfo is null || targetInfo.Value.ValueKind != JsonValueKind.Object)
      {
        return;
      }

      if (!targetInfo.Value.TryGetProperty("windowId", out var windowIdProp))
      {
        return;
      }

      var windowId = windowIdProp.GetInt32();
      await cdp.SendAsync(
          "Browser.setWindowBounds",
          new Dictionary<string, object>
          {
            ["windowId"] = windowId,
            ["bounds"] = new Dictionary<string, object> { ["windowState"] = "minimized" },
          })
        .ConfigureAwait(false);
    }
    catch (Exception ex)
    {
      logger.LogDebug(ex, "Could not minimize browser window via CDP.");
    }
  }
}
