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

        var result = Parser.Default.ParseArguments<SessionOptions, ActionDocOptions>(args);
        return await result.MapResult(
                (SessionOptions o) => RunSessionAsync(o, loggerFactory),
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

    private static string ResolveSessionId(SessionOptions o)
    {
        var raw = o.Id;
        if (string.IsNullOrWhiteSpace(raw))
        {
            raw = Environment.GetEnvironmentVariable("QUICKER_AGENT_SESSION_ID");
        }

        if (string.IsNullOrWhiteSpace(raw))
        {
            raw = "default";
        }

        return SessionIdHelper.Normalize(raw);
    }

    private static string ResolveSessionId(ActionDocOptions o)
    {
        var raw = o.Id;
        if (string.IsNullOrWhiteSpace(raw))
        {
            raw = Environment.GetEnvironmentVariable("QUICKER_AGENT_SESSION_ID");
        }

        if (string.IsNullOrWhiteSpace(raw))
        {
            raw = "default";
        }

        return SessionIdHelper.Normalize(raw);
    }

    private static async Task<int> RunSessionAsync(SessionOptions options, ILoggerFactory loggerFactory)
    {
        var action = (options.Action ?? string.Empty).Trim().ToLowerInvariant();
        var sessionId = ResolveSessionId(options);
        var store = new SessionStore();
        var ct = CancellationToken.None;

        try
        {
            return action switch
            {
                "new" => await RunSessionNewAsync(options, sessionId, store, loggerFactory, ct).ConfigureAwait(false),
                "status" => await RunSessionStatusAsync(options, sessionId, store, loggerFactory, ct).ConfigureAwait(false),
                "close" => await RunSessionCloseAsync(options, sessionId, store, ct).ConfigureAwait(false),
                _ => UnknownSessionAction(action, options.Json),
            };
        }
        catch (Exception ex)
        {
            await EmitErrorAsync(options.Json, "SESSION_ERROR", ex.Message).ConfigureAwait(false);
            var log = loggerFactory.CreateLogger(typeof(Program));
            log.LogError(ex, "session command failed");
            return ExitCodes.Error;
        }
    }

    private static int UnknownSessionAction(string action, bool json)
    {
        if (json)
        {
            global::System.Console.WriteLine(JsonSerializer.Serialize(
                new { ok = false, error = "UNKNOWN_SESSION_ACTION", action },
                JsonWriteOptions));
        }
        else
        {
            global::System.Console.Error.WriteLine(
                $"Unknown session action '{action}'. Use: session new | session status | session close");
        }

        return ExitCodes.Error;
    }

    private static async Task<int> RunSessionNewAsync(
        SessionOptions options,
        string sessionId,
        SessionStore store,
        ILoggerFactory loggerFactory,
        CancellationToken cancellationToken)
    {
        var email = Environment.GetEnvironmentVariable("QUICKER_EMAIL");
        var password = Environment.GetEnvironmentVariable("QUICKER_PASSWORD");
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
        {
            await EmitErrorAsync(
                    options.Json,
                    "MISSING_CREDENTIALS",
                    "Set QUICKER_EMAIL and QUICKER_PASSWORD in .env or the environment.")
                .ConfigureAwait(false);
            return ExitCodes.Error;
        }

        var launcherLogger = loggerFactory.CreateLogger<QkAgentSessionLauncher>();
        var browserLogger = loggerFactory.CreateLogger<BrowserAutomationSession>();
        var loginLogger = loggerFactory.CreateLogger<QuickerWebLoginService>();

        var launcher = new QkAgentSessionLauncher(launcherLogger);
        var loginService = new QuickerWebLoginService(loginLogger);
        var browserSession = new BrowserAutomationSession(loginService, browserLogger);

        var launch = await launcher.ResolveCdpUrlAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var ok = await browserSession
                .ConnectAndLoginAsync(launch.CdpUrl, email, password, cancellationToken)
                .ConfigureAwait(false);

            if (!ok)
            {
                await EmitErrorAsync(options.Json, "LOGIN_FAILED", "Quicker login did not complete.").ConfigureAwait(false);
                return ExitCodes.Error;
            }

            var record = new SessionRecord
            {
                SessionId = sessionId,
                CdpUrl = launch.CdpUrl,
                CreatedUtc = DateTimeOffset.UtcNow,
                QkAgentProcessId = launch.ChildProcess?.HasExited == false ? launch.ChildProcess.Id : null,
            };

            await store.SaveAsync(record, cancellationToken).ConfigureAwait(false);

            if (options.Json)
            {
                global::System.Console.WriteLine(JsonSerializer.Serialize(
                    new
                    {
                        ok = true,
                        sessionId,
                        cdpUrl = launch.CdpUrl,
                        login = true,
                    },
                    JsonWriteOptions));
            }
            else
            {
                global::System.Console.WriteLine($"Session '{sessionId}' created and saved.");
                global::System.Console.WriteLine("Login: succeeded.");
                global::System.Console.WriteLine($"Session file: {store.GetSessionFilePath(sessionId)}");
            }

            return ExitCodes.Success;
        }
        finally
        {
            launch.ChildProcess?.Dispose();
        }
    }

    private static async Task<int> RunSessionStatusAsync(
        SessionOptions options,
        string sessionId,
        SessionStore store,
        ILoggerFactory loggerFactory,
        CancellationToken cancellationToken)
    {
        var record = await store.TryLoadAsync(sessionId, cancellationToken).ConfigureAwait(false);
        if (record is null)
        {
            await EmitErrorAsync(options.Json, "SESSION_NOT_FOUND", $"No session file for id '{sessionId}'.")
                .ConfigureAwait(false);
            return ExitCodes.Error;
        }

        var loginLogger = loggerFactory.CreateLogger<QuickerWebLoginService>();
        var browserLogger = loggerFactory.CreateLogger<BrowserAutomationSession>();
        var loginService = new QuickerWebLoginService(loginLogger);
        var browserSession = new BrowserAutomationSession(loginService, browserLogger);

        var alive = await browserSession.TryConnectAsync(record.CdpUrl, cancellationToken).ConfigureAwait(false);

        if (options.Json)
        {
            global::System.Console.WriteLine(JsonSerializer.Serialize(
                new
                {
                    ok = true,
                    sessionId,
                    cdpReachable = alive,
                    createdUtc = record.CreatedUtc,
                },
                JsonWriteOptions));
        }
        else
        {
            global::System.Console.WriteLine($"Session '{sessionId}': file exists.");
            global::System.Console.WriteLine(alive ? "CDP endpoint: reachable." : "CDP endpoint: not reachable (browser may be closed).");
        }

        return alive ? ExitCodes.Success : ExitCodes.Error;
    }

    private static async Task<int> RunSessionCloseAsync(
        SessionOptions options,
        string sessionId,
        SessionStore store,
        CancellationToken cancellationToken)
    {
        await store.DeleteAsync(sessionId, cancellationToken).ConfigureAwait(false);

        if (options.Json)
        {
            global::System.Console.WriteLine(JsonSerializer.Serialize(
                new { ok = true, sessionId, removed = true },
                JsonWriteOptions));
        }
        else
        {
            global::System.Console.WriteLine($"Session '{sessionId}' metadata removed (browser process not stopped by this tool).");
        }

        return ExitCodes.Success;
    }

    private static async Task<int> RunActionDocAsync(ActionDocOptions options, ILoggerFactory loggerFactory)
    {
        var verb = (options.Action ?? string.Empty).Trim().ToLowerInvariant();
        if (verb != "upload")
        {
            await EmitErrorAsync(
                    options.Json,
                    "UNKNOWN_ACTION_DOC_VERB",
                    "Use: action-doc upload (--code <sharedId> --html <path> | --dir <folder>) [--id <session>]")
                .ConfigureAwait(false);
            return ExitCodes.Error;
        }

        string sharedId;
        string htmlPath;

        try
        {
            if (!string.IsNullOrWhiteSpace(options.Dir))
            {
                var dir = Path.GetFullPath(options.Dir);
                var manifest = ActionManifestReader.FindManifestPath(dir);
                if (manifest is null)
                {
                    await EmitErrorAsync(
                            options.Json,
                            "MANIFEST_NOT_FOUND",
                            $"No action.yaml / meta.yaml / manifest.yaml under '{dir}'.")
                        .ConfigureAwait(false);
                    return ExitCodes.Error;
                }

                var (id, htmlRel) = ActionManifestReader.Read(manifest, defaultHtmlFile: "description.html");
                sharedId = id;
                htmlPath = Path.GetFullPath(Path.Combine(dir, htmlRel));
            }
            else if (!string.IsNullOrWhiteSpace(options.Code) && !string.IsNullOrWhiteSpace(options.Html))
            {
                sharedId = options.Code.Trim();
                htmlPath = Path.GetFullPath(options.Html);
            }
            else
            {
                await EmitErrorAsync(
                        options.Json,
                        "MISSING_ARGUMENTS",
                        "Provide --dir <folder> with manifest YAML, or both --code <sharedId> and --html <path>.")
                    .ConfigureAwait(false);
                return ExitCodes.Error;
            }
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

        string htmlContent;
        try
        {
            htmlContent = await File.ReadAllTextAsync(htmlPath).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            await EmitErrorAsync(options.Json, "HTML_READ_ERROR", ex.Message).ConfigureAwait(false);
            return ExitCodes.Error;
        }

        var sessionId = ResolveSessionId(options);
        var store = new SessionStore();
        var ct = CancellationToken.None;

        var record = await store.TryLoadAsync(sessionId, ct).ConfigureAwait(false);
        if (record is null)
        {
            await EmitErrorAsync(
                    options.Json,
                    "SESSION_NOT_FOUND",
                    $"No session file for id '{sessionId}'. Run: qkagent.exe session new --id {sessionId}")
                .ConfigureAwait(false);
            return ExitCodes.Error;
        }

        var email = Environment.GetEnvironmentVariable("QUICKER_EMAIL");
        var password = Environment.GetEnvironmentVariable("QUICKER_PASSWORD");
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
        {
            await EmitErrorAsync(
                    options.Json,
                    "MISSING_CREDENTIALS",
                    "Set QUICKER_EMAIL and QUICKER_PASSWORD in .env or the environment.")
                .ConfigureAwait(false);
            return ExitCodes.Error;
        }

        var loginLogger = loggerFactory.CreateLogger<QuickerWebLoginService>();
        var uploadLogger = loggerFactory.CreateLogger<ActionDescriptionUploadService>();
        var loginService = new QuickerWebLoginService(loginLogger);
        var uploadService = new ActionDescriptionUploadService(loginService, uploadLogger);
        var settings = ActionDescriptionUploadSettings.FromEnvironment();

        var result = await uploadService
            .UploadHtmlAsync(record.CdpUrl, email, password, sharedId, htmlContent, settings, ct)
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
                    sessionId,
                    sharedId,
                    htmlPath,
                    pageUrlTemplate = settings.PageUrlTemplate,
                },
                JsonWriteOptions));
        }
        else
        {
            global::System.Console.WriteLine($"Uploaded description for shared action {sharedId} from {htmlPath}.");
        }

        return ExitCodes.Success;
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

[Verb("session", HelpText = "Create, inspect, or drop a persisted browser session (CDP).")]
public sealed class SessionOptions
{
    [Value(0, MetaName = "action", Required = true, HelpText = "new | status | close")]
    public string? Action { get; set; }

    [Option("id", HelpText = "Session id (default: default or QUICKER_AGENT_SESSION_ID).")]
    public string? Id { get; set; }

    [Option("json", HelpText = "Emit JSON lines for automation.")]
    public bool Json { get; set; }
}

[Verb("action-doc", HelpText = "Upload HTML intro text for a shared action on getquicker.net.")]
public sealed class ActionDocOptions
{
    [Value(0, MetaName = "action", Required = true, HelpText = "upload")]
    public string? Action { get; set; }

    [Option("id", HelpText = "Browser session id (default: default or QUICKER_AGENT_SESSION_ID).")]
    public string? Id { get; set; }

    [Option("code", HelpText = "Shared action id (GUID) when not using --dir.")]
    public string? Code { get; set; }

    [Option("html", HelpText = "Path to HTML file when not using --dir.")]
    public string? Html { get; set; }

    [Option("dir", HelpText = "Folder with manifest YAML + HTML (see README).")]
    public string? Dir { get; set; }

    [Option("json", HelpText = "Emit JSON for automation.")]
    public bool Json { get; set; }
}
