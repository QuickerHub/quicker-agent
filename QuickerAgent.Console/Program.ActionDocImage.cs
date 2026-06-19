using System.Text.Json;
using Microsoft.Extensions.Logging;
using QuickerAgent.Core;

namespace QuickerAgent.Console;

internal static partial class Program
{
  private static async Task<int> RunActionDocImageAsync(ActionDocOptions options, ILoggerFactory loggerFactory)
  {
    if (string.IsNullOrWhiteSpace(options.Code))
    {
      await EmitErrorAsync(
          options.Json,
          "MISSING_CODE",
          "Provide --code <sharedId> for action-doc image.")
        .ConfigureAwait(false);
      return ExitCodes.Error;
    }

    if (string.IsNullOrWhiteSpace(options.File))
    {
      await EmitErrorAsync(
          options.Json,
          "MISSING_FILE",
          "Provide --file <path> to a local image file.")
        .ConfigureAwait(false);
      return ExitCodes.Error;
    }

    var store = ActionLocalStore.FromEnvironment();
    var sharedId = options.Code.Trim();
    var pagePath = store.GetPageHtmlPath(sharedId);

    if (!File.Exists(pagePath))
    {
      await EmitErrorAsync(
          options.Json,
          "PAGE_HTML_NOT_FOUND",
          $"No page.html at '{pagePath}'. Run: qkagent pull --code {sharedId}")
        .ConfigureAwait(false);
      return ExitCodes.Error;
    }

    if (!TryGetCredentials(options.Json, out var email, out var password))
    {
      return ExitCodes.Error;
    }

    var agentSettings = QuickerAgentSettings.FromEnvironment();
    var loginLogger = loggerFactory.CreateLogger<QuickerWebLoginService>();
    var uploadLogger = loggerFactory.CreateLogger<ActionImageUploadService>();
    var uploadService = new ActionImageUploadService(new QuickerWebLoginService(loginLogger), uploadLogger);

    var uploadResult = await uploadService
      .UploadImageAsync(email, password, sharedId, options.File, agentSettings, CancellationToken.None)
      .ConfigureAwait(false);

    if (!uploadResult.Ok || string.IsNullOrWhiteSpace(uploadResult.Html))
    {
      await EmitErrorAsync(
          options.Json,
          uploadResult.ErrorCode ?? "IMAGE_UPLOAD_FAILED",
          uploadResult.Message ?? "Image upload failed.")
        .ConfigureAwait(false);
      return ExitCodes.Error;
    }

    var cdnUrl = uploadResult.Html;
    string pageHtml;
    try
    {
      pageHtml = await File.ReadAllTextAsync(pagePath, Utf8NoBom).ConfigureAwait(false);
    }
    catch (Exception ex)
    {
      await EmitErrorAsync(options.Json, "PAGE_HTML_READ_ERROR", ex.Message).ConfigureAwait(false);
      return ExitCodes.Error;
    }

    if (!ActionDocPageHtml.TryReplaceImageSrc(
          pageHtml,
          cdnUrl,
          options.ImageIndex,
          options.Alt,
          out var updatedHtml,
          out var replaceError))
    {
      await EmitErrorAsync(options.Json, "IMAGE_REPLACE_FAILED", replaceError ?? "Could not replace <img> src.")
        .ConfigureAwait(false);
      return ExitCodes.Error;
    }

    try
    {
      await WriteHtmlFileAsync(pagePath, updatedHtml).ConfigureAwait(false);
    }
    catch (Exception ex)
    {
      await EmitErrorAsync(options.Json, "PAGE_HTML_WRITE_ERROR", ex.Message).ConfigureAwait(false);
      return ExitCodes.Error;
    }

    if (options.Push)
    {
      return await RunActionDocPushAsync(options, loggerFactory).ConfigureAwait(false);
    }

    if (options.Json)
    {
      global::System.Console.WriteLine(JsonSerializer.Serialize(
        new
        {
          ok = true,
          action = "image",
          sharedId,
          imageUrl = cdnUrl,
          pageHtmlPath = pagePath,
          imageIndex = options.ImageIndex,
          alt = options.Alt,
          pushed = false,
          headless = agentSettings.Headless,
          profileDirectory = agentSettings.ProfileDirectory,
        },
        JsonWriteOptions));
    }
    else
    {
      global::System.Console.WriteLine($"Uploaded image and updated page.html for {sharedId}.");
      global::System.Console.WriteLine($"CDN URL: {cdnUrl}");
      global::System.Console.WriteLine($"Updated: {pagePath}");
      global::System.Console.WriteLine($"Run: qkagent push --code {sharedId}");
    }

    return ExitCodes.Success;
  }
}
