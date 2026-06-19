using System.Text.Json;
using CommandLine;
using Microsoft.Extensions.Logging;
using QuickerAgent.Core;

namespace QuickerAgent.Console;

internal static partial class Program
{
  private static async Task<int> RunQaAsync(QaOptions options, ILoggerFactory loggerFactory)
  {
    LoadEnvironmentVariables();

    var verb = (options.Action ?? string.Empty).Trim().ToLowerInvariant();
    return verb switch
    {
      "post" => await RunQaPostAsync(options, loggerFactory).ConfigureAwait(false),
      "edit" => await RunQaEditAsync(options, loggerFactory).ConfigureAwait(false),
      _ => await UnknownQaVerbAsync(options).ConfigureAwait(false),
    };
  }

  private static async Task<int> UnknownQaVerbAsync(QaOptions options)
  {
    await EmitErrorAsync(
        options.Json,
        "UNKNOWN_QA_VERB",
        "Use: qa post|edit ... (qa post --title ... --category ... --content ... | qa edit --id <questionId> [--title ...] [--content ...]) [--json]")
      .ConfigureAwait(false);
    return ExitCodes.Error;
  }

  private static async Task<int> RunQaPostAsync(QaOptions options, ILoggerFactory loggerFactory)
  {
    if (string.IsNullOrWhiteSpace(options.Title))
    {
      await EmitErrorAsync(options.Json, "MISSING_TITLE", "Provide --title <text>.").ConfigureAwait(false);
      return ExitCodes.Error;
    }

    if (string.IsNullOrWhiteSpace(options.Category))
    {
      await EmitErrorAsync(options.Json, "MISSING_CATEGORY", $"Provide --category <id|name>. Valid: {GetQuickerQaPage.FormatCategoryList()}")
        .ConfigureAwait(false);
      return ExitCodes.Error;
    }

    if (!GetQuickerQaPage.TryResolveCategoryId(options.Category, out var categoryId))
    {
      await EmitErrorAsync(
          options.Json,
          "INVALID_CATEGORY",
          $"Unknown category '{options.Category}'. Valid: {GetQuickerQaPage.FormatCategoryList()}")
        .ConfigureAwait(false);
      return ExitCodes.Error;
    }

    string contentRaw;
    try
    {
      contentRaw = await ResolveQaContentAsync(options).ConfigureAwait(false);
    }
    catch (Exception ex)
    {
      await EmitErrorAsync(options.Json, "CONTENT_READ_ERROR", ex.Message).ConfigureAwait(false);
      return ExitCodes.Error;
    }

    if (string.IsNullOrWhiteSpace(contentRaw))
    {
      await EmitErrorAsync(options.Json, "MISSING_CONTENT", "Provide --content <html> or --content-file <path>.")
        .ConfigureAwait(false);
      return ExitCodes.Error;
    }

    if (!TryGetCredentials(options.Json, out var email, out var password))
    {
      return ExitCodes.Error;
    }

    var agentSettings = QuickerAgentSettings.FromEnvironment();
    var service = CreateQaService(loggerFactory);

    var request = new QaPostRequest
    {
      Title = options.Title.Trim(),
      CategoryId = categoryId,
      ContentHtml = QaPostService.NormalizeContentHtml(contentRaw),
      Keywords = string.IsNullOrWhiteSpace(options.Keywords) ? null : options.Keywords.Trim(),
    };

    var result = await service
      .PostTopicAsync(email, password, request, agentSettings, CancellationToken.None)
      .ConfigureAwait(false);

    if (!result.Ok)
    {
      await EmitErrorAsync(options.Json, result.ErrorCode ?? "QA_POST_FAILED", result.Message ?? "QA post failed.")
        .ConfigureAwait(false);
      return ExitCodes.Error;
    }

    if (options.Json)
    {
      global::System.Console.WriteLine(JsonSerializer.Serialize(
        new
        {
          ok = true,
          action = "qa-post",
          questionId = result.QuestionId,
          questionUrl = result.QuestionUrl,
          title = request.Title,
          categoryId = request.CategoryId,
          categoryName = GetQuickerQaPage.CategoryNames[request.CategoryId],
          headless = agentSettings.Headless,
          profileDirectory = agentSettings.ProfileDirectory,
        },
        JsonWriteOptions));
    }
    else
    {
      global::System.Console.WriteLine($"Posted QA topic #{result.QuestionId}: {result.QuestionUrl}");
    }

    return ExitCodes.Success;
  }

  private static async Task<int> RunQaEditAsync(QaOptions options, ILoggerFactory loggerFactory)
  {
    if (string.IsNullOrWhiteSpace(options.Id))
    {
      await EmitErrorAsync(
          options.Json,
          "MISSING_QUESTION_ID",
          "Provide --id <questionId> or question URL (e.g. 40752 or https://getquicker.net/QA/Question/40752).")
        .ConfigureAwait(false);
      return ExitCodes.Error;
    }

    if (!GetQuickerQaPage.TryParseQuestionId(options.Id, out var questionId))
    {
      await EmitErrorAsync(options.Json, "INVALID_QUESTION_ID", $"Could not parse question id from '{options.Id}'.")
        .ConfigureAwait(false);
      return ExitCodes.Error;
    }

    int? categoryId = null;
    if (!string.IsNullOrWhiteSpace(options.Category))
    {
      if (!GetQuickerQaPage.TryResolveCategoryId(options.Category, out var resolved))
      {
        await EmitErrorAsync(
            options.Json,
            "INVALID_CATEGORY",
            $"Unknown category '{options.Category}'. Valid: {GetQuickerQaPage.FormatCategoryList()}")
          .ConfigureAwait(false);
        return ExitCodes.Error;
      }

      categoryId = resolved;
    }

    string? contentRaw = null;
    var hasContentInput = !string.IsNullOrWhiteSpace(options.Content)
                          || !string.IsNullOrWhiteSpace(options.ContentFile);
    if (hasContentInput)
    {
      try
      {
        contentRaw = await ResolveQaContentAsync(options).ConfigureAwait(false);
      }
      catch (Exception ex)
      {
        await EmitErrorAsync(options.Json, "CONTENT_READ_ERROR", ex.Message).ConfigureAwait(false);
        return ExitCodes.Error;
      }

      if (string.IsNullOrWhiteSpace(contentRaw))
      {
        await EmitErrorAsync(options.Json, "MISSING_CONTENT", "Content from --content or --content-file is empty.")
          .ConfigureAwait(false);
        return ExitCodes.Error;
      }

      contentRaw = QaPostService.NormalizeContentHtml(contentRaw);
    }

    var request = new QaEditRequest
    {
      QuestionId = questionId,
      Title = string.IsNullOrWhiteSpace(options.Title) ? null : options.Title.Trim(),
      CategoryId = categoryId,
      ContentHtml = contentRaw,
      Keywords = options.Keywords,
    };

    if (!request.HasAnyField)
    {
      await EmitErrorAsync(
          options.Json,
          "NO_FIELDS_TO_UPDATE",
          "Provide at least one of --title, --category, --content/--content-file, or --keywords.")
        .ConfigureAwait(false);
      return ExitCodes.Error;
    }

    if (!TryGetCredentials(options.Json, out var email, out var password))
    {
      return ExitCodes.Error;
    }

    var agentSettings = QuickerAgentSettings.FromEnvironment();
    var service = CreateQaService(loggerFactory);

    var result = await service
      .UpdateTopicAsync(email, password, request, agentSettings, CancellationToken.None)
      .ConfigureAwait(false);

    if (!result.Ok)
    {
      await EmitErrorAsync(options.Json, result.ErrorCode ?? "QA_EDIT_FAILED", result.Message ?? "QA edit failed.")
        .ConfigureAwait(false);
      return ExitCodes.Error;
    }

    if (options.Json)
    {
      global::System.Console.WriteLine(JsonSerializer.Serialize(
        new
        {
          ok = true,
          action = "qa-edit",
          questionId = result.QuestionId,
          questionUrl = result.QuestionUrl,
          updatedTitle = request.Title,
          updatedCategoryId = request.CategoryId,
          updatedCategoryName = request.CategoryId is int cid ? GetQuickerQaPage.CategoryNames[cid] : null,
          contentUpdated = request.ContentHtml is not null,
          keywordsUpdated = request.Keywords is not null,
          headless = agentSettings.Headless,
          profileDirectory = agentSettings.ProfileDirectory,
        },
        JsonWriteOptions));
    }
    else
    {
      global::System.Console.WriteLine($"Updated QA topic #{result.QuestionId}: {result.QuestionUrl}");
    }

    return ExitCodes.Success;
  }

  private static QaPostService CreateQaService(ILoggerFactory loggerFactory)
  {
    var loginLogger = loggerFactory.CreateLogger<QuickerWebLoginService>();
    var qaLogger = loggerFactory.CreateLogger<QaPostService>();
    return new QaPostService(new QuickerWebLoginService(loginLogger), qaLogger);
  }

  private static async Task<string> ResolveQaContentAsync(QaOptions options)
  {
    var hasInline = !string.IsNullOrWhiteSpace(options.Content);
    var hasFile = !string.IsNullOrWhiteSpace(options.ContentFile);

    if (hasInline && hasFile)
    {
      throw new InvalidOperationException("Provide only one of --content or --content-file.");
    }

    if (hasFile)
    {
      var path = Path.GetFullPath(options.ContentFile!);
      if (!File.Exists(path))
      {
        throw new FileNotFoundException($"Content file not found: {path}");
      }

      return await File.ReadAllTextAsync(path, Utf8NoBom).ConfigureAwait(false);
    }

    if (hasInline)
    {
      return options.Content!;
    }

    return string.Empty;
  }
}

[Verb("qa", HelpText = "Create or edit topics on getquicker.net QA forum.")]
public sealed class QaOptions
{
  [Value(0, MetaName = "command", Required = true, HelpText = "post | edit")]
  public string? Action { get; set; }

  [Option('i', "id", HelpText = "Question id or URL for edit (e.g. 40752).")]
  public string? Id { get; set; }

  [Option("title", HelpText = "Topic title.")]
  public string? Title { get; set; }

  [Option("category", HelpText = "Category id or Chinese name (e.g. 4 or 功能建议).")]
  public string? Category { get; set; }

  [Option("content", HelpText = "Topic body (HTML or plain text).")]
  public string? Content { get; set; }

  [Option("content-file", HelpText = "Path to HTML or plain-text body file.")]
  public string? ContentFile { get; set; }

  [Option("keywords", HelpText = "Optional comma-separated keywords.")]
  public string? Keywords { get; set; }

  [Option("json", HelpText = "Emit JSON for automation.")]
  public bool Json { get; set; }
}
