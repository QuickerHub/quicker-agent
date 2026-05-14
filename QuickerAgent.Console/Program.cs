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

        var result = Parser.Default.ParseArguments<ActionDocOptions>(args);
        return await result.MapResult(
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
        if (verb != "upload")
        {
            await EmitErrorAsync(
                    options.Json,
                    "UNKNOWN_ACTION_DOC_VERB",
                    "Use: action-doc upload (--code <sharedId> --html <path> | --dir <folder>)")
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

        var ct = CancellationToken.None;
        var loginLogger = loggerFactory.CreateLogger<QuickerWebLoginService>();
        var uploadLogger = loggerFactory.CreateLogger<ActionDescriptionUploadService>();
        var loginService = new QuickerWebLoginService(loginLogger);
        var uploadService = new ActionDescriptionUploadService(loginService, uploadLogger);
        var settings = ActionDescriptionUploadSettings.FromEnvironment();

        var result = await uploadService
            .UploadHtmlAsync(email, password, sharedId, htmlContent, settings, ct)
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
                    sharedId,
                    htmlPath,
                    headless = settings.Headless,
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

[Verb("action-doc", HelpText = "Upload HTML intro text for a shared action on getquicker.net.")]
public sealed class ActionDocOptions
{
    [Value(0, MetaName = "action", Required = true, HelpText = "upload")]
    public string? Action { get; set; }

    [Option("code", HelpText = "Shared action id (GUID) when not using --dir.")]
    public string? Code { get; set; }

    [Option("html", HelpText = "Path to HTML file when not using --dir.")]
    public string? Html { get; set; }

    [Option("dir", HelpText = "Folder with manifest YAML + HTML (see README).")]
    public string? Dir { get; set; }

    [Option("json", HelpText = "Emit JSON for automation.")]
    public bool Json { get; set; }
}
