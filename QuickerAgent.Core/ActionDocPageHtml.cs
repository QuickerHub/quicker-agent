using System.Text.RegularExpressions;

namespace QuickerAgent.Core;

/// <summary>
/// Helpers for editing action-doc page.html fragments.
/// </summary>
public static partial class ActionDocPageHtml
{
  private static readonly Regex ImgTagRegex = new(
    @"<img\b[^>]*>",
    RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

  /// <summary>
  /// Replace the <c>src</c> of one <c>&lt;img&gt;</c> tag by index or alt substring match.
  /// </summary>
  public static bool TryReplaceImageSrc(
    string html,
    string newSrc,
    int index,
    string? altContains,
    out string updated,
    out string? error)
  {
    ArgumentException.ThrowIfNullOrWhiteSpace(newSrc);
    updated = html;
    error = null;

    var matches = ImgTagRegex.Matches(html);
    if (matches.Count == 0)
    {
      error = "No <img> tag found in page.html.";
      return false;
    }

    var matchIndex = ResolveTargetIndex(matches, index, altContains, out error);
    if (matchIndex < 0)
    {
      return false;
    }

    var tag = matches[matchIndex].Value;
    var replacedTag = ReplaceSrcAttribute(tag, newSrc);
    updated = html.Remove(matches[matchIndex].Index, matches[matchIndex].Length)
      .Insert(matches[matchIndex].Index, replacedTag);
    return true;
  }

  private static int ResolveTargetIndex(
    MatchCollection matches,
    int index,
    string? altContains,
    out string? error)
  {
    error = null;

    if (!string.IsNullOrWhiteSpace(altContains))
    {
      for (var i = 0; i < matches.Count; i++)
      {
        if (TryGetAltText(matches[i].Value, out var alt)
            && alt.Contains(altContains.Trim(), StringComparison.OrdinalIgnoreCase))
        {
          return i;
        }
      }

      error = $"No <img> with alt containing '{altContains.Trim()}'.";
      return -1;
    }

    if (index < 0 || index >= matches.Count)
    {
      error = $"Image index {index} is out of range (found {matches.Count} <img> tag(s)).";
      return -1;
    }

    return index;
  }

  private static string ReplaceSrcAttribute(string tag, string newSrc)
  {
    if (SrcAttributeRegex().IsMatch(tag))
    {
      return SrcAttributeRegex().Replace(tag, $"src=\"{EscapeHtmlAttribute(newSrc)}\"");
    }

    return tag.Insert(4, $" src=\"{EscapeHtmlAttribute(newSrc)}\"");
  }

  private static bool TryGetAltText(string tag, out string alt)
  {
    var match = AltAttributeRegex().Match(tag);
    if (!match.Success)
    {
      alt = string.Empty;
      return false;
    }

    alt = match.Groups[1].Value;
    return alt.Length > 0;
  }

  private static string EscapeHtmlAttribute(string value) =>
    value.Replace("&", "&amp;", StringComparison.Ordinal)
      .Replace("\"", "&quot;", StringComparison.Ordinal);

  [GeneratedRegex(@"\bsrc\s*=\s*(['""])(?<v>.*?)\1", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
  private static partial Regex SrcAttributeRegex();

  [GeneratedRegex(@"\balt\s*=\s*(['""])(?<v>.*?)\1", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
  private static partial Regex AltAttributeRegex();
}
