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
    QuickerAgentSettings agentSettings,
    CancellationToken cancellationToken = default) =>
    RunWithEditorAsync(
      email,
      password,
      sharedActionCode,
      agentSettings,
      readHtml: true,
      htmlToWrite: null,
      cancellationToken);

  public Task<ActionDocOperationResult> SetHtmlAsync(
    string email,
    string password,
    string sharedActionCode,
    string htmlContent,
    QuickerAgentSettings agentSettings,
    CancellationToken cancellationToken = default)
  {
    ArgumentNullException.ThrowIfNull(htmlContent);
    return RunWithEditorAsync(
      email,
      password,
      sharedActionCode,
      agentSettings,
      readHtml: false,
      htmlToWrite: htmlContent,
      cancellationToken);
  }

  private async Task<ActionDocOperationResult> RunWithEditorAsync(
    string email,
    string password,
    string sharedActionCode,
    QuickerAgentSettings agentSettings,
    bool readHtml,
    string? htmlToWrite,
    CancellationToken cancellationToken)
  {
    ArgumentException.ThrowIfNullOrEmpty(email);
    ArgumentException.ThrowIfNullOrEmpty(password);
    ArgumentException.ThrowIfNullOrWhiteSpace(sharedActionCode);
    ArgumentNullException.ThrowIfNull(agentSettings);

    try
    {
      using var playwright = await Playwright.CreateAsync().ConfigureAwait(false);
      await using var session = await QuickerBrowserSession
        .CreateAsync(playwright, agentSettings, _loginService, _logger, cancellationToken)
        .ConfigureAwait(false);

      await session.EnsureLoggedInAsync(email, password, cancellationToken).ConfigureAwait(false);
      var page = await session.GetPageAsync(cancellationToken).ConfigureAwait(false);

      var editorReady = await NavigateToEditPageWithLoginRetryAsync(
          page,
          email,
          password,
          sharedActionCode,
          cancellationToken)
        .ConfigureAwait(false);
      if (!editorReady.Ok)
      {
        return editorReady;
      }

      if (readHtml)
      {
        var html = await ReadEditorHtmlAsync(page, cancellationToken).ConfigureAwait(false);
        if (string.IsNullOrEmpty(html))
        {
          return ActionDocOperationResult.Fail(
            "EDITOR_EMPTY",
            "Could not read intro HTML from source textarea. Log in as the action owner.");
        }

        _logger.LogInformation("Read description HTML ({Length} chars).", html.Length);
        return ActionDocOperationResult.Success(html);
      }

      var written = await WriteEditorHtmlAsync(page, htmlToWrite!, cancellationToken).ConfigureAwait(false);
      if (!written)
      {
        return ActionDocOperationResult.Fail(
          "EDITOR_NOT_FOUND",
          "Could not write HTML into source textarea after enabling code view.");
      }

      var synced = await SyncSourceToPreviewAsync(page, cancellationToken).ConfigureAwait(false);
      if (!synced)
      {
        return ActionDocOperationResult.Fail(
          "SOURCE_SYNC_FAILED",
          "Could not sync source HTML back to preview before submit.");
      }

      var saved = await SubmitEditFormAsync(page, cancellationToken).ConfigureAwait(false);
      if (!saved)
      {
        return ActionDocOperationResult.Fail(
          "SUBMIT_FAILED",
          "Could not submit the edit form (primary input or form.requestSubmit).");
      }

      await WaitForFormSubmitCompleteAsync(page, cancellationToken).ConfigureAwait(false);
      return ActionDocOperationResult.Success();
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "action-doc operation failed.");
      return ActionDocOperationResult.Fail("ACTION_DOC_ERROR", ex.Message);
    }
  }

  private async Task<ActionDocOperationResult> NavigateToEditPageWithLoginRetryAsync(
    IPage page,
    string email,
    string password,
    string sharedActionCode,
    CancellationToken cancellationToken)
  {
    var result = await NavigateToEditPageAsync(page, sharedActionCode, cancellationToken).ConfigureAwait(false);
    if (result.Ok || !string.Equals(result.ErrorCode, "SESSION_EXPIRED", StringComparison.Ordinal))
    {
      return result;
    }

    _logger.LogInformation("Edit page requires login; re-authenticating.");
    var loggedIn = await _loginService.LoginAsync(page, email, password, cancellationToken).ConfigureAwait(false);
    if (!loggedIn)
    {
      return ActionDocOperationResult.Fail("LOGIN_FAILED", "Quicker login did not complete.");
    }

    return await NavigateToEditPageAsync(page, sharedActionCode, cancellationToken).ConfigureAwait(false);
  }

  private async Task<ActionDocOperationResult> NavigateToEditPageAsync(
    IPage page,
    string sharedActionCode,
    CancellationToken cancellationToken)
  {
    _ = cancellationToken;
    var editUrl = GetQuickerActionDocPage.ExpandEditPageUrl(sharedActionCode);
    _logger.LogInformation("Opening edit page {Url}", editUrl);
    await page
      .GotoAsync(editUrl, new PageGotoOptions { WaitUntil = WaitUntilState.DOMContentLoaded, Timeout = 90_000 })
      .ConfigureAwait(false);

    if (await _loginService.IsLoginPageAsync(page).ConfigureAwait(false))
    {
      return ActionDocOperationResult.Fail(
        "SESSION_EXPIRED",
        "Redirected to login while opening the edit page.");
    }

    if (!page.Url.Contains(GetQuickerActionDocPage.EditPageUrlFragment, StringComparison.OrdinalIgnoreCase))
    {
      return ActionDocOperationResult.Fail(
        "EDIT_PAGE_NOT_REACHED",
        $"Expected edit page URL; got {page.Url}");
    }

    _logger.LogInformation("Waiting for editor ({Selector}).", GetQuickerActionDocPage.EditorWaitSelector);
    try
    {
      await page
        .WaitForSelectorAsync(
          GetQuickerActionDocPage.EditorWaitSelector,
          new PageWaitForSelectorOptions { State = WaitForSelectorState.Visible, Timeout = 45_000 })
        .ConfigureAwait(false);
    }
    catch (TimeoutException)
    {
      return ActionDocOperationResult.Fail(
        "EDITOR_NOT_FOUND",
        $"Editor not visible: {GetQuickerActionDocPage.EditorWaitSelector}");
    }

    return ActionDocOperationResult.Success();
  }

  private async Task<bool> EnsureSourceCodeViewAsync(IPage page)
  {
    var textarea = await FindSourceTextareaAsync(page).ConfigureAwait(false);
    if (textarea is not null && await textarea.IsVisibleAsync().ConfigureAwait(false))
    {
      return true;
    }

    try
    {
      var button = page
        .GetByRole(AriaRole.Button, new PageGetByRoleOptions { Name = GetQuickerActionDocPage.SourceCodeButtonName })
        .First;
      if (await button.IsVisibleAsync().ConfigureAwait(false))
      {
        _logger.LogInformation("Switching to source view ({ButtonName}).", GetQuickerActionDocPage.SourceCodeButtonName);
        await button.ClickAsync().ConfigureAwait(false);
      }
    }
    catch (Exception ex)
    {
      _logger.LogDebug(ex, "Source view button click failed; trying script toggle.");
      await page.EvaluateAsync<bool>(ActionDescriptionEditorInterop.EnsureSourceViewScript).ConfigureAwait(false);
    }

    foreach (var selector in GetQuickerActionDocPage.SourceCodeTextareaSelectors)
    {
      try
      {
        var loc = page.Locator(selector).First;
        await loc.WaitForAsync(
          new LocatorWaitForOptions { State = WaitForSelectorState.Visible, Timeout = 15_000 })
          .ConfigureAwait(false);
        return true;
      }
      catch (TimeoutException)
      {
        _logger.LogDebug("Source textarea not visible yet: {Selector}", selector);
      }
    }

    return false;
  }

  private async Task<ILocator?> FindSourceTextareaAsync(IPage page)
  {
    foreach (var selector in GetQuickerActionDocPage.SourceCodeTextareaSelectors)
    {
      try
      {
        var loc = page.Locator(selector).First;
        if (await loc.IsVisibleAsync().ConfigureAwait(false))
        {
          return loc;
        }
      }
      catch
      {
        // try next selector
      }
    }

    return null;
  }

  private async Task<string?> ReadEditorHtmlAsync(IPage page, CancellationToken cancellationToken)
  {
    _ = cancellationToken;
    if (!await EnsureSourceCodeViewAsync(page).ConfigureAwait(false))
    {
      _logger.LogWarning("Source view not available; falling back to Summernote API.");
      return await page
        .EvaluateAsync<string?>(ActionDescriptionEditorInterop.ReadSummernoteCodeScript)
        .ConfigureAwait(false);
    }

    var textarea = await FindSourceTextareaAsync(page).ConfigureAwait(false);
    if (textarea is null)
    {
      return null;
    }

    var html = await textarea.InputValueAsync().ConfigureAwait(false);
    if (!string.IsNullOrEmpty(html))
    {
      _logger.LogInformation("Read HTML from source textarea.");
      return html;
    }

    return await page
      .EvaluateAsync<string?>(ActionDescriptionEditorInterop.ReadSummernoteCodeScript)
      .ConfigureAwait(false);
  }

  private async Task<bool> WriteEditorHtmlAsync(
    IPage page,
    string htmlContent,
    CancellationToken cancellationToken)
  {
    _ = cancellationToken;
    if (!await EnsureSourceCodeViewAsync(page).ConfigureAwait(false))
    {
      return false;
    }

    var textarea = await FindSourceTextareaAsync(page).ConfigureAwait(false);
    if (textarea is null)
    {
      return false;
    }

    await textarea.FillAsync(htmlContent).ConfigureAwait(false);
    var dispatched = await page
      .EvaluateAsync<string?>(ActionDescriptionEditorInterop.DispatchSourceInputScript)
      .ConfigureAwait(false);
    _logger.LogInformation("Wrote HTML to source textarea (sync: {Selector}).", dispatched ?? "fill only");
    return true;
  }

  /// <summary>
  /// Exit source view so Summernote applies textarea content to the WYSIWYG preview (same as clicking 源代码 again).
  /// </summary>
  private async Task<bool> SyncSourceToPreviewAsync(IPage page, CancellationToken cancellationToken)
  {
    var codeview = page.Locator(".note-editor.codeview").First;
    var inCodeview = await codeview.IsVisibleAsync().ConfigureAwait(false);
    if (!inCodeview)
    {
      if (await page.Locator(".note-editable").First.IsVisibleAsync().ConfigureAwait(false))
      {
        _logger.LogInformation("Already in preview mode.");
        return true;
      }

      return false;
    }

    try
    {
      var button = page
        .GetByRole(AriaRole.Button, new PageGetByRoleOptions { Name = GetQuickerActionDocPage.SourceCodeButtonName })
        .First;
      if (await button.IsVisibleAsync().ConfigureAwait(false))
      {
        _logger.LogInformation(
          "Clicking {ButtonName} again to apply source HTML and return to preview.",
          GetQuickerActionDocPage.SourceCodeButtonName);
        await button.ClickAsync().ConfigureAwait(false);
        await page
          .Locator(".note-editable")
          .First
          .WaitForAsync(
            new LocatorWaitForOptions { State = WaitForSelectorState.Visible, Timeout = 10_000 })
          .ConfigureAwait(false);
        var stillCodeview = await codeview.IsVisibleAsync().ConfigureAwait(false);
        if (!stillCodeview)
        {
          return true;
        }
      }
    }
    catch (Exception ex)
    {
      _logger.LogWarning(ex, "Source button toggle back to preview failed; trying script.");
    }

    var mode = await page
      .EvaluateAsync<string?>(ActionDescriptionEditorInterop.ExitSourceViewScript)
      .ConfigureAwait(false);
    if (mode is "preview" or "toggled")
    {
      _logger.LogInformation("Exited source view via script ({Mode}).", mode);
      return true;
    }

    return await page.Locator(".note-editable").First.IsVisibleAsync().ConfigureAwait(false)
           && !await codeview.IsVisibleAsync().ConfigureAwait(false);
  }

  private async Task<bool> SubmitEditFormAsync(IPage page, CancellationToken cancellationToken)
  {
    _ = cancellationToken;

    try
    {
      var byName = page
        .GetByRole(AriaRole.Button, new PageGetByRoleOptions { Name = GetQuickerActionDocPage.SubmitButtonName })
        .First;
      if (await byName.IsVisibleAsync().ConfigureAwait(false))
      {
        await byName.ScrollIntoViewIfNeededAsync().ConfigureAwait(false);
        await byName.ClickAsync().ConfigureAwait(false);
        _logger.LogInformation("Clicked submit by accessible name.");
        return true;
      }
    }
    catch (Exception ex)
    {
      _logger.LogDebug(ex, "Submit by accessible name failed.");
    }

    foreach (var selector in GetQuickerActionDocPage.SubmitSelectors)
    {
      try
      {
        var submit = page.Locator(selector).First;
        if (await submit.IsVisibleAsync().ConfigureAwait(false))
        {
          _logger.LogInformation("Clicking submit input ({Selector}).", selector);
          await submit.ScrollIntoViewIfNeededAsync().ConfigureAwait(false);
          await submit.ClickAsync().ConfigureAwait(false);
          return true;
        }
      }
      catch (Exception ex)
      {
        _logger.LogDebug(ex, "Submit selector did not work: {Selector}", selector);
      }
    }

    try
    {
      var submitted = await page
        .EvaluateAsync<bool>(
          @"() => {
              const form = document.querySelector('body > div.body-wrapper > div > form')
                || document.querySelector('form');
              if (!form) return false;
              if (typeof form.requestSubmit === 'function') {
                form.requestSubmit();
                return true;
              }
              form.submit();
              return true;
            }")
        .ConfigureAwait(false);
      if (submitted)
      {
        _logger.LogInformation("Submitted edit form via requestSubmit().");
        return true;
      }
    }
    catch (Exception ex)
    {
      _logger.LogDebug(ex, "form.requestSubmit failed.");
    }

    return false;
  }

  private async Task WaitForFormSubmitCompleteAsync(IPage page, CancellationToken cancellationToken)
  {
    _ = cancellationToken;
    _logger.LogInformation("Waiting for form submit to complete...");

    try
    {
      await page
        .WaitForLoadStateAsync(LoadState.NetworkIdle, new PageWaitForLoadStateOptions { Timeout = 60_000 })
        .ConfigureAwait(false);
    }
    catch (TimeoutException)
    {
      _logger.LogDebug("NetworkIdle wait timed out after submit; continuing.");
    }

    try
    {
      await page
        .WaitForFunctionAsync(
          @"() => window.location.href.toLowerCase().indexOf('/member/action/edit') === -1",
          null,
          new PageWaitForFunctionOptions { Timeout = 30_000 })
        .ConfigureAwait(false);
    }
    catch (TimeoutException)
    {
      _logger.LogDebug("Leave edit page wait timed out; submit may still have succeeded.");
    }

    _logger.LogInformation("Form submit wait finished (url: {Url}).", page.Url);
  }
}
