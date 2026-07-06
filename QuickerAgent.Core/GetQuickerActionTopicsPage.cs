namespace QuickerAgent.Core;

using System.Text.RegularExpressions;

/// <summary>
/// getquicker.net shared-action topics URLs and DOM helpers.
/// </summary>
public static class GetQuickerActionTopicsPage
{
  private static readonly Regex TopicIdPattern = new(
    @"/Common/Topics/ViewTopic/(\d+)",
    RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

  private static readonly Regex SharedActionCodePattern = new(
    @"[?&]code=([0-9a-fA-F-]{36})",
    RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

  public const string TopicsListUrlTemplate =
    "https://getquicker.net/Share/Actions/Topics?code={code}";

  public const string TopicsListArchivedQuery = "&showAll=true";

  public const string ViewTopicUrlTemplate =
    "https://getquicker.net/Common/Topics/ViewTopic/{id}";

  public const string NewTopicUrlTemplate =
    "https://getquicker.net/Common/Topics/New?objectType=SharedAction&objectId={code}";

  public const string QuestionListSelector = ".question-list .question-item[data-tid]";

  public const string ReplyTextareaSelector = "#new-comment";

  public const string AddCommentButtonText = "添加评论";

  public const string SubmitReplyButtonText = "提交回复";

  public const string ArchiveButtonText = "归档";

  public static string ExpandTopicsListUrl(string sharedActionCode, bool includeArchived) =>
    ExpandTopicsListUrl(sharedActionCode, includeArchived ? TopicsListArchivedQuery : string.Empty);

  public static string ExpandTopicsListUrl(string sharedActionCode, string querySuffix = "")
  {
    ArgumentException.ThrowIfNullOrWhiteSpace(sharedActionCode);
    var url = TopicsListUrlTemplate.Replace("{code}", sharedActionCode.Trim(), StringComparison.Ordinal);
    return string.IsNullOrEmpty(querySuffix) ? url : url + querySuffix;
  }

  public static string ExpandViewTopicUrl(int topicId) =>
    ViewTopicUrlTemplate.Replace("{id}", topicId.ToString(), StringComparison.Ordinal);

  public static bool TryParseTopicId(string input, out int topicId)
  {
    ArgumentException.ThrowIfNullOrWhiteSpace(input);
    input = input.Trim();

    if (int.TryParse(input, out topicId) && topicId > 0)
    {
      return true;
    }

    var match = TopicIdPattern.Match(input);
    if (match.Success && int.TryParse(match.Groups[1].Value, out topicId) && topicId > 0)
    {
      return true;
    }

    topicId = 0;
    return false;
  }

  public static bool TryParseSharedActionCode(string input, out string sharedActionCode)
  {
    ArgumentException.ThrowIfNullOrWhiteSpace(input);
    input = input.Trim();

    if (Guid.TryParse(input, out _))
    {
      sharedActionCode = input;
      return true;
    }

    var match = SharedActionCodePattern.Match(input);
    if (match.Success)
    {
      sharedActionCode = match.Groups[1].Value;
      return true;
    }

    sharedActionCode = string.Empty;
    return false;
  }

  /// <summary>Maps getquicker topic category to cea-action-issues labels.</summary>
  public static IReadOnlyList<string> SuggestIssueLabels(string? category)
  {
    if (string.IsNullOrWhiteSpace(category))
    {
      return ["area:actions", "type:idea"];
    }

    return category.Trim() switch
    {
      "BUG反馈" or "异常报告" => ["area:actions", "type:bug"],
      "功能建议" or "动作需求" => ["area:actions", "type:feat"],
      "使用问题" => ["area:actions", "type:bug"],
      _ => ["area:actions", "type:idea"],
    };
  }
}
