namespace QuickerAgent.Core;

/// <summary>
/// Fixed getquicker.net UI labels and selectors for the shared-action intro editor.
/// </summary>
public static class GetQuickerActionDocPage
{
  public const string EditPageUrlTemplate = "https://getquicker.net/Member/Action/Edit?id={code}";

  public const string EditPageUrlFragment = "/Member/Action/Edit";

  /// <summary>Summernote toolbar: toggle source code view.</summary>
  public const string SourceCodeButtonName = "源代码";

  /// <summary>Intro editor root (WYSIWYG or codeview container).</summary>
  public const string EditorWaitSelector = ".note-editor.note-frame";

  /// <summary>Hidden textarea Summernote binds on getquicker.net edit page.</summary>
  public const string SummernoteTextareaId = "SharedActionVm_Detail";

  /// <summary>Legacy id used on some Summernote installs.</summary>
  public const string SummernoteLegacyTextareaId = "summernote";

  /// <summary>JS expression resolving the Summernote root element.</summary>
  public const string SummernoteRootJs =
    "document.querySelector('#SharedActionVm_Detail') || document.querySelector('#summernote')";

  /// <summary>Source textarea when codeview is active (site-specific).</summary>
  public const string SourceCodeTextareaCss =
    "body > div.body-wrapper > div > form > div > div.col-md-9 > div:nth-child(5) > div > div.note-editor.note-frame.card.codeview > div.note-editing-area > textarea";

  /// <summary>Shorter fallbacks if layout shifts.</summary>
  public const string SourceCodeTextareaFallbackCss = "div.note-editor.codeview .note-editing-area textarea, .note-codable";

  /// <summary>Primary submit control on edit form (getquicker.net uses a button).</summary>
  public const string SubmitButtonCss = "form button.btn.btn-primary";

  /// <summary>Legacy submit input if the site layout changes.</summary>
  public const string SubmitInputCss =
    "body > div.body-wrapper > div > form > div > div.col-md-9 > div.form-group.mt-3.row > div > input.btn.btn-primary";

  public const string SubmitInputFallbackCss = "form input.btn.btn-primary[type='submit']";

  public const string EditFormCss = "body > div.body-wrapper > div > form";

  /// <summary>Accessible name fallback for submit input.</summary>
  public const string SubmitButtonName = "更新动作信息";

  public static string ExpandEditPageUrl(string sharedActionCode)
  {
    ArgumentException.ThrowIfNullOrWhiteSpace(sharedActionCode);

    return EditPageUrlTemplate
      .Replace("{code}", sharedActionCode, StringComparison.OrdinalIgnoreCase)
      .Replace("{0}", sharedActionCode, StringComparison.Ordinal);
  }

  public static IReadOnlyList<string> SourceCodeTextareaSelectors { get; } =
    [SourceCodeTextareaCss, "div.note-editor.codeview .note-editing-area textarea", ".note-codable"];

  public static IReadOnlyList<string> SubmitSelectors { get; } =
    [SubmitButtonCss, SubmitInputCss, SubmitInputFallbackCss];
}
