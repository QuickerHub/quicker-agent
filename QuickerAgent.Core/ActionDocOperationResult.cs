namespace QuickerAgent.Core;

/// <summary>
/// Result of fetching or updating shared-action description HTML.
/// </summary>
public readonly record struct ActionDocOperationResult(
  bool Ok,
  string? ErrorCode,
  string? Message,
  string? Html = null)
{
  public static ActionDocOperationResult Success(string? html = null) => new(true, null, null, html);

  public static ActionDocOperationResult Fail(string code, string message) =>
    new(false, code, message, null);
}
