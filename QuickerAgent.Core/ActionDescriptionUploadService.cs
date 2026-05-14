using Microsoft.Extensions.Logging;
using Microsoft.Playwright;

namespace QuickerAgent.Core;

/// <summary>
/// Connects over CDP, logs in, opens the shared-action page, fills Summernote HTML, and saves.
/// </summary>
public sealed class ActionDescriptionUploadService
{
    private readonly QuickerWebLoginService _loginService;
    private readonly ILogger<ActionDescriptionUploadService> _logger;

    public ActionDescriptionUploadService(
        QuickerWebLoginService loginService,
        ILogger<ActionDescriptionUploadService> logger)
    {
        _loginService = loginService;
        _logger = logger;
    }

    public async Task<ActionDescriptionUploadResult> UploadHtmlAsync(
        string cdpUrl,
        string email,
        string password,
        string sharedActionCode,
        string htmlContent,
        ActionDescriptionUploadSettings settings,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(cdpUrl);
        ArgumentException.ThrowIfNullOrEmpty(email);
        ArgumentException.ThrowIfNullOrEmpty(password);
        ArgumentException.ThrowIfNullOrWhiteSpace(sharedActionCode);
        ArgumentNullException.ThrowIfNull(htmlContent);
        ArgumentNullException.ThrowIfNull(settings);

        try
        {
            using var playwright = await Playwright.CreateAsync().ConfigureAwait(false);
            await using var browser = await playwright.Chromium.ConnectOverCDPAsync(cdpUrl).ConfigureAwait(false);

            var context = browser.Contexts.Count > 0
                ? browser.Contexts[0]
                : await browser.NewContextAsync().ConfigureAwait(false);

            var page = context.Pages.Count > 0
                ? context.Pages[0]
                : await context.NewPageAsync().ConfigureAwait(false);

            page.SetDefaultTimeout(60_000);
            page.SetDefaultNavigationTimeout(60_000);

            _logger.LogInformation("Logging in before opening action doc page.");
            var loggedIn = await _loginService.LoginAsync(page, email, password, cancellationToken).ConfigureAwait(false);
            if (!loggedIn)
            {
                return ActionDescriptionUploadResult.Fail("LOGIN_FAILED", "Quicker login did not complete.");
            }

            var targetUrl = ActionDescriptionUploadSettings.ExpandPageUrl(settings.PageUrlTemplate, sharedActionCode);
            _logger.LogInformation("Opening {Url}", targetUrl);
            await page.GotoAsync(
                    targetUrl,
                    new PageGotoOptions { WaitUntil = WaitUntilState.NetworkIdle, Timeout = 90_000 })
                .ConfigureAwait(false);

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
                await page.WaitForSelectorAsync(
                        settings.EditorWaitSelector,
                        new PageWaitForSelectorOptions { State = WaitForSelectorState.Visible, Timeout = 45_000 })
                    .ConfigureAwait(false);
            }
            catch (TimeoutException)
            {
                return ActionDescriptionUploadResult.Fail(
                    "EDITOR_NOT_FOUND",
                    $"No visible editor matched '{settings.EditorWaitSelector}'. " +
                    "Log in as the action owner, or set QKAGENT_ACTION_DOC_PAGE_URL / QKAGENT_ACTION_DOC_OPEN_EDITOR_SELECTOR / QKAGENT_ACTION_DOC_EDITOR_SELECTOR.");
            }

            var mode = await page.EvaluateAsync<string?>(
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

            if (string.IsNullOrEmpty(mode))
            {
                return ActionDescriptionUploadResult.Fail(
                    "EDITOR_NOT_FOUND",
                    "Summernote / .note-editable not found after wait. Adjust QKAGENT_ACTION_DOC_EDITOR_SELECTOR.");
            }

            _logger.LogInformation("Injected HTML via {Mode}.", mode);

            var saved = await TryClickSaveAsync(page, settings, cancellationToken).ConfigureAwait(false);
            if (!saved)
            {
                return ActionDescriptionUploadResult.Fail(
                    "SAVE_BUTTON_NOT_FOUND",
                    $"Could not find save control (name '{settings.SaveButtonAccessibleName}' or QKAGENT_ACTION_DOC_SAVE_SELECTOR).");
            }

            await page.WaitForLoadStateAsync(LoadState.NetworkIdle).ConfigureAwait(false);
            return ActionDescriptionUploadResult.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "action-doc upload failed.");
            return ActionDescriptionUploadResult.Fail("UPLOAD_ERROR", ex.Message);
        }
    }

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

        var byName = page.GetByRole(AriaRole.Button, new PageGetByRoleOptions { Name = settings.SaveButtonAccessibleName })
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
