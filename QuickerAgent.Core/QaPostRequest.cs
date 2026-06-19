namespace QuickerAgent.Core;

/// <summary>
/// Input for posting a new QA topic.
/// </summary>
public sealed class QaPostRequest
{
  public required string Title { get; init; }

  public required int CategoryId { get; init; }

  public required string ContentHtml { get; init; }

  public string? Keywords { get; init; }
}
