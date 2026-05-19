using Microsoft.Extensions.Logging;
using Microsoft.Playwright;

namespace QuickerAgent.Core;

/// <summary>
/// Launches a persistent Playwright context, preferring system Chrome then Edge, then bundled Chromium.
/// </summary>
public static class QuickerBrowserLauncher
{
  private const string DefaultUserAgent =
    "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36";

  public static async Task<(IBrowserContext Context, QuickerBrowserLaunchInfo Info)> LaunchPersistentAsync(
    IPlaywright playwright,
    QuickerAgentSettings settings,
    ILogger logger,
    CancellationToken cancellationToken = default)
  {
    ArgumentNullException.ThrowIfNull(playwright);
    ArgumentNullException.ThrowIfNull(settings);
    ArgumentNullException.ThrowIfNull(logger);

    Directory.CreateDirectory(settings.ProfileDirectory);

    var channels = ResolveChannelOrder(settings.BrowserChannel);
    PlaywrightException? lastError = null;

    foreach (var channel in channels)
    {
      cancellationToken.ThrowIfCancellationRequested();

      try
      {
        var options = BuildLaunchOptions(settings, channel);
        var context = await playwright.Chromium
          .LaunchPersistentContextAsync(settings.ProfileDirectory, options)
          .ConfigureAwait(false);

        if (!settings.Headless)
        {
          await QuickerBrowserFocusSuppressor
            .TrySuppressAsync(context, logger, cancellationToken)
            .ConfigureAwait(false);
        }

        var label = channel ?? "chromium";
        logger.LogInformation(
          "Browser started (channel={Channel}, headless={Headless}, profile={Profile}).",
          label,
          settings.Headless,
          settings.ProfileDirectory);

        return (context, new QuickerBrowserLaunchInfo(label, channel is null));
      }
      catch (PlaywrightException ex)
      {
        lastError = ex;
        logger.LogDebug(
          ex,
          "Could not launch browser channel '{Channel}'; trying next.",
          channel ?? "bundled-chromium");
      }
    }

    var detail = lastError?.Message ?? "No channel attempted.";
    throw new InvalidOperationException(
      "Could not launch Chrome, Edge, or bundled Chromium. Install Google Chrome or Microsoft Edge, " +
      "or run Playwright install chromium. " + detail,
      lastError);
  }

  private static BrowserTypeLaunchPersistentContextOptions BuildLaunchOptions(
    QuickerAgentSettings settings,
    string? channel)
  {
    var options = new BrowserTypeLaunchPersistentContextOptions
    {
      Headless = settings.Headless,
      ViewportSize = new ViewportSize { Width = 1280, Height = 800 },
      UserAgent = DefaultUserAgent,
    };

    if (!string.IsNullOrWhiteSpace(channel))
    {
      options.Channel = channel;
    }

    if (!settings.Headless)
    {
      options.Args = QuickerBrowserFocusSuppressor.BackgroundLaunchArgs;
    }

    return options;
  }

  internal static string?[] ResolveChannelOrder(string? forcedChannel)
  {
    if (string.IsNullOrWhiteSpace(forcedChannel))
    {
      return new string?[] { "chrome", "msedge", null };
    }

    var normalized = forcedChannel.Trim().ToLowerInvariant();
    return normalized switch
    {
      "chromium" or "bundled" or "playwright" => new string?[] { null },
      "chrome" => new string?[] { "chrome" },
      "msedge" or "edge" => new string?[] { "msedge" },
      _ => new string?[] { forcedChannel.Trim() },
    };
  }
}
