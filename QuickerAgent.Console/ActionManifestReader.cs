namespace QuickerAgent.Console;

/// <summary>
/// Minimal key: value reader for per-action manifest YAML (no full YAML parser dependency).
/// </summary>
internal static class ActionManifestReader
{
    /// <summary>
    /// Reads shared action id and optional HTML file name from a manifest file.
    /// </summary>
    /// <param name="yamlPath">Path to action.yaml / meta.yaml / manifest.yaml.</param>
    /// <param name="defaultHtmlFile">Used when the manifest omits html key.</param>
    public static (string SharedId, string HtmlRelativePath) Read(string yamlPath, string defaultHtmlFile)
    {
        ArgumentException.ThrowIfNullOrEmpty(yamlPath);
        ArgumentException.ThrowIfNullOrEmpty(defaultHtmlFile);

        if (!File.Exists(yamlPath))
        {
            throw new FileNotFoundException("Manifest YAML not found.", yamlPath);
        }

        string? sharedId = null;
        var htmlFile = defaultHtmlFile;

        foreach (var raw in File.ReadAllLines(yamlPath))
        {
            var line = raw.Trim();
            if (line.Length == 0 || line.StartsWith('#'))
            {
                continue;
            }

            var colon = line.IndexOf(':');
            if (colon <= 0)
            {
                continue;
            }

            var key = line[..colon].Trim();
            var value = line[(colon + 1)..].Trim();
            value = StripQuotes(value);

            if (IsSharedIdKey(key))
            {
                sharedId = value;
            }
            else if (IsHtmlKey(key))
            {
                if (!string.IsNullOrWhiteSpace(value))
                {
                    htmlFile = value;
                }
            }
        }

        if (string.IsNullOrWhiteSpace(sharedId))
        {
            throw new InvalidOperationException(
                $"Manifest '{yamlPath}' must set one of: sharedId, shared_id, code.");
        }

        return (sharedId.Trim(), htmlFile.Trim());
    }

    private static bool IsSharedIdKey(string key) =>
        key.Equals("sharedId", StringComparison.OrdinalIgnoreCase)
        || key.Equals("shared_id", StringComparison.OrdinalIgnoreCase)
        || key.Equals("code", StringComparison.OrdinalIgnoreCase);

    private static bool IsHtmlKey(string key) =>
        key.Equals("html", StringComparison.OrdinalIgnoreCase)
        || key.Equals("htmlFile", StringComparison.OrdinalIgnoreCase)
        || key.Equals("html_file", StringComparison.OrdinalIgnoreCase)
        || key.Equals("description", StringComparison.OrdinalIgnoreCase);

    private static string StripQuotes(string value)
    {
        if (value.Length >= 2)
        {
            if ((value[0] == '"' && value[^1] == '"') || (value[0] == '\'' && value[^1] == '\''))
            {
                return value[1..^1];
            }
        }

        return value;
    }

    /// <summary>
    /// Finds action.yaml, meta.yaml, or manifest.yaml under <paramref name="directory"/>.
    /// </summary>
    public static string? FindManifestPath(string directory)
    {
        foreach (var name in new[] { "action.yaml", "action.yml", "meta.yaml", "meta.yml", "manifest.yaml", "manifest.yml" })
        {
            var p = Path.Combine(directory, name);
            if (File.Exists(p))
            {
                return p;
            }
        }

        return null;
    }
}
