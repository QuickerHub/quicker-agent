namespace QuickerAgent.Core;

/// <summary>Summary row from a shared action topics list page.</summary>
public sealed record ActionTopicListItem(
  int TopicId,
  string Title,
  string TopicUrl,
  string? Category,
  int? ViewCount,
  string? Author,
  string? AuthorUrl,
  string? CreatedAtText,
  bool IsArchived);

/// <summary>A clickable control visible to the action author on a topic page.</summary>
public sealed record ActionTopicAuthorControl(string Text, string? Href);

/// <summary>Full topic detail including replies.</summary>
public sealed record ActionTopicDetail(
  int TopicId,
  string Title,
  string TopicUrl,
  string? Category,
  string? Author,
  string? AuthorUrl,
  string? CreatedAtText,
  string? SharedActionId,
  string? SharedActionUrl,
  string? SharedActionTitle,
  string BodyHtml,
  string BodyText,
  IReadOnlyList<ActionTopicReply> Replies,
  bool IsArchived,
  bool CanArchive,
  IReadOnlyList<ActionTopicAuthorControl>? AuthorControls = null);

/// <summary>A reply or comment on a topic.</summary>
public sealed record ActionTopicReply(
  int Index,
  string? Author,
  string? AuthorUrl,
  string? CreatedAtText,
  string BodyHtml,
  string BodyText,
  bool IsOriginalPost);

/// <summary>Result of an action-topics mutation (reply, archive, mark).</summary>
public readonly record struct ActionTopicsOperationResult(
  bool Ok,
  string? ErrorCode,
  string? Message,
  int? TopicId = null,
  string? TopicUrl = null)
{
  public static ActionTopicsOperationResult Success(int topicId, string topicUrl) =>
    new(true, null, null, topicId, topicUrl);

  public static ActionTopicsOperationResult Fail(string code, string message) =>
    new(false, code, message, null, null);
}
