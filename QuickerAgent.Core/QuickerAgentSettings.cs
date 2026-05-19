namespace QuickerAgent.Core;

/// <summary>
/// Global agent settings: browser profile, headless, optional channel override.
/// </summary>
public sealed class QuickerAgentSettings
{
  public string ProfileDirectory { get; init; } =
    Path.Combine(
      Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
      "qkagent",
      "browser-profile");

  /// <summary>
  /// Run browser without UI. Defaults to true so pull/push do not steal focus.
  /// Set <c>QKAGENT_HEADLESS=false</c> to show a minimized window for debugging.
  /// </summary>
  public bool Headless { get; init; } = true;

  /// <summary>
  /// Force browser channel: chrome, msedge, edge, chromium (bundled). When null, try chrome → msedge → bundled.
  /// </summary>
  public string? BrowserChannel { get; init; }

  public static QuickerAgentSettings FromEnvironment()
  {
    var profile = Environment.GetEnvironmentVariable("QKAGENT_PROFILE_DIR");
    var headlessRaw = Environment.GetEnvironmentVariable("QKAGENT_HEADLESS");
    var channel = Environment.GetEnvironmentVariable("QKAGENT_BROWSER_CHANNEL");

    return new QuickerAgentSettings
    {
      ProfileDirectory = string.IsNullOrWhiteSpace(profile)
        ? Path.Combine(
          Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
          "qkagent",
          "browser-profile")
        : Path.GetFullPath(profile.Trim()),
      Headless = ParseHeadless(headlessRaw),
      BrowserChannel = string.IsNullOrWhiteSpace(channel) ? null : channel.Trim(),
    };
  }

  private static bool ParseHeadless(string? value)
  {
    if (string.IsNullOrWhiteSpace(value))
    {
      return true;
    }

    value = value.Trim();
    if (value.Equals("0", StringComparison.Ordinal)
        || value.Equals("false", StringComparison.OrdinalIgnoreCase)
        || value.Equals("no", StringComparison.OrdinalIgnoreCase))
    {
      return false;
    }

    return value.Equals("1", StringComparison.Ordinal)
           || value.Equals("true", StringComparison.OrdinalIgnoreCase)
           || value.Equals("yes", StringComparison.OrdinalIgnoreCase);
  }
}
