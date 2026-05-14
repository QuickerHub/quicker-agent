using System.Text.Json;

namespace QuickerAgent.Core;

/// <summary>
/// Extracts a CDP endpoint URL from environment, JSON line, or plain text.
/// </summary>
public static class CdpUrlParser
{
    /// <summary>
    /// Reads <c>QKAGENT_CDP_URL</c> when set and non-empty.
    /// </summary>
    public static string? FromEnvironment()
    {
        var raw = Environment.GetEnvironmentVariable("QKAGENT_CDP_URL");
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        return NormalizeUrl(raw.Trim());
    }

    /// <summary>
    /// Tries to parse a line as JSON <c>{"cdp":"ws://..."}</c> (property name case-insensitive).
    /// </summary>
    public static bool TryParseJsonLine(string line, out string? cdpUrl)
    {
        cdpUrl = null;
        line = line.Trim();
        if (line.Length == 0 || line[0] != '{')
        {
            return false;
        }

        try
        {
            using var doc = JsonDocument.Parse(line);
            if (doc.RootElement.ValueKind != JsonValueKind.Object)
            {
                return false;
            }

            foreach (var prop in doc.RootElement.EnumerateObject())
            {
                if (prop.NameEquals("cdp") && prop.Value.ValueKind == JsonValueKind.String)
                {
                    cdpUrl = NormalizeUrl(prop.Value.GetString()!);
                    return !string.IsNullOrEmpty(cdpUrl);
                }
            }
        }
        catch (JsonException)
        {
            return false;
        }

        return false;
    }

    /// <summary>
    /// Treats the whole line as a URL if it looks like http(s) or ws(s).
    /// </summary>
    public static bool TryParsePlainLine(string line, out string? cdpUrl)
    {
        cdpUrl = null;
        line = line.Trim();
        if (line.Length == 0)
        {
            return false;
        }

        if (!line.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
            && !line.StartsWith("https://", StringComparison.OrdinalIgnoreCase)
            && !line.StartsWith("ws://", StringComparison.OrdinalIgnoreCase)
            && !line.StartsWith("wss://", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        cdpUrl = NormalizeUrl(line);
        return !string.IsNullOrEmpty(cdpUrl);
    }

    /// <summary>
    /// Finds the first ws(s):// or http(s):// token in arbitrary text.
    /// </summary>
    public static bool TryExtractEmbedded(string line, out string? cdpUrl)
    {
        cdpUrl = null;
        var idx = IndexOfAnyScheme(line, out var len);
        if (idx < 0)
        {
            return false;
        }

        var end = line.IndexOfAny([' ', '\t', '\r', '\n', '"', '\''], idx + len);
        var slice = end < 0 ? line[idx..] : line[idx..end];
        cdpUrl = NormalizeUrl(slice);
        return !string.IsNullOrEmpty(cdpUrl);
    }

    public static bool TryParseLine(string line, out string? cdpUrl)
    {
        if (TryParseJsonLine(line, out cdpUrl))
        {
            return true;
        }

        if (TryParsePlainLine(line, out cdpUrl))
        {
            return true;
        }

        return TryExtractEmbedded(line, out cdpUrl);
    }

    private static int IndexOfAnyScheme(string line, out int schemeLen)
    {
        schemeLen = 0;
        var schemes = new[] { "wss://", "ws://", "https://", "http://" };
        var best = -1;
        foreach (var s in schemes)
        {
            var i = line.IndexOf(s, StringComparison.OrdinalIgnoreCase);
            if (i >= 0 && (best < 0 || i < best))
            {
                best = i;
                schemeLen = s.Length;
            }
        }

        return best;
    }

    private static string NormalizeUrl(string url) => url.Trim();
}
