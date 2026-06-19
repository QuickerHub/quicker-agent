namespace QuickerAgent.Core;

/// <summary>
/// Result of creating or updating a QA topic on getquicker.net.
/// </summary>
public readonly record struct QaPostOperationResult(
  bool Ok,
  string? ErrorCode,
  string? Message,
  int? QuestionId = null,
  string? QuestionUrl = null)
{
  public static QaPostOperationResult Success(int questionId, string questionUrl) =>
    new(true, null, null, questionId, questionUrl);

  public static QaPostOperationResult Fail(string code, string message) =>
    new(false, code, message, null, null);
}
