using Microsoft.Extensions.Logging;
using Microsoft.Playwright;

namespace QuickerAgent.Core;

/// <summary>
/// Fetches or updates HTML intro text for a shared action on getquicker.net.
/// Uses a persistent browser profile and system Chrome/Edge when available.
/// </summary>
public sealed class ActionDescriptionService
{
  private readonly QuickerWebLoginService _loginService;
  private readonly ILogger<ActionDescriptionService> _logger;

  public ActionDescriptionService(
    QuickerWebLoginService loginService,
    ILogger<ActionDescriptionService> logger)
  {
    _loginService = loginService;
    _logger = logger;
  }

  public Task<ActionDocOperationResult> GetHtmlAsync(
    string email,
    string password,
    string sharedActionCode,
    ActionDescriptionUploadSettings pageSettings,
    QuickerAgentSettings agentSettings,
    CancellationToken cancellationToken = default) =>
    RunWithEditorAsync(
      email,
      password,
      sharedActionCode,
      pageSettings,
      agentSettings,
      readHtml: true,
      htmlToWrite: null,
      cancellationToken);

  public Task<ActionDocOperationResult> SetHtmlAsync(
    string email,
    string password,
    string sharedActionCode,
    string htmlContent,
    ActionDescriptionUploadSettings pageSettings,
    QuickerAgentSettings agentSettings,
    CancellationToken cancellationToken = default)
  {
    ArgumentNullException.ThrowIfNull(htmlContent);
    return RunWithEditorAsync(
      email,
      password,
      sharedActionCode,
      pageSettings,
      agentSettings,
      readHtml: false,
      htmlToWrite: htmlContent,
      cancellationToken);
  }

  private async Task<ActionDocOperationResult> RunWithEditorAsync(
    string email,
    string password,
    string sharedActionCode,
    ActionDescriptionUploadSettings pageSettings,
    QuickerAgentSettings agentSettings,
    bool readHtml,
    string? htmlToWrite,
    CancellationToken cancellationToken)
  {
    ArgumentException.ThrowIfNullOrEmpty(email);
    ArgumentException.ThrowIfNullOrEmpty(password);
    ArgumentException.ThrowIfNullOrWhiteSpace(sharedActionCode);
    ArgumentNullException.ThrowIfNull(pageSettings);
    ArgumentNullException.ThrowIfNull(agentSettings);

    try
    {
      using var playwright = await Playwright.CreateAsync().ConfigureAwait(false);
      await using var session = await QuickerBrowserSession
        .CreateAsync(playwright, agentSettings, _loginService, _logger, cancellationToken)
        .ConfigureAwait(false);

      await session.EnsureLoggedInAsync(email, password, cancellationToken).ConfigureAwait(false);
      var page = await session.GetPageAsync(cancellationToken).ConfigureAwait(false);

      var editorReady = await NavigateToEditorAsync(page, sharedActionCode, pageSettings, cancellationToken)
        .ConfigureAwait(false);
      if (!editorReady.Ok)
      {
        return editorReady;
      }

      if (readHtml)
      {
        var html = await ReadEditorHtmlAsync(page).ConfigureAwait(false);
        if (string.IsNullOrEmpty(html))
        {
          return ActionDocOperationResult.Fail(
            "EDITOR_EMPTY",
            "Summernote / .note-editable returned no HTML. Log in as the action owner or adjust selectors.");
        }

        _logger.LogInformation("Read description HTML ({Length} chars).", html.Length);
        return ActionDocOperationResult.Success(html);
      }

      var mode = await WriteEditorHtmlAsync(page, htmlToWrite!).ConfigureAwait(false);
      if (string.IsNullOrEmpty(mode))
      {
        return ActionDocOperationResult.Fail(
          "EDITOR_NOT_FOUND",
          "Summernote / .note-editable not found after wait. Adjust QKAGENT_ACTION_DOC_EDITOR_SELECTOR.");
      }

      _logger.LogInformation("Injected HTML via {Mode}.", mode);

      var saved = await TryClickSaveAsync(page, pageSettings, cancellationToken).ConfigureAwait(false);
      if (!saved)
      {
        return ActionDocOperationResult.Fail(
          "SAVE_BUTTON_NOT_FOUND",
          $"Could not find save control (name '{pageSettings.SaveButtonAccessibleName}' or QKAGENT_ACTION_DOC_SAVE_SELECTOR).");
      }

      await page.WaitForLoadStateAsync(LoadState.NetworkIdle).ConfigureAwait(false);
      return ActionDocOperationResult.Success();
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "action-doc operation failed.");
      return ActionDocOperationResult.Fail("ACTION_DOC_ERROR", ex.Message);
    }
  }

  private async Task<ActionDocOperationResult> NavigateToEditorAsync(
    IPage page,
    string sharedActionCode,
    ActionDescriptionUploadSettings settings,
    CancellationToken cancellationToken)
  {
    var targetUrl = ActionDescriptionUploadSettings.ExpandPageUrl(settings.PageUrlTemplate, sharedActionCode);
    _logger.LogInformation("Opening {Url}", targetUrl);
    await page
      .GotoAsync(targetUrl, new PageGotoOptions { WaitUntil = WaitUntilState.NetworkIdle, Timeout = 90_000 })
      .ConfigureAwait(false);

    if (await _loginService.IsLoginPageAsync(page).ConfigureAwait(false))
    {
      return ActionDocOperationResult.Fail(
        "SESSION_EXPIRED",
        "Login session expired while opening the action page. Retry the command to re-login.");
    }

    if (!string.IsNullOrWhiteSpace(settings.OpenEditorCssSelector))
    {
      var opener = page.Locator(settings.OpenEditorCssSelector).First;
      try
      {
        if (await opener.IsVisibleAsync().ConfigureAwait(false))
        {
          _logger.LogInformation("Clicking open-editor control ({Selector}).", settings.OpenEditorCssSelector);
          await opener.ClickAsync().ConfigureAwait(false);
          await Task.Delay(800, cancellationToken).ConfigureAwait(false);
        }
      }
      catch (Exception ex)
      {
        _logger.LogWarning(ex, "Open-editor selector did not succeed; continuing.");
      }
    }

    _logger.LogInformation("Waiting for editor ({Selector}).", settings.EditorWaitSelector);
    try
    {
      await page
        .WaitForSelectorAsync(
          settings.EditorWaitSelector,
          new PageWaitForSelectorOptions { State = WaitForSelectorState.Visible, Timeout = 45_000 })
        .ConfigureAwait(false);
    }
    catch (TimeoutException)
    {
      return ActionDocOperationResult.Fail(
        "EDITOR_NOT_FOUND",
        $"No visible editor matched '{settings.EditorWaitSelector}'. " +
        "Log in as the action owner, or set QKAGENT_ACTION_DOC_PAGE_URL / QKAGENT_ACTION_DOC_OPEN_EDITOR_SELECTOR / QKAGENT_ACTION_DOC_EDITOR_SELECTOR.");
    }

    return ActionDocOperationResult.Success();
  }

  private static async Task<string?> ReadEditorHtmlAsync(IPage page) =>
    await page
      .EvaluateAsync<string?>(
        @"() => {
            const jq = window['jQuery'] || window['$'];
            const sn = document.querySelector('#summernote');
            if (jq && sn && jq.fn && jq.fn.summernote) {
              return jq(sn).summernote('code');
            }
            const ed = document.querySelector('.note-editable');
            if (ed) {
              return ed.innerHTML;
            }
            return null;
          }")
      .ConfigureAwait(false);

  private static async Task<string?> WriteEditorHtmlAsync(IPage page, string htmlContent) =>
    await page
      .EvaluateAsync<string?>(
        @"html => {
            const jq = window['jQuery'] || window['$'];
            const sn = document.querySelector('#summernote');
            if (jq && sn && jq.fn && jq.fn.summernote) {
              jq(sn).summernote('code', html);
              return 'summernote';
            }
            const ed = document.querySelector('.note-editable');
            if (ed) {
              ed.innerHTML = html;
              return 'editable';
            }
            return null;
          }",
        htmlContent)
      .ConfigureAwait(false);

  private static async Task<bool> TryClickSaveAsync(
    IPage page,
    ActionDescriptionUploadSettings settings,
    CancellationToken cancellationToken)
  {
    _ = cancellationToken;

    if (!string.IsNullOrWhiteSpace(settings.SaveCssSelector))
    {
      var loc = page.Locator(settings.SaveCssSelector).First;
      if (await loc.IsVisibleAsync().ConfigureAwait(false))
      {
        await loc.ClickAsync().ConfigureAwait(false);
        return true;
      }
    }

    var byName = page
      .GetByRole(AriaRole.Button, new PageGetByRoleOptions { Name = settings.SaveButtonAccessibleName })
      .First;
    if (await byName.IsVisibleAsync().ConfigureAwait(false))
    {
      await byName.ClickAsync().ConfigureAwait(false);
      return true;
    }

    var submit = page.Locator("button[type='submit'], input[type='submit']").First;
    if (await submit.IsVisibleAsync().ConfigureAwait(false))
    {
      await submit.ClickAsync().ConfigureAwait(false);
      return true;
    }

    return false;
  }
}
