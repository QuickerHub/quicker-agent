using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using Microsoft.Playwright;

namespace QuickerAgent.Core;

/// <summary>
/// Reads and manages shared-action discussion topics on getquicker.net via Playwright.
/// </summary>
public sealed class ActionTopicsService
{
  private static readonly List<string> LastSubmitRequests = new();

  private const string ListExtractScript = """
    () => {
      const items = [];
      document.querySelectorAll('.question-list .question-item[data-tid]').forEach(el => {
        const topicId = parseInt(el.getAttribute('data-tid') || '0', 10);
        const titleLink = el.querySelector('a.question-title');
        const categorySpan = el.querySelector('span[style*="background-color"]');
        const viewSpan = el.querySelector('span[title="浏览次数"]');
        const authorLink = el.querySelector('a.user-link');
        const createdSpan = el.querySelector('span[title="创建时间"]');
        const viewText = viewSpan?.textContent?.replace(/\D/g, '') || '';
        items.push({
          topicId,
          title: (titleLink?.textContent || '').trim(),
          topicUrl: titleLink?.href || '',
          category: (categorySpan?.textContent || '').trim() || null,
          viewCount: viewText ? parseInt(viewText, 10) : null,
          author: (authorLink?.textContent || '').trim() || null,
          authorUrl: authorLink?.href || null,
          createdAtText: (createdSpan?.textContent || '').trim() || null,
          isArchived: el.classList.contains('archived') || !!el.querySelector('.archived, .badge-secondary')
        });
      });
      return items;
    }
    """;

  private const string ListAuthorControlsScript = """
    () => {
      const items = [];
      document.querySelectorAll('.question-list .question-item[data-tid]').forEach(el => {
        const topicId = parseInt(el.getAttribute('data-tid') || '0', 10);
        const controls = [...el.querySelectorAll('a, button, i[title], [title]')]
          .map(node => ({
            text: (node.getAttribute('title') || node.textContent || '').trim(),
            href: node.href || node.getAttribute('href') || '',
            cls: node.className || ''
          }))
          .filter(x => x.text);
        items.push({ topicId, controls });
      });
      return items;
    }
    """;

  private const string DetailExtractScript = """
    () => {
      const topicId = parseInt(location.pathname.split('/').pop() || '0', 10);
      const title = (document.querySelector('h1.topic-title')?.textContent || '').trim();
      const category = [...document.querySelectorAll('span, .badge')]
        .map(el => (el.textContent || '').trim())
        .find(t => ['BUG反馈','功能建议','使用问题','经验创意','动作需求','随便聊聊'].includes(t)) || null;
      const sharedActionLink = document.querySelector('a[href*="Sharedaction?code="]');
      const sharedActionUrl = sharedActionLink?.href || null;
      const sharedActionTitle = (sharedActionLink?.textContent || '').trim() || null;
      let sharedActionId = null;
      if (sharedActionUrl) {
        const m = sharedActionUrl.match(/code=([0-9a-f-]+)/i);
        sharedActionId = m ? m[1] : null;
      }
      const bodies = [...document.querySelectorAll('.topic-body.user-content')];
      const firstBody = bodies[0];
      const authorLink = document.querySelector('a.user-link');
      const createdText = document.querySelector('span[title="创建时间"]')?.textContent?.trim() || null;
      const archiveBtn = [...document.querySelectorAll('a, button')]
        .find(el => {
          const text = (el.textContent || '').trim();
          return text === '归档' || text === '归档话题' || text.includes('归档此') || text.includes('标记为已处理');
        });
      const replies = bodies.map((el, index) => {
        const replyAuthor = el.closest('.d-flex, .media, .comment-item')?.querySelector('a.user-link');
        const replyTime = el.closest('.d-flex, .media, .comment-item')?.querySelector('span[title="创建时间"]');
        return {
          index,
          author: (replyAuthor?.textContent || (index === 0 ? authorLink?.textContent : '') || '').trim() || null,
          authorUrl: replyAuthor?.href || (index === 0 ? authorLink?.href : null) || null,
          createdAtText: (replyTime?.textContent || (index === 0 ? createdText : '') || '').trim() || null,
          bodyHtml: el.innerHTML || '',
          bodyText: (el.innerText || '').trim(),
          isOriginalPost: el.getAttribute('isfirst') === '1' || index === 0
        };
      });
      return {
        topicId,
        title,
        topicUrl: location.href,
        category,
        author: (authorLink?.textContent || '').trim() || null,
        authorUrl: authorLink?.href || null,
        createdAtText: createdText,
        sharedActionId,
        sharedActionUrl,
        sharedActionTitle,
        bodyHtml: firstBody?.innerHTML || '',
        bodyText: (firstBody?.innerText || '').trim(),
        replies,
        isArchived: document.body.innerText.includes('已归档') || !!document.querySelector('.archived-topic'),
        canArchive: !!archiveBtn,
        authorControls: [...document.querySelectorAll('a, button, .dropdown-item')]
          .map(el => ({ text: (el.textContent || '').trim(), href: el.href || el.getAttribute('href') || '' }))
          .filter(x => x.text && x.text.length < 48)
          .slice(0, 80)
      };
    }
    """;

  private readonly QuickerWebLoginService _loginService;
  private readonly ILogger<ActionTopicsService> _logger;

  public ActionTopicsService(QuickerWebLoginService loginService, ILogger<ActionTopicsService> logger)
  {
    _loginService = loginService;
    _logger = logger;
  }

  public Task<IReadOnlyList<ActionTopicListItem>> ListTopicsAsync(
    string sharedActionCode,
    bool includeArchived,
    QuickerAgentSettings agentSettings,
    CancellationToken cancellationToken = default)
  {
    ArgumentException.ThrowIfNullOrWhiteSpace(sharedActionCode);
    ArgumentNullException.ThrowIfNull(agentSettings);

    return RunReadOnlyAsync(
      agentSettings,
      cancellationToken,
      async (page, ct) =>
      {
        var url = GetQuickerActionTopicsPage.ExpandTopicsListUrl(
          sharedActionCode,
          includeArchived ? GetQuickerActionTopicsPage.TopicsListArchivedQuery : string.Empty);

        await NavigateAsync(page, url, ct).ConfigureAwait(false);
        await page.WaitForSelectorAsync(
            GetQuickerActionTopicsPage.QuestionListSelector,
            new PageWaitForSelectorOptions { Timeout = 30_000, State = WaitForSelectorState.Attached })
          .ConfigureAwait(false);

        var raw = await page.EvaluateAsync<JsonElement>(ListExtractScript).ConfigureAwait(false);
        return ParseListItems(raw, sharedActionCode);
      });
  }

  public Task<ActionTopicDetail> GetTopicAsync(
    int topicId,
    QuickerAgentSettings agentSettings,
    CancellationToken cancellationToken = default) =>
    GetTopicAsync(topicId, email: null, password: null, agentSettings, cancellationToken);

  public Task<ActionTopicDetail> GetTopicAsync(
    int topicId,
    string? email,
    string? password,
    QuickerAgentSettings agentSettings,
    CancellationToken cancellationToken = default)
  {
    if (topicId <= 0)
    {
      throw new ArgumentOutOfRangeException(nameof(topicId));
    }

    ArgumentNullException.ThrowIfNull(agentSettings);

    var useAuth = !string.IsNullOrWhiteSpace(email) && !string.IsNullOrWhiteSpace(password);

    if (useAuth)
    {
      return RunAuthenticatedReadAsync(
        email!,
        password!,
        agentSettings,
        cancellationToken,
        async (page, ct) => await ReadTopicDetailAsync(page, topicId, ct).ConfigureAwait(false));
    }

    return RunReadOnlyAsync(
      agentSettings,
      cancellationToken,
      async (page, ct) => await ReadTopicDetailAsync(page, topicId, ct).ConfigureAwait(false));
  }

  public Task<ActionTopicsOperationResult> ReplyAsync(
    string email,
    string password,
    int topicId,
    string contentHtml,
    QuickerAgentSettings agentSettings,
    CancellationToken cancellationToken = default)
  {
    ArgumentException.ThrowIfNullOrEmpty(email);
    ArgumentException.ThrowIfNullOrEmpty(password);
    if (topicId <= 0)
    {
      return Task.FromResult(ActionTopicsOperationResult.Fail("INVALID_TOPIC_ID", "Topic id must be positive."));
    }

    if (string.IsNullOrWhiteSpace(contentHtml))
    {
      return Task.FromResult(ActionTopicsOperationResult.Fail("INVALID_CONTENT", "Content is required."));
    }

    ArgumentNullException.ThrowIfNull(agentSettings);

    return RunAuthenticatedAsync(
      email,
      password,
      agentSettings,
      cancellationToken,
      async (page, ct) =>
      {
        var url = GetQuickerActionTopicsPage.ExpandViewTopicUrl(topicId);
        if (!await OpenPageAsync(page, url, email, password, ct).ConfigureAwait(false))
        {
          return ActionTopicsOperationResult.Fail("LOGIN_FAILED", "Could not sign in to getquicker.net.");
        }

        await page.ReloadAsync(new PageReloadOptions { WaitUntil = WaitUntilState.DOMContentLoaded })
          .ConfigureAwait(false);
        await page
          .WaitForSelectorAsync("h1.topic-title", new PageWaitForSelectorOptions { Timeout = 30_000 })
          .ConfigureAwait(false);

        var normalized = QaPostService.NormalizeContentHtml(contentHtml);
        var plainSnippet = StripHtmlToPlainForComment(normalized).Trim();

        if (!await EnsureReplyEditorVisibleAsync(page, ct).ConfigureAwait(false))
        {
          return ActionTopicsOperationResult.Fail(
            "REPLY_FORM_NOT_FOUND",
            "Reply editor was not found. Ensure the account can comment on this topic.");
        }

        var written = await TryWriteTopicReplyContentAsync(page, normalized).ConfigureAwait(false);
        if (!written)
        {
          var probe = await ProbeReplyEditorStateAsync(page).ConfigureAwait(false);
          return ActionTopicsOperationResult.Fail(
            "REPLY_WRITE_FAILED",
            $"Could not write content into the topic reply editor. Probe: {probe}");
        }

        var snippet = plainSnippet.Length > 24 ? plainSnippet[..24] : plainSnippet;
        var requiresOfficialDraft = await page
          .Locator("button")
          .Filter(new LocatorFilterOptions { HasText = GetQuickerActionTopicsPage.SubmitReplyButtonText })
          .First
          .IsVisibleAsync()
          .ConfigureAwait(false);

        if (requiresOfficialDraft && !await HasOfficialReplyDraftAsync(page, snippet).ConfigureAwait(false))
        {
          var probe = await ProbeReplyEditorStateAsync(page).ConfigureAwait(false);
          return ActionTopicsOperationResult.Fail(
            "REPLY_WRITE_FAILED",
            $"Reply draft was empty before submit. Probe: {probe}");
        }

        if (requiresOfficialDraft)
        {
          try
          {
            await page
              .WaitForFunctionAsync(
                TopicReplyEditorInterop.IsSubmitReplyEnabledScript,
                new PageWaitForFunctionOptions { Timeout = 10_000 })
              .ConfigureAwait(false);
          }
          catch (TimeoutException)
          {
            var probe = await ProbeReplyEditorStateAsync(page).ConfigureAwait(false);
            return ActionTopicsOperationResult.Fail(
              "SUBMIT_BUTTON_DISABLED",
              $"Submit reply stayed disabled after writing. Probe: {probe}");
          }

          var preSubmitProbe = await ProbeReplyEditorStateAsync(page).ConfigureAwait(false);
          await File.WriteAllTextAsync(
              Path.Combine(AppContext.BaseDirectory, "topic-pre-submit.html"),
              await page.ContentAsync().ConfigureAwait(false))
            .ConfigureAwait(false);
          await File.WriteAllTextAsync(
              Path.Combine(AppContext.BaseDirectory, "topic-pre-submit-probe.json"),
              preSubmitProbe)
            .ConfigureAwait(false);
        }

        if (!await TrySubmitTopicReplyAsync(page, snippet).ConfigureAwait(false))
        {
          var probe = await ProbeReplyEditorStateAsync(page).ConfigureAwait(false);
          var posts = LastSubmitRequests.Count > 0 ? string.Join("; ", LastSubmitRequests) : "(none)";
          return ActionTopicsOperationResult.Fail(
            "SUBMIT_FAILED",
            $"Reply submit did not complete. Posts: {posts}. Probe: {probe}");
        }

        try
        {
          await page
            .WaitForLoadStateAsync(LoadState.NetworkIdle, new PageWaitForLoadStateOptions { Timeout = 30_000 })
            .ConfigureAwait(false);
        }
        catch (TimeoutException)
        {
          // NetworkIdle may not settle; continue if still on topic page.
        }

        _logger.LogInformation("Replied to action topic {TopicId}", topicId);
        return ActionTopicsOperationResult.Success(topicId, page.Url);
      });
  }

  public Task<ActionTopicsOperationResult> ArchiveAsync(
    string email,
    string password,
    int topicId,
    QuickerAgentSettings agentSettings,
    CancellationToken cancellationToken = default)
  {
    ArgumentException.ThrowIfNullOrEmpty(email);
    ArgumentException.ThrowIfNullOrEmpty(password);
    if (topicId <= 0)
    {
      return Task.FromResult(ActionTopicsOperationResult.Fail("INVALID_TOPIC_ID", "Topic id must be positive."));
    }

    ArgumentNullException.ThrowIfNull(agentSettings);

    return RunAuthenticatedAsync(
      email,
      password,
      agentSettings,
      cancellationToken,
      async (page, ct) =>
      {
        var detail = await ReadTopicDetailAsync(page, topicId, ct).ConfigureAwait(false);
        if (detail.CanArchive)
        {
          return await ClickArchiveOnViewTopicAsync(page, ct).ConfigureAwait(false)
            ? ActionTopicsOperationResult.Success(topicId, page.Url)
            : ActionTopicsOperationResult.Fail("ARCHIVE_CLICK_FAILED", "Archive control was found but click failed.");
        }

        if (string.IsNullOrWhiteSpace(detail.SharedActionId))
        {
          return ActionTopicsOperationResult.Fail(
            "MISSING_SHARED_ACTION",
            "Could not resolve shared action id for this topic.");
        }

        var listUrl = GetQuickerActionTopicsPage.ExpandTopicsListUrl(detail.SharedActionId);
        await NavigateAsync(page, listUrl, ct).ConfigureAwait(false);
        await page
          .WaitForSelectorAsync(GetQuickerActionTopicsPage.QuestionListSelector, new PageWaitForSelectorOptions
          {
            Timeout = 30_000,
            State = WaitForSelectorState.Attached,
          })
          .ConfigureAwait(false);

        var row = page.Locator($".question-item[data-tid=\"{topicId}\"]");
        if (!await row.IsVisibleAsync().ConfigureAwait(false))
        {
          return ActionTopicsOperationResult.Fail("TOPIC_ROW_NOT_FOUND", $"Topic #{topicId} was not found on the action topics list.");
        }

        var listArchive = row.Locator("a, button, [title]")
          .Filter(new LocatorFilterOptions { HasTextRegex = new Regex("归档") });

        if (await listArchive.First.IsVisibleAsync().ConfigureAwait(false))
        {
          await listArchive.First.ClickAsync().ConfigureAwait(false);
        }
        else
        {
          var titled = row.Locator("[title*='归档'], [data-action*='archive']");
          if (!await titled.First.IsVisibleAsync().ConfigureAwait(false))
          {
            return ActionTopicsOperationResult.Fail(
              "ARCHIVE_CONTROL_NOT_FOUND",
              "Archive control not visible. Only the shared-action author can archive topics.");
          }

          await titled.First.ClickAsync().ConfigureAwait(false);
        }

        await ConfirmArchiveDialogAsync(page).ConfigureAwait(false);
        _logger.LogInformation("Archived action topic {TopicId} from list page", topicId);
        return ActionTopicsOperationResult.Success(topicId, GetQuickerActionTopicsPage.ExpandViewTopicUrl(topicId));
      });
  }

  private static async Task<bool> ClickArchiveOnViewTopicAsync(IPage page, CancellationToken cancellationToken)
  {
    _ = cancellationToken;
    var archiveControl = page
      .Locator("a, button")
      .Filter(new LocatorFilterOptions
      {
        HasTextRegex = new Regex("^(归档|归档话题)$|归档此|标记为已处理"),
      });

    if (!await archiveControl.First.IsVisibleAsync().ConfigureAwait(false))
    {
      return false;
    }

    await archiveControl.First.ClickAsync().ConfigureAwait(false);
    await ConfirmArchiveDialogAsync(page).ConfigureAwait(false);
    return true;
  }

  private static async Task ConfirmArchiveDialogAsync(IPage page)
  {
    var confirm = page.Locator(
      "button:has-text('确定'), button:has-text('确认'), input[type='submit'][value*='确定']");
    if (await confirm.First.IsVisibleAsync().ConfigureAwait(false))
    {
      await confirm.First.ClickAsync().ConfigureAwait(false);
    }

    await page.WaitForLoadStateAsync(LoadState.DOMContentLoaded).ConfigureAwait(false);
  }

  /// <summary>Alias for archive when triage marks a topic handled.</summary>
  public Task<ActionTopicsOperationResult> MarkHandledAsync(
    string email,
    string password,
    int topicId,
    QuickerAgentSettings agentSettings,
    CancellationToken cancellationToken = default) =>
    ArchiveAsync(email, password, topicId, agentSettings, cancellationToken);

  private static async Task<bool> EnsureReplyEditorVisibleAsync(IPage page, CancellationToken cancellationToken)
  {
    _ = cancellationToken;

    var hasOfficialReply = await page
      .Locator("button")
      .Filter(new LocatorFilterOptions { HasText = GetQuickerActionTopicsPage.SubmitReplyButtonText })
      .First
      .IsVisibleAsync()
      .ConfigureAwait(false);

    if (!hasOfficialReply)
    {
      var addComment = page
        .Locator("a, button")
        .Filter(new LocatorFilterOptions { HasText = GetQuickerActionTopicsPage.AddCommentButtonText });

      if (await addComment.First.IsVisibleAsync().ConfigureAwait(false))
      {
        await addComment.First.ClickAsync().ConfigureAwait(false);
        await page.WaitForTimeoutAsync(500).ConfigureAwait(false);
      }
    }

    try
    {
      await page
        .WaitForSelectorAsync(
          ".note-editor, #new-comment, textarea[id*='comment'], textarea[name*='Content']",
          new PageWaitForSelectorOptions { Timeout = 15_000, State = WaitForSelectorState.Attached })
        .ConfigureAwait(false);
      return true;
    }
    catch (TimeoutException)
    {
      return false;
    }
  }

  private sealed class TopicReplyWritePayload
  {
    public bool Ok { get; set; }
    public int Len { get; set; }
    public int TaValueLen { get; set; }
    public bool SubmitEnabled { get; set; }
    public string? Reason { get; set; }
  }

  private static async Task<bool> TryWriteTopicReplyContentAsync(IPage page, string contentHtml)
  {
    var html = contentHtml.Trim();
    var plain = StripHtmlToPlainForComment(contentHtml).Trim();
    if (plain.Length == 0)
    {
      return false;
    }

    var hasOfficialReply = await page
      .Locator("button")
      .Filter(new LocatorFilterOptions { HasText = GetQuickerActionTopicsPage.SubmitReplyButtonText })
      .First
      .IsVisibleAsync()
      .ConfigureAwait(false);

    if (hasOfficialReply)
    {
      return await TryWriteOfficialTopicReplyAsync(page, html, plain).ConfigureAwait(false);
    }

    return await TryWriteThreadedTopicCommentAsync(page, plain).ConfigureAwait(false);
  }

  private static async Task<bool> TryWriteOfficialTopicReplyAsync(IPage page, string html, string plain)
  {
    var snippet = plain.Length > 16 ? plain[..16] : plain;
    var editorIndex = await page
      .EvaluateAsync<int>(TopicReplyEditorInterop.FindOfficialReplyEditorIndexScript)
      .ConfigureAwait(false);

    if (editorIndex < 0)
    {
      return false;
    }

    if (await TryWriteOfficialTopicReplyViaSourceViewPlaywrightAsync(page, editorIndex, html, snippet)
          .ConfigureAwait(false))
    {
      return true;
    }

    var officialEditor = page.Locator(".note-editor").Nth(editorIndex);
    var editable = officialEditor.Locator(".note-editing-area .note-editable").First;

    if (await editable.IsVisibleAsync().ConfigureAwait(false))
    {
      await page.EvaluateAsync(
        """
        (index) => {
          const editor = document.querySelectorAll('.note-editor')[index];
          const ta = editor?.querySelector('textarea');
          const jq = window['jQuery'] || window['$'];
          if (ta && jq?.fn?.summernote) {
            jq(ta).summernote('focus');
          }
          editor?.querySelector('.note-editing-area .note-editable')?.focus();
        }
        """,
        editorIndex).ConfigureAwait(false);

      await editable.ClickAsync().ConfigureAwait(false);
      await editable.PressSequentiallyAsync(plain, new LocatorPressSequentiallyOptions { Delay = 25 })
        .ConfigureAwait(false);
      await page.WaitForTimeoutAsync(500).ConfigureAwait(false);

      _ = await page
        .EvaluateAsync<TopicReplyWritePayload>(
          TopicReplyEditorInterop.SyncOfficialReplyFromEditableScript,
          editorIndex)
        .ConfigureAwait(false);

      if (await HasOfficialReplyDraftAsync(page, snippet).ConfigureAwait(false))
      {
        return true;
      }
    }

    foreach (var write in new Func<Task<TopicReplyWritePayload?>>[]
             {
               () => page.EvaluateAsync<TopicReplyWritePayload>(
                 TopicReplyEditorInterop.WriteOfficialReplyViaSourceViewScript,
                 new { html }),
               () => page.EvaluateAsync<TopicReplyWritePayload>(
                 TopicReplyEditorInterop.WriteOfficialReplyScript,
                 new { html }),
             })
    {
      _ = await write().ConfigureAwait(false);
      if (await HasOfficialReplyDraftAsync(page, snippet).ConfigureAwait(false))
      {
        return true;
      }
    }

    return false;
  }

  private static async Task<bool> TryWriteOfficialTopicReplyViaSourceViewPlaywrightAsync(
    IPage page,
    int editorIndex,
    string html,
    string snippet)
  {
    var editor = page.Locator(".note-editor").Nth(editorIndex);
    var sourceBtn = editor
      .Locator("button[aria-label*='源代码'], button[data-original-title*='源代码'], button")
      .Filter(new LocatorFilterOptions { HasText = GetQuickerActionDocPage.SourceCodeButtonName })
      .First;

    if (!await sourceBtn.IsVisibleAsync().ConfigureAwait(false))
    {
      return false;
    }

    await sourceBtn.ClickAsync().ConfigureAwait(false);
    var codable = editor.Locator(".note-codable, div.note-editing-area textarea").First;
    try
    {
      await codable.WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Visible, Timeout = 10_000 })
        .ConfigureAwait(false);
    }
    catch (TimeoutException)
    {
      return false;
    }

    await codable.FillAsync(html).ConfigureAwait(false);
    await page
      .EvaluateAsync(
        """
        (index) => {
          const editor = document.querySelectorAll('.note-editor')[index];
          const codable = editor?.querySelector('.note-codable, .note-editing-area textarea');
          codable?.dispatchEvent(new Event('input', { bubbles: true }));
          codable?.dispatchEvent(new Event('change', { bubbles: true }));
        }
        """,
        editorIndex)
      .ConfigureAwait(false);

    await sourceBtn.ClickAsync().ConfigureAwait(false);
    await editor
      .Locator(".note-editing-area .note-editable")
      .First
      .WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Visible, Timeout = 10_000 })
      .ConfigureAwait(false);

    return await HasOfficialReplyDraftAsync(page, snippet).ConfigureAwait(false);
  }

  private static async Task<bool> HasOfficialReplyDraftAsync(IPage page, string snippet)
  {
    return await page
      .EvaluateAsync<bool>(
        """
        (snippet) => {
          const h5 = [...document.querySelectorAll('h5')].find(h => (h.textContent || '').includes('回复主贴'));
          if (!h5) return false;
          const needle = snippet.replace(/\\s+/g, '');
          const editors = [...document.querySelectorAll('.note-editor')];
          let editor = null;
          for (const node of editors) {
            if (h5.compareDocumentPosition(node) & Node.DOCUMENT_POSITION_FOLLOWING) {
              editor = node;
              break;
            }
          }
          if (!editor) return false;
          const editable = editor.querySelector('.note-editing-area .note-editable');
          const text = (editable?.innerText || '').replace(/\\s+/g, '');
          return needle.length > 0 && text.includes(needle);
        }
        """,
        snippet)
      .ConfigureAwait(false);
  }

  private static async Task<bool> TryWriteThreadedTopicCommentAsync(IPage page, string plain)
  {
    var addComment = page
      .Locator("a, button")
      .Filter(new LocatorFilterOptions { HasText = GetQuickerActionTopicsPage.AddCommentButtonText });

    if (await addComment.First.IsVisibleAsync().ConfigureAwait(false))
    {
      await addComment.First.ClickAsync().ConfigureAwait(false);
      await page.WaitForTimeoutAsync(800).ConfigureAwait(false);
    }

    var commentBox = page.Locator(GetQuickerActionTopicsPage.ReplyTextareaSelector);
    if (!await commentBox.IsVisibleAsync().ConfigureAwait(false))
    {
      return false;
    }

    await commentBox.ClickAsync().ConfigureAwait(false);
    await commentBox.FillAsync(string.Empty).ConfigureAwait(false);
    await commentBox.PressSequentiallyAsync(plain, new LocatorPressSequentiallyOptions { Delay = 15 })
      .ConfigureAwait(false);
    return true;
  }

  private static async Task<bool> TrySubmitTopicReplyAsync(IPage page, string expectedSnippet)
  {
    LastSubmitRequests.Clear();
    void OnRequest(object? _, IRequest request)
    {
      if (request.Method is "POST" or "PUT")
      {
        LastSubmitRequests.Add($"{request.Method} {request.Url}");
      }
    }

    page.Request += OnRequest;

    try
    {
      var submitBtn = page
        .Locator("button")
        .Filter(new LocatorFilterOptions { HasText = GetQuickerActionTopicsPage.SubmitReplyButtonText })
        .First;

      if (!await submitBtn.IsVisibleAsync().ConfigureAwait(false))
      {
        submitBtn = page
          .Locator("button, input[type='submit']")
          .Filter(new LocatorFilterOptions { HasTextRegex = new Regex("发表评论|发表回复") })
          .First;
      }

      if (!await submitBtn.IsEnabledAsync().ConfigureAwait(false))
      {
        return false;
      }

      var responseTask = page.WaitForResponseAsync(
        r => r.Request.Method is "POST" or "PUT",
        new PageWaitForResponseOptions { Timeout = 30_000 });

      await submitBtn.ClickAsync().ConfigureAwait(false);
      LastSubmitRequests.Add("submit:playwright-click");

      try
      {
        var response = await responseTask.ConfigureAwait(false);
        LastSubmitRequests.Add($"RESPONSE {response.Status} {response.Url}");
      }
      catch (TimeoutException)
      {
        // Continue to DOM verification.
      }

      try
      {
        await page
          .WaitForLoadStateAsync(LoadState.NetworkIdle, new PageWaitForLoadStateOptions { Timeout = 15_000 })
          .ConfigureAwait(false);
      }
      catch (TimeoutException)
      {
        // Continue.
      }

      await page.ReloadAsync(new PageReloadOptions { WaitUntil = WaitUntilState.DOMContentLoaded }).ConfigureAwait(false);
      await page.WaitForSelectorAsync("h1.topic-title", new PageWaitForSelectorOptions { Timeout = 30_000 })
        .ConfigureAwait(false);

      return await page
        .EvaluateAsync<bool>(
          "(snippet) => (document.body.innerText || '').includes(snippet)",
          expectedSnippet)
        .ConfigureAwait(false);
    }
    finally
    {
      page.Request -= OnRequest;
    }
  }

  private static async Task<string> ProbeReplyEditorStateAsync(IPage page) =>
    await page
      .EvaluateAsync<string>(
        """
        () => JSON.stringify({
          editorIndex: (() => {
            const h5 = [...document.querySelectorAll('h5')].find(h => (h.textContent || '').includes('回复主贴'));
            if (!h5) return -1;
            const editors = [...document.querySelectorAll('.note-editor')];
            for (let i = 0; i < editors.length; i++) {
              if (h5.compareDocumentPosition(editors[i]) & Node.DOCUMENT_POSITION_FOLLOWING) return i;
            }
            return -1;
          })(),
          officialEditable: (() => {
            const h5 = [...document.querySelectorAll('h5')].find(h => (h.textContent || '').includes('回复主贴'));
            if (!h5) return '';
            for (const editor of document.querySelectorAll('.note-editor')) {
              if (!(h5.compareDocumentPosition(editor) & Node.DOCUMENT_POSITION_FOLLOWING)) continue;
              return (editor.querySelector('.note-editing-area .note-editable')?.innerText || '').slice(0, 80);
            }
            return '';
          })(),
          draftCode: (() => {
            const h5 = [...document.querySelectorAll('h5')].find(h => (h.textContent || '').includes('回复主贴'));
            if (!h5) return '';
            for (const editor of document.querySelectorAll('.note-editor')) {
              if (!(h5.compareDocumentPosition(editor) & Node.DOCUMENT_POSITION_FOLLOWING)) continue;
              const jq = window['jQuery'] || window['$'];
              const ta = editor.querySelector('textarea');
              if (ta && jq?.fn?.summernote) return (jq(ta).summernote('code') || '').slice(0, 120);
            }
            return '';
          })(),
          submit: [...document.querySelectorAll('button')].filter(b => (b.textContent || '').includes('提交回复')).map(b => ({
            disabled: b.disabled,
            text: b.textContent.trim()
          }))
        })
        """)
      .ConfigureAwait(false);

  private static readonly Regex HtmlTagPattern = new("<[^>]+>", RegexOptions.Compiled);

  private static string StripHtmlToPlainForComment(string contentHtml)
  {
    if (!contentHtml.Contains('<', StringComparison.Ordinal))
    {
      return contentHtml;
    }

    var withBreaks = contentHtml
      .Replace("<br>", "\n", StringComparison.OrdinalIgnoreCase)
      .Replace("<br/>", "\n", StringComparison.OrdinalIgnoreCase)
      .Replace("<br />", "\n", StringComparison.OrdinalIgnoreCase)
      .Replace("</p>", "\n\n", StringComparison.OrdinalIgnoreCase)
      .Replace("</li>", "\n", StringComparison.OrdinalIgnoreCase);

    return System.Net.WebUtility.HtmlDecode(HtmlTagPattern.Replace(withBreaks, string.Empty)).Trim();
  }

  private static IReadOnlyList<ActionTopicListItem> ParseListItems(JsonElement raw, string sharedActionCode)
  {
    var items = new List<ActionTopicListItem>();
    if (raw.ValueKind != JsonValueKind.Array)
    {
      return items;
    }

    foreach (var el in raw.EnumerateArray())
    {
      var topicId = el.GetProperty("topicId").GetInt32();
      if (topicId <= 0)
      {
        continue;
      }

      var topicUrl = el.GetProperty("topicUrl").GetString();
      if (string.IsNullOrWhiteSpace(topicUrl))
      {
        topicUrl = GetQuickerActionTopicsPage.ExpandViewTopicUrl(topicId);
      }

      items.Add(new ActionTopicListItem(
        TopicId: topicId,
        Title: el.GetProperty("title").GetString() ?? string.Empty,
        TopicUrl: topicUrl,
        Category: GetNullableString(el, "category"),
        ViewCount: el.TryGetProperty("viewCount", out var vc) && vc.ValueKind == JsonValueKind.Number
          ? vc.GetInt32()
          : null,
        Author: GetNullableString(el, "author"),
        AuthorUrl: GetNullableString(el, "authorUrl"),
        CreatedAtText: GetNullableString(el, "createdAtText"),
        IsArchived: el.TryGetProperty("isArchived", out var ar) && ar.GetBoolean()));
    }

    _ = sharedActionCode;
    return items;
  }

  private static ActionTopicDetail ParseDetail(JsonElement raw)
  {
    var replies = new List<ActionTopicReply>();
    if (raw.TryGetProperty("replies", out var repliesEl) && repliesEl.ValueKind == JsonValueKind.Array)
    {
      foreach (var reply in repliesEl.EnumerateArray())
      {
        replies.Add(new ActionTopicReply(
          Index: reply.GetProperty("index").GetInt32(),
          Author: GetNullableString(reply, "author"),
          AuthorUrl: GetNullableString(reply, "authorUrl"),
          CreatedAtText: GetNullableString(reply, "createdAtText"),
          BodyHtml: reply.GetProperty("bodyHtml").GetString() ?? string.Empty,
          BodyText: reply.GetProperty("bodyText").GetString() ?? string.Empty,
          IsOriginalPost: reply.TryGetProperty("isOriginalPost", out var op) && op.GetBoolean()));
      }
    }

    var topicId = raw.GetProperty("topicId").GetInt32();
    IReadOnlyList<ActionTopicAuthorControl>? authorControls = null;
    if (raw.TryGetProperty("authorControls", out var controlsEl) && controlsEl.ValueKind == JsonValueKind.Array)
    {
      authorControls = controlsEl.EnumerateArray()
        .Select(c => new ActionTopicAuthorControl(
          c.GetProperty("text").GetString() ?? string.Empty,
          GetNullableString(c, "href")))
        .Where(c => c.Text.Length > 0)
        .ToList();
    }

    return new ActionTopicDetail(
      TopicId: topicId,
      Title: raw.GetProperty("title").GetString() ?? string.Empty,
      TopicUrl: raw.GetProperty("topicUrl").GetString()
                ?? GetQuickerActionTopicsPage.ExpandViewTopicUrl(topicId),
      Category: GetNullableString(raw, "category"),
      Author: GetNullableString(raw, "author"),
      AuthorUrl: GetNullableString(raw, "authorUrl"),
      CreatedAtText: GetNullableString(raw, "createdAtText"),
      SharedActionId: GetNullableString(raw, "sharedActionId"),
      SharedActionUrl: GetNullableString(raw, "sharedActionUrl"),
      SharedActionTitle: GetNullableString(raw, "sharedActionTitle"),
      BodyHtml: raw.GetProperty("bodyHtml").GetString() ?? string.Empty,
      BodyText: raw.GetProperty("bodyText").GetString() ?? string.Empty,
      Replies: replies,
      IsArchived: raw.TryGetProperty("isArchived", out var ar) && ar.GetBoolean(),
      CanArchive: raw.TryGetProperty("canArchive", out var ca) && ca.GetBoolean(),
      AuthorControls: authorControls);
  }

  private static string? GetNullableString(JsonElement parent, string propertyName) =>
    parent.TryGetProperty(propertyName, out var value) && value.ValueKind == JsonValueKind.String
      ? value.GetString()
      : null;

  private async Task<ActionTopicDetail> ReadTopicDetailAsync(IPage page, int topicId, CancellationToken cancellationToken)
  {
    _ = cancellationToken;
    var url = GetQuickerActionTopicsPage.ExpandViewTopicUrl(topicId);
    if (!page.Url.Contains($"/ViewTopic/{topicId}", StringComparison.OrdinalIgnoreCase))
    {
      await NavigateAsync(page, url, cancellationToken).ConfigureAwait(false);
    }

    if (page.Url.Contains("/Errors/404", StringComparison.OrdinalIgnoreCase))
    {
      throw new InvalidOperationException($"Topic #{topicId} was not found.");
    }

    await page
      .WaitForSelectorAsync("h1.topic-title", new PageWaitForSelectorOptions { Timeout = 30_000 })
      .ConfigureAwait(false);

    var raw = await page.EvaluateAsync<JsonElement>(DetailExtractScript).ConfigureAwait(false);
    return ParseDetail(raw);
  }

  private async Task<T> RunAuthenticatedReadAsync<T>(
    string email,
    string password,
    QuickerAgentSettings agentSettings,
    CancellationToken cancellationToken,
    Func<IPage, CancellationToken, Task<T>> action)
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
    catch (Exception ex) when (ex is not InvalidOperationException)
    {
      _logger.LogError(ex, "Action topics authenticated read failed.");
      throw new InvalidOperationException(ex.Message, ex);
    }
  }

  private async Task<T> RunReadOnlyAsync<T>(
    QuickerAgentSettings agentSettings,
    CancellationToken cancellationToken,
    Func<IPage, CancellationToken, Task<T>> action)
  {
    try
    {
      using var playwright = await Playwright.CreateAsync().ConfigureAwait(false);
      await using var session = await QuickerBrowserSession
        .CreateAsync(playwright, agentSettings, _loginService, _logger, cancellationToken)
        .ConfigureAwait(false);

      var page = await session.GetPageAsync(cancellationToken).ConfigureAwait(false);
      return await action(page, cancellationToken).ConfigureAwait(false);
    }
    catch (Exception ex) when (ex is not InvalidOperationException)
    {
      _logger.LogError(ex, "Action topics read failed.");
      throw new InvalidOperationException(ex.Message, ex);
    }
  }

  private async Task<ActionTopicsOperationResult> RunAuthenticatedAsync(
    string email,
    string password,
    QuickerAgentSettings agentSettings,
    CancellationToken cancellationToken,
    Func<IPage, CancellationToken, Task<ActionTopicsOperationResult>> action)
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
      _logger.LogError(ex, "Action topics mutation failed.");
      return ActionTopicsOperationResult.Fail("ACTION_TOPICS_OPERATION_FAILED", ex.Message);
    }
  }

  private static async Task NavigateAsync(IPage page, string url, CancellationToken cancellationToken)
  {
    _ = cancellationToken;
    await page
      .GotoAsync(url, new PageGotoOptions { Timeout = 60_000, WaitUntil = WaitUntilState.DOMContentLoaded })
      .ConfigureAwait(false);
  }

  private async Task<bool> OpenPageAsync(
    IPage page,
    string url,
    string email,
    string password,
    CancellationToken cancellationToken)
  {
    await NavigateAsync(page, url, cancellationToken).ConfigureAwait(false);

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

    await NavigateAsync(page, url, cancellationToken).ConfigureAwait(false);
    return !await _loginService.IsLoginPageAsync(page).ConfigureAwait(false);
  }
}
