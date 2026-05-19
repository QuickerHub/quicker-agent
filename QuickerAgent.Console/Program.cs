using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using CommandLine;
using DotNetEnv;
using Microsoft.Extensions.Logging;
using QuickerAgent.Core;

namespace QuickerAgent.Console;

/// <summary>
/// Exit codes for agent / scripting.
/// </summary>
public static class ExitCodes
{
  public const int Success = 0;
  public const int Error = 1;
}

internal static class Program
{
  private static readonly UTF8Encoding Utf8NoBom = new(encoderShouldEmitUTF8Identifier: false);

  private static readonly JsonSerializerOptions JsonWriteOptions = new()
  {
    WriteIndented = false,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
  };

  private static async Task<int> Main(string[] args)
  {
    ConfigureConsoleUtf8();
    LoadEnvironmentVariables();

    using var loggerFactory = LoggerFactory.Create(static b =>
    {
      b.SetMinimumLevel(LogLevel.Information);
      b.AddSimpleConsole(o =>
      {
        o.SingleLine = true;
        o.TimestampFormat = "HH:mm:ss ";
      });
    });

    var result = Parser.Default.ParseArguments<ActionDocOptions>(args);
    return await result
      .MapResult(
        (ActionDocOptions o) => RunActionDocAsync(o, loggerFactory),
        _ => Task.FromResult(ExitCodes.Error))
      .ConfigureAwait(false);
  }

  private static void ConfigureConsoleUtf8()
  {
    try
    {
      var utf8NoBom = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);
      global::System.Console.OutputEncoding = utf8NoBom;
      global::System.Console.InputEncoding = utf8NoBom;
    }
    catch
    {
      // ignore
    }
  }

  private static void LoadEnvironmentVariables()
  {
    try
    {
      var exeDir = Path.GetDirectoryName(Environment.ProcessPath) ?? Environment.CurrentDirectory;
      var envPath = Path.Combine(exeDir, ".env");
      if (File.Exists(envPath))
      {
        Env.Load(envPath);
        global::System.Console.WriteLine($"Loaded env file: {envPath}");
        return;
      }

      var currentDir = Environment.CurrentDirectory;
      for (var i = 0; i < 5; i++)
      {
        envPath = Path.Combine(currentDir, ".env");
        if (File.Exists(envPath))
        {
          Env.Load(envPath);
          global::System.Console.WriteLine($"Loaded env file: {envPath}");
          return;
        }

        var parentDir = Directory.GetParent(currentDir);
        if (parentDir is null)
        {
          break;
        }

        currentDir = parentDir.FullName;
      }
    }
    catch (Exception ex)
    {
      global::System.Console.Error.WriteLine($"Failed to load .env: {ex.Message}");
    }
  }

  private static async Task<int> RunActionDocAsync(ActionDocOptions options, ILoggerFactory loggerFactory)
  {
    var verb = (options.Action ?? string.Empty).Trim().ToLowerInvariant();
    return verb switch
    {
      "get" => await RunActionDocGetAsync(options, loggerFactory).ConfigureAwait(false),
      "pull" => await RunActionDocPullAsync(options, loggerFactory).ConfigureAwait(false),
      "upload" or "set" => await RunActionDocUploadAsync(options, loggerFactory).ConfigureAwait(false),
      "push" or "apply" => await RunActionDocPushAsync(options, loggerFactory).ConfigureAwait(false),
      _ => await UnknownVerbAsync(options).ConfigureAwait(false),
    };
  }

  private static async Task<int> UnknownVerbAsync(ActionDocOptions options)
  {
    await EmitErrorAsync(
        options.Json,
        "UNKNOWN_ACTION_DOC_VERB",
        "Use: pull|push|apply|get|upload|set (--code <sharedId> | --dir <folder>) [--json]")
      .ConfigureAwait(false);
    return ExitCodes.Error;
  }

  /// <summary>
  /// Resolves targets for pull/push: --code uses ~/.quicker/actions/&lt;id&gt;/info.html; --dir uses manifest in that folder.
  /// </summary>
  private static (string SharedId, string HtmlPath, string ActionDirectory) ResolvePullPushTargets(
    ActionDocOptions options,
    ActionLocalStore store)
  {
    if (!string.IsNullOrWhiteSpace(options.Dir))
    {
      var dir = Path.GetFullPath(options.Dir);
      var manifest = ActionManifestReader.FindManifestPath(dir);
      if (manifest is null)
      {
        throw new InvalidOperationException(
          $"No action.yaml / meta.yaml / manifest.yaml under '{dir}'.");
      }

      var (id, htmlRel) = ActionManifestReader.Read(manifest, defaultHtmlFile: ActionLocalStore.InfoHtmlFileName);
      var htmlPath = Path.GetFullPath(Path.Combine(dir, htmlRel));
      return (id, htmlPath, dir);
    }

    if (string.IsNullOrWhiteSpace(options.Code))
    {
      throw new InvalidOperationException(
        "Provide --code <sharedId> for pull/push, or --dir <folder> with meta.yaml.");
    }

    var sharedId = options.Code.Trim();
    var actionDir = store.GetActionDirectory(sharedId);
    return (sharedId, store.GetInfoHtmlPath(sharedId), actionDir);
  }

  private static (string SharedId, string HtmlPath) ResolveTargets(
    ActionDocOptions options,
    bool requireHtmlInput)
  {
    if (!string.IsNullOrWhiteSpace(options.Dir))
    {
      var dir = Path.GetFullPath(options.Dir);
      var manifest = ActionManifestReader.FindManifestPath(dir);
      if (manifest is null)
      {
        throw new InvalidOperationException(
          $"No action.yaml / meta.yaml / manifest.yaml under '{dir}'.");
      }

      var (id, htmlRel) = ActionManifestReader.Read(manifest, defaultHtmlFile: "description.html");
      var htmlPath = Path.GetFullPath(Path.Combine(dir, htmlRel));
      return (id, htmlPath);
    }

    if (!string.IsNullOrWhiteSpace(options.Code))
    {
      var sharedId = options.Code.Trim();
      if (requireHtmlInput)
      {
        if (string.IsNullOrWhiteSpace(options.Html))
        {
          throw new InvalidOperationException("Provide --html <path> when using --code for upload/set.");
        }

        return (sharedId, Path.GetFullPath(options.Html));
      }

      var outPath = string.IsNullOrWhiteSpace(options.Out)
        ? Path.Combine(Environment.CurrentDirectory, "description.html")
        : Path.GetFullPath(options.Out);
      return (sharedId, outPath);
    }

    throw new InvalidOperationException(
      "Provide --dir <folder> with manifest YAML, or --code <sharedId> (and --html for upload).");
  }

  private static async Task<int> RunActionDocGetAsync(ActionDocOptions options, ILoggerFactory loggerFactory)
  {
    string sharedId;
    string outputPath;

    try
    {
      (sharedId, outputPath) = ResolveTargets(options, requireHtmlInput: false);
    }
    catch (Exception ex)
    {
      await EmitErrorAsync(options.Json, "ACTION_DOC_MANIFEST_ERROR", ex.Message).ConfigureAwait(false);
      return ExitCodes.Error;
    }

    var fetch = await FetchDescriptionHtmlAsync(options.Json, sharedId, loggerFactory).ConfigureAwait(false);
    if (fetch.Html is null)
    {
      return ExitCodes.Error;
    }

    try
    {
      await WriteHtmlFileAsync(outputPath, fetch.Html).ConfigureAwait(false);
    }
    catch (Exception ex)
    {
      await EmitErrorAsync(options.Json, "HTML_WRITE_ERROR", ex.Message).ConfigureAwait(false);
      return ExitCodes.Error;
    }

    EmitGetSuccess(options.Json, "get", sharedId, outputPath, fetch.Html.Length, fetch.AgentSettings);
    return ExitCodes.Success;
  }

  private static async Task<int> RunActionDocPullAsync(ActionDocOptions options, ILoggerFactory loggerFactory)
  {
    var store = ActionLocalStore.FromEnvironment();
    string sharedId;
    string outputPath;
    string actionDir;

    try
    {
      (sharedId, outputPath, actionDir) = ResolvePullPushTargets(options, store);
    }
    catch (Exception ex)
    {
      await EmitErrorAsync(options.Json, "ACTION_DOC_RESOLVE_ERROR", ex.Message).ConfigureAwait(false);
      return ExitCodes.Error;
    }

    var fetch = await FetchDescriptionHtmlAsync(options.Json, sharedId, loggerFactory).ConfigureAwait(false);
    if (fetch.Html is null)
    {
      return ExitCodes.Error;
    }

    try
    {
      Directory.CreateDirectory(actionDir);
      await WriteHtmlFileAsync(outputPath, fetch.Html).ConfigureAwait(false);
      await store.WriteMetaAsync(sharedId, CancellationToken.None).ConfigureAwait(false);
    }
    catch (Exception ex)
    {
      await EmitErrorAsync(options.Json, "HTML_WRITE_ERROR", ex.Message).ConfigureAwait(false);
      return ExitCodes.Error;
    }

    if (options.Json)
    {
      global::System.Console.WriteLine(JsonSerializer.Serialize(
        new
        {
          ok = true,
          action = "pull",
          sharedId,
          actionDirectory = actionDir,
          infoHtmlPath = outputPath,
          actionsRoot = store.ActionsRoot,
          htmlLength = fetch.Html.Length,
          headless = fetch.AgentSettings.Headless,
          profileDirectory = fetch.AgentSettings.ProfileDirectory,
        },
        JsonWriteOptions));
    }
    else
    {
      global::System.Console.WriteLine(
        $"Pulled description for {sharedId} to {outputPath}. Edit the file, then: action-doc push --code {sharedId}");
    }

    return ExitCodes.Success;
  }

  private static async Task<int> RunActionDocPushAsync(ActionDocOptions options, ILoggerFactory loggerFactory)
  {
    var store = ActionLocalStore.FromEnvironment();
    string sharedId;
    string htmlPath;

    try
    {
      (sharedId, htmlPath, _) = ResolvePullPushTargets(options, store);
    }
    catch (Exception ex)
    {
      await EmitErrorAsync(options.Json, "ACTION_DOC_RESOLVE_ERROR", ex.Message).ConfigureAwait(false);
      return ExitCodes.Error;
    }

    var pagePath = Path.Combine(store.GetActionDirectory(sharedId), ActionLocalStore.PageHtmlFileName);
    if (File.Exists(pagePath))
    {
      if (!ActionDocBuildRunner.TryBuild(store.ActionsRoot, sharedId, out var buildError))
      {
        await EmitErrorAsync(options.Json, "ACTION_DOC_BUILD_ERROR", buildError ?? "Build failed.")
          .ConfigureAwait(false);
        return ExitCodes.Error;
      }
    }

    if (!File.Exists(htmlPath))
    {
      await EmitErrorAsync(
          options.Json,
          "LOCAL_INFO_NOT_FOUND",
          $"No local description at '{htmlPath}'. Add page.html and build, or run: action-doc pull --code {sharedId}")
        .ConfigureAwait(false);
      return ExitCodes.Error;
    }

    return await UploadDescriptionFromFileAsync(
        options,
        sharedId,
        htmlPath,
        loggerFactory,
        jsonActionName: options.Action?.Trim().ToLowerInvariant() is "apply" ? "apply" : "push")
      .ConfigureAwait(false);
  }

  private static async Task<int> RunActionDocUploadAsync(ActionDocOptions options, ILoggerFactory loggerFactory)
  {
    string sharedId;
    string htmlPath;

    try
    {
      (sharedId, htmlPath) = ResolveTargets(options, requireHtmlInput: true);
    }
    catch (Exception ex)
    {
      await EmitErrorAsync(options.Json, "ACTION_DOC_MANIFEST_ERROR", ex.Message).ConfigureAwait(false);
      return ExitCodes.Error;
    }

    if (!File.Exists(htmlPath))
    {
      await EmitErrorAsync(options.Json, "HTML_NOT_FOUND", $"HTML file not found: {htmlPath}")
        .ConfigureAwait(false);
      return ExitCodes.Error;
    }

    return await UploadDescriptionFromFileAsync(
        options,
        sharedId,
        htmlPath,
        loggerFactory,
        jsonActionName: "upload")
      .ConfigureAwait(false);
  }

  private static async Task<(string? Html, QuickerAgentSettings AgentSettings)> FetchDescriptionHtmlAsync(
    bool json,
    string sharedId,
    ILoggerFactory loggerFactory)
  {
    if (!TryGetCredentials(json, out var email, out var password))
    {
      return (null, QuickerAgentSettings.FromEnvironment());
    }

    var agentSettings = QuickerAgentSettings.FromEnvironment();
    var loginLogger = loggerFactory.CreateLogger<QuickerWebLoginService>();
    var docLogger = loggerFactory.CreateLogger<ActionDescriptionService>();
    var service = new ActionDescriptionService(new QuickerWebLoginService(loginLogger), docLogger);

    var result = await service
      .GetHtmlAsync(email, password, sharedId, agentSettings, CancellationToken.None)
      .ConfigureAwait(false);

    if (!result.Ok || string.IsNullOrEmpty(result.Html))
    {
      await EmitErrorAsync(
          json,
          result.ErrorCode ?? "GET_FAILED",
          result.Message ?? "Failed to fetch description HTML.")
        .ConfigureAwait(false);
      return (null, agentSettings);
    }

    return (result.Html, agentSettings);
  }

  private static async Task<int> UploadDescriptionFromFileAsync(
    ActionDocOptions options,
    string sharedId,
    string htmlPath,
    ILoggerFactory loggerFactory,
    string jsonActionName)
  {
    string htmlContent;
    try
    {
      htmlContent = await File.ReadAllTextAsync(htmlPath, Utf8NoBom).ConfigureAwait(false);
    }
    catch (Exception ex)
    {
      await EmitErrorAsync(options.Json, "HTML_READ_ERROR", ex.Message).ConfigureAwait(false);
      return ExitCodes.Error;
    }

    if (!TryGetCredentials(options.Json, out var email, out var password))
    {
      return ExitCodes.Error;
    }

    var agentSettings = QuickerAgentSettings.FromEnvironment();
    var loginLogger = loggerFactory.CreateLogger<QuickerWebLoginService>();
    var docLogger = loggerFactory.CreateLogger<ActionDescriptionService>();
    var service = new ActionDescriptionService(new QuickerWebLoginService(loginLogger), docLogger);

    var result = await service
      .SetHtmlAsync(email, password, sharedId, htmlContent, agentSettings, CancellationToken.None)
      .ConfigureAwait(false);

    if (!result.Ok)
    {
      await EmitErrorAsync(options.Json, result.ErrorCode ?? "UPLOAD_FAILED", result.Message ?? "Upload failed.")
        .ConfigureAwait(false);
      return ExitCodes.Error;
    }

    if (options.Json)
    {
      global::System.Console.WriteLine(JsonSerializer.Serialize(
        new
        {
          ok = true,
          action = jsonActionName,
          sharedId,
          htmlPath,
          headless = agentSettings.Headless,
          profileDirectory = agentSettings.ProfileDirectory,
        },
        JsonWriteOptions));
    }
    else
    {
      global::System.Console.WriteLine($"Uploaded description for shared action {sharedId} from {htmlPath}.");
    }

    return ExitCodes.Success;
  }

  private static async Task WriteHtmlFileAsync(string outputPath, string html)
  {
    var outDir = Path.GetDirectoryName(outputPath);
    if (!string.IsNullOrEmpty(outDir))
    {
      Directory.CreateDirectory(outDir);
    }

    await File.WriteAllTextAsync(outputPath, html, Utf8NoBom).ConfigureAwait(false);
  }

  private static void EmitGetSuccess(
    bool json,
    string actionName,
    string sharedId,
    string outputPath,
    int htmlLength,
    QuickerAgentSettings agentSettings)
  {
    if (json)
    {
      global::System.Console.WriteLine(JsonSerializer.Serialize(
        new
        {
          ok = true,
          action = actionName,
          sharedId,
          outputPath,
          htmlLength,
          browserChannel = agentSettings.BrowserChannel,
          profileDirectory = agentSettings.ProfileDirectory,
          headless = agentSettings.Headless,
        },
        JsonWriteOptions));
    }
    else
    {
      global::System.Console.WriteLine($"Saved description for {sharedId} to {outputPath}.");
    }
  }

  private static bool TryGetCredentials(bool json, out string email, out string password)
  {
    email = Environment.GetEnvironmentVariable("QUICKER_EMAIL") ?? string.Empty;
    password = Environment.GetEnvironmentVariable("QUICKER_PASSWORD") ?? string.Empty;
    if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
    {
      if (json)
      {
        global::System.Console.WriteLine(JsonSerializer.Serialize(
          new
          {
            ok = false,
            error = "MISSING_CREDENTIALS",
            message = "Set QUICKER_EMAIL and QUICKER_PASSWORD in .env or the environment.",
          },
          JsonWriteOptions));
      }
      else
      {
        global::System.Console.Error.WriteLine(
          "MISSING_CREDENTIALS: Set QUICKER_EMAIL and QUICKER_PASSWORD in .env or the environment.");
      }

      return false;
    }

    return true;
  }

  private static Task EmitErrorAsync(bool json, string code, string message)
  {
    if (json)
    {
      global::System.Console.WriteLine(JsonSerializer.Serialize(
        new { ok = false, error = code, message },
        JsonWriteOptions));
    }
    else
    {
      global::System.Console.Error.WriteLine($"{code}: {message}");
    }

    return Task.CompletedTask;
  }
}

[Verb("action-doc", HelpText = "Get or update HTML intro text for a shared action on getquicker.net.")]
public sealed class ActionDocOptions
{
  [Value(0, MetaName = "command", Required = true, HelpText = "pull | push | apply | get | upload | set")]
  public string? Action { get; set; }

  [Option("code", HelpText = "Shared action id (GUID) when not using --dir.")]
  public string? Code { get; set; }

  [Option("html", HelpText = "Path to HTML file for upload/set when not using --dir.")]
  public string? Html { get; set; }

  [Option("dir", HelpText = "Folder with manifest YAML + description.html.")]
  public string? Dir { get; set; }

  [Option("out", HelpText = "Output HTML path for get when using --code (default: ./description.html).")]
  public string? Out { get; set; }

  [Option("json", HelpText = "Emit JSON for automation.")]
  public bool Json { get; set; }
}
