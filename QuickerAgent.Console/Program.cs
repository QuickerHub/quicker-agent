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
    public const int NotImplemented = 2;
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

    private static Task<int> RunActionDocAsync(ActionDocOptions options, ILoggerFactory loggerFactory)
    {
        _ = loggerFactory;
        var msg =
            "action-doc is not implemented yet. Use this verb later to read/write action documentation on getquicker.net.";
        if (options.Json)
        {
            global::System.Console.WriteLine(JsonSerializer.Serialize(
                new { ok = false, error = "NOT_IMPLEMENTED", message = msg },
                JsonWriteOptions));
        }
        else
        {
            global::System.Console.Error.WriteLine(msg);
        }

        return Task.FromResult(ExitCodes.NotImplemented);
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

[Verb("action-doc", HelpText = "Read or write action documentation (placeholder).")]
public sealed class ActionDocOptions
{
    [Value(0, MetaName = "action", Required = true, HelpText = "get | set")]
    public string? Action { get; set; }

    [Option("id", HelpText = "Session id for future use when automation is implemented.")]
    public string? Id { get; set; }

    [Option("json", HelpText = "Emit JSON for automation.")]
    public bool Json { get; set; }
}
