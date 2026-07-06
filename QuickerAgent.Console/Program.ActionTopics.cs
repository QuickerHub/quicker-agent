using System.Text.Json;
using CommandLine;
using Microsoft.Extensions.Logging;
using QuickerAgent.Core;

namespace QuickerAgent.Console;

internal static partial class Program
{
  private static async Task<int> RunActionTopicsAsync(ActionTopicsOptions options, ILoggerFactory loggerFactory)
  {
    LoadEnvironmentVariables();

    var verb = (options.Action ?? string.Empty).Trim().ToLowerInvariant();
    return verb switch
    {
      "list" => await RunActionTopicsListAsync(options, loggerFactory).ConfigureAwait(false),
      "get" => await RunActionTopicsGetAsync(options, loggerFactory).ConfigureAwait(false),
      "reply" => await RunActionTopicsReplyAsync(options, loggerFactory).ConfigureAwait(false),
      "archive" => await RunActionTopicsArchiveAsync(options, loggerFactory).ConfigureAwait(false),
      "mark" => await RunActionTopicsMarkAsync(options, loggerFactory).ConfigureAwait(false),
      _ => await UnknownActionTopicsVerbAsync(options).ConfigureAwait(false),
    };
  }

  private static async Task<int> UnknownActionTopicsVerbAsync(ActionTopicsOptions options)
  {
    await EmitErrorAsync(
        options.Json,
        "UNKNOWN_ACTION_TOPICS_VERB",
        "Use: action-topics list|get|reply|archive|mark ... [--json]")
      .ConfigureAwait(false);
    return ExitCodes.Error;
  }

  private static async Task<int> RunActionTopicsListAsync(
    ActionTopicsOptions options,
    ILoggerFactory loggerFactory)
  {
    if (!TryResolveSharedActionCode(options, out var sharedActionCode))
    {
      await EmitErrorAsync(
          options.Json,
          "MISSING_CODE",
          "Provide --code <sharedActionId|Topics URL|Sharedaction URL>.")
        .ConfigureAwait(false);
      return ExitCodes.Error;
    }

    var agentSettings = QuickerAgentSettings.FromEnvironment();
    var service = CreateActionTopicsService(loggerFactory);

    IReadOnlyList<ActionTopicListItem> items;
    try
    {
      items = await service
        .ListTopicsAsync(sharedActionCode, options.IncludeArchived, agentSettings, CancellationToken.None)
        .ConfigureAwait(false);
    }
    catch (Exception ex)
    {
      await EmitErrorAsync(options.Json, "LIST_FAILED", ex.Message).ConfigureAwait(false);
      return ExitCodes.Error;
    }

    if (options.Json)
    {
      global::System.Console.WriteLine(JsonSerializer.Serialize(
        new
        {
          ok = true,
          action = "action-topics-list",
          sharedActionCode,
          includeArchived = options.IncludeArchived,
          count = items.Count,
          topics = items,
          headless = agentSettings.Headless,
          profileDirectory = agentSettings.ProfileDirectory,
        },
        JsonWriteOptions));
    }
    else
    {
      foreach (var item in items)
      {
        global::System.Console.WriteLine(
          $"#{item.TopicId} [{item.Category}] {item.Title} ({item.Author}, {item.CreatedAtText})");
      }
    }

    return ExitCodes.Success;
  }

  private static async Task<int> RunActionTopicsGetAsync(
    ActionTopicsOptions options,
    ILoggerFactory loggerFactory)
  {
    if (!TryResolveTopicId(options, out var topicId))
    {
      await EmitErrorAsync(
          options.Json,
          "MISSING_TOPIC_ID",
          "Provide --id <topicId|ViewTopic URL>.")
        .ConfigureAwait(false);
      return ExitCodes.Error;
    }

    var agentSettings = QuickerAgentSettings.FromEnvironment();
    var service = CreateActionTopicsService(loggerFactory);

    string? email = null;
    string? password = null;
    if (options.Login)
    {
      if (!TryGetCredentials(options.Json, out email, out password))
      {
        return ExitCodes.Error;
      }
    }

    ActionTopicDetail detail;
    try
    {
      detail = await service
        .GetTopicAsync(topicId, email, password, agentSettings, CancellationToken.None)
        .ConfigureAwait(false);
    }
    catch (Exception ex)
    {
      await EmitErrorAsync(options.Json, "GET_FAILED", ex.Message).ConfigureAwait(false);
      return ExitCodes.Error;
    }

    var suggestedLabels = GetQuickerActionTopicsPage.SuggestIssueLabels(detail.Category);

    if (options.Json)
    {
      global::System.Console.WriteLine(JsonSerializer.Serialize(
        new
        {
          ok = true,
          action = "action-topics-get",
          topic = detail,
          suggestedIssueLabels = suggestedLabels,
          headless = agentSettings.Headless,
          profileDirectory = agentSettings.ProfileDirectory,
        },
        JsonWriteOptions));
    }
    else
    {
      global::System.Console.WriteLine($"#{detail.TopicId} {detail.Title}");
      global::System.Console.WriteLine($"Category: {detail.Category}");
      global::System.Console.WriteLine($"Author: {detail.Author}");
      global::System.Console.WriteLine(detail.BodyText);
    }

    return ExitCodes.Success;
  }

  private static async Task<int> RunActionTopicsReplyAsync(
    ActionTopicsOptions options,
    ILoggerFactory loggerFactory)
  {
    if (!TryResolveTopicId(options, out var topicId))
    {
      await EmitErrorAsync(options.Json, "MISSING_TOPIC_ID", "Provide --id <topicId|ViewTopic URL>.")
        .ConfigureAwait(false);
      return ExitCodes.Error;
    }

    string contentRaw;
    try
    {
      contentRaw = await ResolveActionTopicsContentAsync(options).ConfigureAwait(false);
    }
    catch (Exception ex)
    {
      await EmitErrorAsync(options.Json, "CONTENT_READ_ERROR", ex.Message).ConfigureAwait(false);
      return ExitCodes.Error;
    }

    if (string.IsNullOrWhiteSpace(contentRaw))
    {
      await EmitErrorAsync(options.Json, "MISSING_CONTENT", "Provide --content or --content-file.")
        .ConfigureAwait(false);
      return ExitCodes.Error;
    }

    if (!TryGetCredentials(options.Json, out var email, out var password))
    {
      return ExitCodes.Error;
    }

    var agentSettings = QuickerAgentSettings.FromEnvironment();
    var service = CreateActionTopicsService(loggerFactory);
    var result = await service
      .ReplyAsync(email, password, topicId, contentRaw, agentSettings, CancellationToken.None)
      .ConfigureAwait(false);

    return await EmitActionTopicsMutationResultAsync(options.Json, "action-topics-reply", result, agentSettings)
      .ConfigureAwait(false);
  }

  private static async Task<int> RunActionTopicsArchiveAsync(
    ActionTopicsOptions options,
    ILoggerFactory loggerFactory)
  {
    if (!TryResolveTopicId(options, out var topicId))
    {
      await EmitErrorAsync(options.Json, "MISSING_TOPIC_ID", "Provide --id <topicId|ViewTopic URL>.")
        .ConfigureAwait(false);
      return ExitCodes.Error;
    }

    if (!TryGetCredentials(options.Json, out var email, out var password))
    {
      return ExitCodes.Error;
    }

    var agentSettings = QuickerAgentSettings.FromEnvironment();
    var service = CreateActionTopicsService(loggerFactory);
    var result = await service
      .ArchiveAsync(email, password, topicId, agentSettings, CancellationToken.None)
      .ConfigureAwait(false);

    return await EmitActionTopicsMutationResultAsync(options.Json, "action-topics-archive", result, agentSettings)
      .ConfigureAwait(false);
  }

  private static async Task<int> RunActionTopicsMarkAsync(
    ActionTopicsOptions options,
    ILoggerFactory loggerFactory)
  {
    var status = (options.Status ?? "handled").Trim().ToLowerInvariant();
    if (status is not ("handled" or "archive" or "archived"))
    {
      await EmitErrorAsync(
          options.Json,
          "INVALID_STATUS",
          "mark supports --status handled (alias for archive).")
        .ConfigureAwait(false);
      return ExitCodes.Error;
    }

    return await RunActionTopicsArchiveAsync(options, loggerFactory).ConfigureAwait(false);
  }

  private static async Task<int> EmitActionTopicsMutationResultAsync(
    bool json,
    string actionName,
    ActionTopicsOperationResult result,
    QuickerAgentSettings agentSettings)
  {
    if (!result.Ok)
    {
      await EmitErrorAsync(json, result.ErrorCode ?? "ACTION_TOPICS_FAILED", result.Message ?? "Operation failed.")
        .ConfigureAwait(false);
      return ExitCodes.Error;
    }

    if (json)
    {
      global::System.Console.WriteLine(JsonSerializer.Serialize(
        new
        {
          ok = true,
          action = actionName,
          topicId = result.TopicId,
          topicUrl = result.TopicUrl,
          headless = agentSettings.Headless,
          profileDirectory = agentSettings.ProfileDirectory,
        },
        JsonWriteOptions));
    }
    else
    {
      global::System.Console.WriteLine($"OK topic #{result.TopicId}: {result.TopicUrl}");
    }

    return ExitCodes.Success;
  }

  private static ActionTopicsService CreateActionTopicsService(ILoggerFactory loggerFactory)
  {
    var loginLogger = loggerFactory.CreateLogger<QuickerWebLoginService>();
    var topicsLogger = loggerFactory.CreateLogger<ActionTopicsService>();
    return new ActionTopicsService(new QuickerWebLoginService(loginLogger), topicsLogger);
  }

  private static bool TryResolveSharedActionCode(ActionTopicsOptions options, out string sharedActionCode)
  {
    sharedActionCode = string.Empty;
    if (string.IsNullOrWhiteSpace(options.Code))
    {
      return false;
    }

    return GetQuickerActionTopicsPage.TryParseSharedActionCode(options.Code, out sharedActionCode);
  }

  private static bool TryResolveTopicId(ActionTopicsOptions options, out int topicId)
  {
    topicId = 0;
    if (string.IsNullOrWhiteSpace(options.Id))
    {
      return false;
    }

    return GetQuickerActionTopicsPage.TryParseTopicId(options.Id, out topicId);
  }

  private static async Task<string> ResolveActionTopicsContentAsync(ActionTopicsOptions options)
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

    return options.Content ?? string.Empty;
  }
}

[Verb("action-topics", HelpText = "List, read, reply, archive shared-action discussion topics on getquicker.net.")]
public sealed class ActionTopicsOptions
{
  [Value(0, MetaName = "command", Required = true, HelpText = "list | get | reply | archive | mark")]
  public string? Action { get; set; }

  [Option("code", HelpText = "Shared action id or Topics/Sharedaction URL (for list).")]
  public string? Code { get; set; }

  [Option('i', "id", HelpText = "Topic id or ViewTopic URL.")]
  public string? Id { get; set; }

  [Option("content", HelpText = "Reply body (HTML or plain text).")]
  public string? Content { get; set; }

  [Option("content-file", HelpText = "Path to reply body file.")]
  public string? ContentFile { get; set; }

  [Option("include-archived", HelpText = "Include archived topics when listing.")]
  public bool IncludeArchived { get; set; }

  [Option("status", HelpText = "For mark: handled (archives topic).")]
  public string? Status { get; set; }

  [Option("login", HelpText = "Sign in before reading (exposes author-only controls like archive).")]
  public bool Login { get; set; }

  [Option("json", HelpText = "Emit JSON for automation.")]
  public bool Json { get; set; }
}
