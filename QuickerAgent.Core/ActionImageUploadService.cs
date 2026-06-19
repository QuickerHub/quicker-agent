using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Playwright;

namespace QuickerAgent.Core;

/// <summary>
/// Uploads images for shared-action intro docs to getquicker.net CDN.
/// </summary>
public sealed class ActionImageUploadService
{
  private readonly QuickerWebLoginService _loginService;
  private readonly ILogger<ActionImageUploadService> _logger;

  public ActionImageUploadService(
    QuickerWebLoginService loginService,
    ILogger<ActionImageUploadService> logger)
  {
    _loginService = loginService;
    _logger = logger;
  }

  public Task<ActionDocOperationResult> UploadImageAsync(
    string email,
    string password,
    string sharedActionCode,
    string localImagePath,
    QuickerAgentSettings agentSettings,
    CancellationToken cancellationToken = default)
  {
    ArgumentException.ThrowIfNullOrEmpty(email);
    ArgumentException.ThrowIfNullOrEmpty(password);
    ArgumentException.ThrowIfNullOrWhiteSpace(sharedActionCode);
    ArgumentException.ThrowIfNullOrWhiteSpace(localImagePath);
    ArgumentNullException.ThrowIfNull(agentSettings);

    var fullPath = Path.GetFullPath(localImagePath);
    if (!File.Exists(fullPath))
    {
      return Task.FromResult(ActionDocOperationResult.Fail("IMAGE_NOT_FOUND", $"Image file not found: {fullPath}"));
    }

    return UploadImageCoreAsync(email, password, sharedActionCode, fullPath, agentSettings, cancellationToken);
  }

  private async Task<ActionDocOperationResult> UploadImageCoreAsync(
    string email,
    string password,
    string sharedActionCode,
    string fullPath,
    QuickerAgentSettings agentSettings,
    CancellationToken cancellationToken)
  {
    try
    {
      using var playwright = await Playwright.CreateAsync().ConfigureAwait(false);
      await using var session = await QuickerBrowserSession
        .CreateAsync(playwright, agentSettings, _loginService, _logger, cancellationToken)
        .ConfigureAwait(false);

      await session.EnsureLoggedInAsync(email, password, cancellationToken).ConfigureAwait(false);
      var page = await session.GetPageAsync(cancellationToken).ConfigureAwait(false);

      var editorReady = await NavigateToEditPageAsync(page, sharedActionCode, cancellationToken).ConfigureAwait(false);
      if (!editorReady.Ok)
      {
        return editorReady;
      }

      var cdnUrl = await UploadViaSummernoteAsync(page, fullPath, cancellationToken).ConfigureAwait(false);
      if (string.IsNullOrWhiteSpace(cdnUrl))
      {
        return ActionDocOperationResult.Fail("IMAGE_UPLOAD_FAILED", "Could not upload image to getquicker.net.");
      }

      _logger.LogInformation("Uploaded image to {Url}", cdnUrl);
      return ActionDocOperationResult.Success(cdnUrl);
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Image upload failed.");
      return ActionDocOperationResult.Fail("IMAGE_UPLOAD_ERROR", ex.Message);
    }
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

  private async Task<string?> UploadViaSummernoteAsync(
    IPage page,
    string localImagePath,
    CancellationToken cancellationToken)
  {
    var uploadResponseTask = page.WaitForResponseAsync(
      response => response.Url.Contains("/site/upload", StringComparison.OrdinalIgnoreCase)
                  && string.Equals(response.Request.Method, "POST", StringComparison.OrdinalIgnoreCase),
      new PageWaitForResponseOptions { Timeout = 60_000 });

    await page
      .Locator(GetQuickerActionDocPage.ImageToolbarButtonSelector)
      .First
      .ClickAsync(new LocatorClickOptions { Timeout = 30_000 })
      .ConfigureAwait(false);

    var fileInput = page.Locator(GetQuickerActionDocPage.ImageFileInputSelector).Last;
    await fileInput
      .WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Attached, Timeout = 15_000 })
      .ConfigureAwait(false);

    cancellationToken.ThrowIfCancellationRequested();
    await fileInput.SetInputFilesAsync(localImagePath).ConfigureAwait(false);

    var response = await uploadResponseTask.ConfigureAwait(false);
    if (!response.Ok)
    {
      _logger.LogWarning("Image upload HTTP {Status} for {Url}", response.Status, response.Url);
      return null;
    }

    var body = (await response.TextAsync().ConfigureAwait(false)).Trim();
    return ParseUploadResponse(body);
  }

  internal static string? ParseUploadResponse(string body)
  {
    if (string.IsNullOrWhiteSpace(body))
    {
      return null;
    }

    if (body.StartsWith('"') && body.EndsWith('"'))
    {
      try
      {
        return JsonSerializer.Deserialize<string>(body);
      }
      catch (JsonException)
      {
        return body.Trim('"').Replace("\\\"", "\"", StringComparison.Ordinal);
      }
    }

    if (body.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
        || body.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
    {
      return body;
    }

    return null;
  }
}
