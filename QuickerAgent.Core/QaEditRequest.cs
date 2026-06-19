namespace QuickerAgent.Core;

/// <summary>
/// Partial update for an existing QA topic. At least one field must be set.
/// </summary>
public sealed class QaEditRequest
{
  public required int QuestionId { get; init; }

  public string? Title { get; init; }

  public int? CategoryId { get; init; }

  public string? ContentHtml { get; init; }

  public string? Keywords { get; init; }

  public bool HasAnyField =>
    !string.IsNullOrWhiteSpace(Title)
    || CategoryId is not null
    || !string.IsNullOrWhiteSpace(ContentHtml)
    || Keywords is not null;
}
