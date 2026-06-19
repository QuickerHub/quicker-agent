using System.Net;
using Microsoft.Extensions.Logging;
using Microsoft.Playwright;

namespace QuickerAgent.Core;

/// <summary>
/// Creates and updates QA topics on getquicker.net via Playwright.
/// </summary>
public sealed class QaPostService
{
  private readonly QuickerWebLoginService _loginService;
  private readonly ILogger<QaPostService> _logger;

  public QaPostService(QuickerWebLoginService loginService, ILogger<QaPostService> logger)
  {
    _loginService = loginService;
    _logger = logger;
  }

  public Task<QaPostOperationResult> PostTopicAsync(
    string email,
    string password,
    QaPostRequest request,
    QuickerAgentSettings agentSettings,
    CancellationToken cancellationToken = default)
  {
    ArgumentException.ThrowIfNullOrEmpty(email);
    ArgumentException.ThrowIfNullOrEmpty(password);
    ArgumentNullException.ThrowIfNull(request);
    ArgumentNullException.ThrowIfNull(agentSettings);

    if (string.IsNullOrWhiteSpace(request.Title))
    {
      return Task.FromResult(QaPostOperationResult.Fail("INVALID_TITLE", "Title is required."));
    }

    if (!GetQuickerQaPage.CategoryNames.ContainsKey(request.CategoryId))
    {
      return Task.FromResult(QaPostOperationResult.Fail(
        "INVALID_CATEGORY",
        $"Unknown category id {request.CategoryId}. Valid values: {GetQuickerQaPage.FormatCategoryList()}"));
    }

    if (string.IsNullOrWhiteSpace(request.ContentHtml))
    {
      return Task.FromResult(QaPostOperationResult.Fail("INVALID_CONTENT", "Content is required."));
    }

    return RunWithSessionAsync(
      email,
      password,
      agentSettings,
      cancellationToken,
      async (page, ct) =>
      {
        if (!await OpenQaPageAsync(page, GetQuickerQaPage.NewTopicUrl, email, password, ct).ConfigureAwait(false))
        {
          return QaPostOperationResult.Fail("LOGIN_FAILED", "Could not sign in to getquicker.net.");
        }

        if (!await EnsureQaFormReadyAsync(page, ct).ConfigureAwait(false))
        {
          return QaPostOperationResult.Fail("EDITOR_NOT_READY", "TinyMCE editor did not initialize on /QA/New.");
        }

        try
        {
          await ApplyTopicFieldsAsync(
              page,
              title: request.Title.Trim(),
              categoryId: request.CategoryId,
              contentHtml: request.ContentHtml,
              keywords: request.Keywords?.Trim())
            .ConfigureAwait(false);
        }
        catch (InvalidOperationException ex)
        {
          return QaPostOperationResult.Fail("EDITOR_WRITE_FAILED", ex.Message);
        }

        _logger.LogInformation(
          "Submitting new topic (category {CategoryId}: {CategoryName})...",
          request.CategoryId,
          GetQuickerQaPage.CategoryNames[request.CategoryId]);

        return await SubmitTopicAsync(
            page,
            GetQuickerQaPage.SubmitButtonSelector,
            expectedQuestionId: null,
            ct)
          .ConfigureAwait(false);
      });
  }

  public Task<QaPostOperationResult> UpdateTopicAsync(
    string email,
    string password,
    QaEditRequest request,
    QuickerAgentSettings agentSettings,
    CancellationToken cancellationToken = default)
  {
    ArgumentException.ThrowIfNullOrEmpty(email);
    ArgumentException.ThrowIfNullOrEmpty(password);
    ArgumentNullException.ThrowIfNull(request);
    ArgumentNullException.ThrowIfNull(agentSettings);

    if (request.QuestionId <= 0)
    {
      return Task.FromResult(QaPostOperationResult.Fail("INVALID_QUESTION_ID", "Question id must be positive."));
    }

    if (!request.HasAnyField)
    {
      return Task.FromResult(QaPostOperationResult.Fail(
        "NO_FIELDS_TO_UPDATE",
        "Provide at least one of --title, --category, --content/--content-file, or --keywords."));
    }

    if (request.CategoryId is int categoryId && !GetQuickerQaPage.CategoryNames.ContainsKey(categoryId))
    {
      return Task.FromResult(QaPostOperationResult.Fail(
        "INVALID_CATEGORY",
        $"Unknown category id {categoryId}. Valid values: {GetQuickerQaPage.FormatCategoryList()}"));
    }

    return RunWithSessionAsync(
      email,
      password,
      agentSettings,
      cancellationToken,
      async (page, ct) =>
      {
        var editUrl = GetQuickerQaPage.ExpandEditQuestionUrl(request.QuestionId);
        if (!await OpenQaPageAsync(page, editUrl, email, password, ct).ConfigureAwait(false))
        {
          return QaPostOperationResult.Fail("LOGIN_FAILED", "Could not sign in to getquicker.net.");
        }

        if (page.Url.Contains("/Errors/404", StringComparison.OrdinalIgnoreCase))
        {
          return QaPostOperationResult.Fail(
            "QUESTION_NOT_FOUND",
            $"Question #{request.QuestionId} was not found or you cannot edit it.");
        }

        if (!await EnsureQaFormReadyAsync(page, ct).ConfigureAwait(false))
        {
          return QaPostOperationResult.Fail("FORM_NOT_READY", "QA edit form did not load.");
        }

        try
        {
          await ApplyTopicFieldsAsync(
              page,
              title: string.IsNullOrWhiteSpace(request.Title) ? null : request.Title.Trim(),
              categoryId: request.CategoryId,
              contentHtml: string.IsNullOrWhiteSpace(request.ContentHtml) ? null : request.ContentHtml,
              keywords: request.Keywords)
            .ConfigureAwait(false);
        }
        catch (InvalidOperationException ex)
        {
          return QaPostOperationResult.Fail("EDITOR_WRITE_FAILED", ex.Message);
        }

        _logger.LogInformation("Updating QA topic {QuestionId}...", request.QuestionId);

        return await SubmitTopicAsync(
            page,
            GetQuickerQaPage.EditSubmitButtonSelector,
            expectedQuestionId: request.QuestionId,
            ct)
          .ConfigureAwait(false);
      });
  }

  public static string NormalizeContentHtml(string content)
  {
    ArgumentNullException.ThrowIfNull(content);
    content = content.Trim();
    if (content.Length == 0)
    {
      return content;
    }

    if (content.Contains('<', StringComparison.Ordinal) && content.Contains('>', StringComparison.Ordinal))
    {
      return content;
    }

    var paragraphs = content
      .Split(["\r\n\r\n", "\n\n"], StringSplitOptions.None)
      .Select(p => p.Trim())
      .Where(p => p.Length > 0)
      .Select(p => "<p>" + WebUtility.HtmlEncode(p).Replace("\r\n", "<br>").Replace("\n", "<br>") + "</p>");

    return string.Join(string.Empty, paragraphs);
  }

  private async Task<QaPostOperationResult> RunWithSessionAsync(
    string email,
    string password,
    QuickerAgentSettings agentSettings,
    CancellationToken cancellationToken,
    Func<IPage, CancellationToken, Task<QaPostOperationResult>> action)
  {
    try
    {
      using var playwright = await Playwright.CreateAsync().ConfigureAwait(false);
      await using var session = await QuickerBrowserSession
        .CreateAsync(playwright, agentSettings, _loginService, _logger, cancellationToken)
        .ConfigureAwait(false);

      await session.EnsureLoggedInAsync(email, password, cancellationToken).ConfigureAwait(false);
      var page = await session.GetPageAsync(cancellationToken).ConfigureAwait(false);
      return await action(page, cancellationToken).ConfigureAwait(false);
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "QA operation failed.");
      return QaPostOperationResult.Fail("QA_OPERATION_FAILED", ex.Message);
    }
  }

  private async Task<bool> OpenQaPageAsync(
    IPage page,
    string url,
    string email,
    string password,
    CancellationToken cancellationToken)
  {
    _logger.LogInformation("Opening {Url}", url);
    await page
      .GotoAsync(url, new PageGotoOptions
      {
        Timeout = 60_000,
        WaitUntil = WaitUntilState.DOMContentLoaded,
      })
      .ConfigureAwait(false);

    if (!await _loginService.IsLoginPageAsync(page).ConfigureAwait(false))
    {
      return true;
    }

    _logger.LogInformation("Redirected to login; signing in again...");
    var loggedIn = await _loginService.LoginAsync(page, email, password, cancellationToken).ConfigureAwait(false);
    if (!loggedIn)
    {
      return false;
    }

    await page
      .GotoAsync(url, new PageGotoOptions
      {
        Timeout = 60_000,
        WaitUntil = WaitUntilState.DOMContentLoaded,
      })
      .ConfigureAwait(false);

    return !await _loginService.IsLoginPageAsync(page).ConfigureAwait(false);
  }

  private async Task<bool> EnsureQaFormReadyAsync(IPage page, CancellationToken cancellationToken)
  {
    _ = cancellationToken;

    var formReady = await page
      .WaitForSelectorAsync(GetQuickerQaPage.TitleInputSelector, new PageWaitForSelectorOptions
      {
        Timeout = 30_000,
        State = WaitForSelectorState.Visible,
      })
      .ConfigureAwait(false);

    if (formReady is null)
    {
      return false;
    }

    if (!await WaitForTinyMceReadyAsync(page, cancellationToken).ConfigureAwait(false))
    {
      return false;
    }

    await page.WaitForLoadStateAsync(LoadState.NetworkIdle).ConfigureAwait(false);
    return true;
  }

  private static async Task ApplyTopicFieldsAsync(
    IPage page,
    string? title,
    int? categoryId,
    string? contentHtml,
    string? keywords)
  {
    if (!string.IsNullOrWhiteSpace(title))
    {
      await page.FillAsync(GetQuickerQaPage.TitleInputSelector, title).ConfigureAwait(false);
    }

    if (categoryId is int category)
    {
      await page
        .CheckAsync($"{GetQuickerQaPage.CategoryRadioSelector}[value=\"{category}\"]")
        .ConfigureAwait(false);
    }

    if (!string.IsNullOrWhiteSpace(contentHtml))
    {
      var written = await TryWriteTinyMceContentAsync(page, contentHtml).ConfigureAwait(false);
      if (!written)
      {
        throw new InvalidOperationException("Could not write content into TinyMCE.");
      }
    }

    if (keywords is not null)
    {
      await page.FillAsync(GetQuickerQaPage.KeywordsInputSelector, keywords.Trim()).ConfigureAwait(false);
    }
  }

  private async Task<QaPostOperationResult> SubmitTopicAsync(
    IPage page,
    string submitSelector,
    int? expectedQuestionId,
    CancellationToken cancellationToken)
  {
    _ = cancellationToken;

    await page.ClickAsync(submitSelector).ConfigureAwait(false);

    var expectedFragment = expectedQuestionId is int id
      ? $"{GetQuickerQaPage.QuestionUrlFragment}{id}"
      : GetQuickerQaPage.QuestionUrlFragment;

    try
    {
      await page
        .WaitForURLAsync(
          url => url.Contains(expectedFragment, StringComparison.OrdinalIgnoreCase),
          new PageWaitForURLOptions { Timeout = 60_000 })
        .ConfigureAwait(false);
    }
    catch (TimeoutException)
    {
      var validation = await ReadValidationErrorsAsync(page).ConfigureAwait(false);
      if (!string.IsNullOrWhiteSpace(validation))
      {
        return QaPostOperationResult.Fail("VALIDATION_FAILED", validation);
      }

      return QaPostOperationResult.Fail(
        "SUBMIT_TIMEOUT",
        "Submit did not redirect to the question page. Check fields and permissions.");
    }

    if (!GetQuickerQaPage.TryParseQuestionId(page.Url, out var questionId))
    {
      return QaPostOperationResult.Fail(
        "QUESTION_ID_PARSE_FAILED",
        $"Submit finished but could not parse question id from URL: {page.Url}");
    }

    if (expectedQuestionId is int expected && questionId != expected)
    {
      return QaPostOperationResult.Fail(
        "QUESTION_ID_MISMATCH",
        $"Expected question #{expected} but landed on #{questionId} ({page.Url}).");
    }

    var questionUrl = GetQuickerQaPage.ExpandQuestionUrl(questionId);
    _logger.LogInformation("QA topic {QuestionId}: {QuestionUrl}", questionId, page.Url);
    return QaPostOperationResult.Success(questionId, page.Url);
  }

  private static async Task<bool> WaitForTinyMceReadyAsync(IPage page, CancellationToken cancellationToken)
  {
    _ = cancellationToken;
    try
    {
      await page
        .WaitForFunctionAsync(
          "() => window.tinymce && typeof window.tinymce.get === 'function' && !!window.tinymce.get('Vm_Content')",
          new PageWaitForFunctionOptions { Timeout = 30_000 })
        .ConfigureAwait(false);
      return true;
    }
    catch (TimeoutException)
    {
      return false;
    }
  }

  private static async Task<bool> TryWriteTinyMceContentAsync(IPage page, string contentHtml)
  {
    var writeResult = await page
      .EvaluateAsync<string?>(
        """
        (content) => {
          try {
            const ed = window.tinymce?.get?.('Vm_Content');
            if (!ed) {
              const textarea = document.querySelector('#Vm_Content');
              if (textarea) {
                textarea.value = content;
                return textarea.value.length > 0 ? 'textarea-only' : 'editor-missing';
              }
              return 'editor-missing';
            }
            ed.setContent(content);
            window.tinymce.triggerSave();
            const saved = document.querySelector('#Vm_Content')?.value ?? '';
            if (saved.length > 0 || content.includes('<img')) {
              return 'ok';
            }
            return 'empty-textarea';
          } catch (err) {
            return 'error:' + String(err);
          }
        }
        """,
        contentHtml)
      .ConfigureAwait(false);

    return string.Equals(writeResult, "ok", StringComparison.Ordinal)
           || string.Equals(writeResult, "textarea-only", StringComparison.Ordinal);
  }

  private static async Task<string?> ReadValidationErrorsAsync(IPage page)
  {
    var errors = await page
      .Locator(GetQuickerQaPage.ValidationErrorSelector)
      .AllInnerTextsAsync()
      .ConfigureAwait(false);

    var messages = errors
      .Select(static e => e.Trim())
      .Where(static e => e.Length > 0)
      .Distinct(StringComparer.Ordinal)
      .ToList();

    return messages.Count == 0 ? null : string.Join("; ", messages);
  }
}
