using System.Text.RegularExpressions;

namespace QuickerAgent.Core;

public static partial class SessionIdHelper
{
    private static readonly Regex SafeId = MyRegex();

    /// <summary>
    /// Returns a filesystem-safe session id segment, or throws if empty after normalization.
    /// </summary>
    public static string Normalize(string? sessionId)
    {
        var s = (sessionId ?? string.Empty).Trim();
        if (s.Length == 0)
        {
            s = "default";
        }

        if (!SafeId.IsMatch(s))
        {
            throw new ArgumentException(
                "Session id may only contain letters, digits, hyphen, and underscore.",
                nameof(sessionId));
        }

        return s;
    }

    [GeneratedRegex(@"^[a-zA-Z0-9_-]{1,64}$", RegexOptions.CultureInvariant)]
    private static partial Regex MyRegex();
}
