namespace QuickerAgent.Core;

using System.Text.RegularExpressions;

/// <summary>
/// getquicker.net QA forum URLs and form selectors.
/// </summary>
public static class GetQuickerQaPage
{
  private static readonly Regex QuestionIdPattern = new(
    @"/QA/Question/(\d+)",
    RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);
  public const string NewTopicUrl = "https://getquicker.net/QA/New";

  public const string EditQuestionUrlTemplate = "https://getquicker.net/QA/EditQuestion?id={id}";

  public const string QuestionUrlFragment = "/QA/Question/";

  public const string EditQuestionUrlFragment = "/QA/EditQuestion";

  public const string TitleInputSelector = "#Vm_Title";

  public const string CategoryRadioSelector = "input[name=\"Vm.CategoryId\"]";

  public const string KeywordsInputSelector = "#Vm_Keywords";

  public const string ContentTextareaId = "Vm_Content";

  public const string SubmitButtonSelector = "form#form input[type='submit'][value='发表话题']";

  public const string EditSubmitButtonSelector = "form#form input[type='submit'][value='保存更改']";

  public const string ValidationErrorSelector = "form#form .field-validation-error, form#form .text-danger:not(.field-validation-valid)";

  /// <summary>Known QA category ids on getquicker.net.</summary>
  public static IReadOnlyDictionary<int, string> CategoryNames { get; } =
    new Dictionary<int, string>
    {
      [1] = "使用问题",
      [2] = "经验创意",
      [3] = "BUG反馈",
      [4] = "功能建议",
      [5] = "信息发布",
      [6] = "动作需求",
      [7] = "动作推荐",
      [8] = "随便聊聊",
      [9] = "动作开发",
      [10] = "异常报告",
      [11] = "动作库优化",
    };

  public static bool TryResolveCategoryId(string input, out int categoryId)
  {
    ArgumentException.ThrowIfNullOrWhiteSpace(input);
    input = input.Trim();

    if (int.TryParse(input, out categoryId) && CategoryNames.ContainsKey(categoryId))
    {
      return true;
    }

    foreach (var (id, name) in CategoryNames)
    {
      if (name.Equals(input, StringComparison.OrdinalIgnoreCase))
      {
        categoryId = id;
        return true;
      }
    }

    categoryId = 0;
    return false;
  }

  public static string FormatCategoryList() =>
    string.Join(", ", CategoryNames.OrderBy(kv => kv.Key).Select(kv => $"{kv.Key}={kv.Value}"));

  public static string ExpandEditQuestionUrl(int questionId) =>
    EditQuestionUrlTemplate.Replace("{id}", questionId.ToString(), StringComparison.Ordinal);

  public static string ExpandQuestionUrl(int questionId) =>
    $"https://getquicker.net{QuestionUrlFragment}{questionId}";

  /// <summary>Parse question id from a numeric string or /QA/Question/{id} URL.</summary>
  public static bool TryParseQuestionId(string input, out int questionId)
  {
    ArgumentException.ThrowIfNullOrWhiteSpace(input);
    input = input.Trim();

    if (int.TryParse(input, out questionId) && questionId > 0)
    {
      return true;
    }

    var match = QuestionIdPattern.Match(input);
    if (match.Success && int.TryParse(match.Groups[1].Value, out questionId) && questionId > 0)
    {
      return true;
    }

    questionId = 0;
    return false;
  }
}
