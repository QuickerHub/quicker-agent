namespace QuickerAgent.Core;

/// <summary>
/// Playwright targets for uploading HTML into the getquicker.net shared-action intro editor.
/// </summary>
public sealed class ActionDescriptionUploadSettings
{
    /// <summary>
    /// Page to open after login. Use <c>{code}</c> or <c>{0}</c> for the shared action id.
    /// </summary>
    public string PageUrlTemplate { get; init; } = "https://getquicker.net/Sharedaction?code={code}";

    /// <summary>
    /// Optional CSS selector for a control that must be clicked before the Summernote editor appears
    /// (e.g. owner-only &quot;编辑&quot; entry). When null/empty, skipped.
    /// </summary>
    public string? OpenEditorCssSelector { get; init; }

    /// <summary>
    /// Selector passed to page.WaitForSelectorAsync for the editor root.
    /// </summary>
    public string EditorWaitSelector { get; init; } = "#summernote, .note-editable";

    /// <summary>
    /// Accessible name of the save button (Chinese UI default).
    /// </summary>
    public string SaveButtonAccessibleName { get; init; } = "保存";

    /// <summary>
    /// Optional CSS selector for save; used when role/name lookup finds nothing.
    /// </summary>
    public string? SaveCssSelector { get; init; }

    /// <summary>
    /// Builds settings from optional environment overrides.
    /// </summary>
    public static ActionDescriptionUploadSettings FromEnvironment()
    {
        var pageUrl = Environment.GetEnvironmentVariable("QKAGENT_ACTION_DOC_PAGE_URL");
        var openEditor = Environment.GetEnvironmentVariable("QKAGENT_ACTION_DOC_OPEN_EDITOR_SELECTOR");
        var editorWait = Environment.GetEnvironmentVariable("QKAGENT_ACTION_DOC_EDITOR_SELECTOR");
        var saveName = Environment.GetEnvironmentVariable("QKAGENT_ACTION_DOC_SAVE_BUTTON_TEXT");
        var saveCss = Environment.GetEnvironmentVariable("QKAGENT_ACTION_DOC_SAVE_SELECTOR");

        return new ActionDescriptionUploadSettings
        {
            PageUrlTemplate = string.IsNullOrWhiteSpace(pageUrl)
                ? "https://getquicker.net/Sharedaction?code={code}"
                : pageUrl.Trim(),
            OpenEditorCssSelector = string.IsNullOrWhiteSpace(openEditor) ? null : openEditor.Trim(),
            EditorWaitSelector = string.IsNullOrWhiteSpace(editorWait)
                ? "#summernote, .note-editable"
                : editorWait.Trim(),
            SaveButtonAccessibleName = string.IsNullOrWhiteSpace(saveName) ? "保存" : saveName.Trim(),
            SaveCssSelector = string.IsNullOrWhiteSpace(saveCss) ? null : saveCss.Trim(),
        };
    }

    /// <summary>
    /// Expands <see cref="PageUrlTemplate"/> with the shared action id.
    /// </summary>
    public static string ExpandPageUrl(string template, string sharedActionCode)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(template);
        ArgumentException.ThrowIfNullOrWhiteSpace(sharedActionCode);

        return template
            .Replace("{code}", sharedActionCode, StringComparison.OrdinalIgnoreCase)
            .Replace("{0}", sharedActionCode, StringComparison.Ordinal);
    }
}
