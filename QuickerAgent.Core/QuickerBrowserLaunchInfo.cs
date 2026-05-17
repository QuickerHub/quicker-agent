namespace QuickerAgent.Core;

/// <summary>
/// Describes which browser channel was used for a session.
/// </summary>
public sealed record QuickerBrowserLaunchInfo(string ChannelLabel, bool UsesBundledChromium);
