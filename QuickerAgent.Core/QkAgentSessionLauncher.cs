using System.Diagnostics;
using System.Threading.Channels;
using Microsoft.Extensions.Logging;

namespace QuickerAgent.Core;

public sealed class QkAgentSessionLauncher
{
    private readonly ILogger<QkAgentSessionLauncher> _logger;

    public QkAgentSessionLauncher(ILogger<QkAgentSessionLauncher> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Resolves CDP URL from <c>QKAGENT_CDP_URL</c> or by running <c>QKAGENT_COMMAND</c>.
    /// </summary>
    public async Task<QkAgentLaunchResult> ResolveCdpUrlAsync(CancellationToken cancellationToken = default)
    {
        var fromEnv = CdpUrlParser.FromEnvironment();
        if (!string.IsNullOrEmpty(fromEnv))
        {
            _logger.LogInformation("Using CDP URL from QKAGENT_CDP_URL.");
            return new QkAgentLaunchResult(fromEnv, ChildProcess: null);
        }

        // Default must not be "qkagent": this repo ships the CLI as qkagent.exe on PATH.
        var command = Environment.GetEnvironmentVariable("QKAGENT_COMMAND");
        if (string.IsNullOrWhiteSpace(command))
        {
            command = "qkagent-host";
        }
        else
        {
            command = command.Trim();
        }

        var args = Environment.GetEnvironmentVariable("QKAGENT_SESSION_NEW_ARGS") ?? string.Empty;

        _logger.LogInformation("Starting process: {Command} {Args}", command, args);

        var psi = new ProcessStartInfo
        {
            FileName = command,
            Arguments = args,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
        };

        var process = new Process { StartInfo = psi, EnableRaisingEvents = true };
        if (!process.Start())
        {
            throw new InvalidOperationException($"Failed to start process: {command}");
        }

        try
        {
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutCts.CancelAfter(TimeSpan.FromSeconds(120));

            var url = await ReadFirstCdpUrlFromProcessAsync(process, timeoutCts.Token).ConfigureAwait(false);
            if (string.IsNullOrEmpty(url))
            {
                try
                {
                    if (!process.HasExited)
                    {
                        process.Kill(entireProcessTree: true);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogDebug(ex, "Failed to kill CDP launcher after missing CDP URL.");
                }

                throw new InvalidOperationException(
                    "Timed out waiting for a CDP URL from the CDP launcher (QKAGENT_COMMAND). Set QKAGENT_CDP_URL or adjust QKAGENT_COMMAND / output format.");
            }

            _ = Task.Run(() => DrainStreamAsync(process.StandardOutput), CancellationToken.None);
            _ = Task.Run(() => DrainStreamAsync(process.StandardError), CancellationToken.None);

            return new QkAgentLaunchResult(url, process);
        }
        catch
        {
            try
            {
                if (!process.HasExited)
                {
                    process.Kill(entireProcessTree: true);
                }
            }
            catch
            {
                // ignore
            }

            throw;
        }
    }

    private async Task<string?> ReadFirstCdpUrlFromProcessAsync(Process process, CancellationToken cancellationToken)
    {
        var channel = Channel.CreateUnbounded<string>();
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var pumpsRemaining = 2;

        async Task PumpAsync(StreamReader reader)
        {
            try
            {
                while (true)
                {
                    var line = await reader.ReadLineAsync(linked.Token).ConfigureAwait(false);
                    if (line is null)
                    {
                        break;
                    }

                    await channel.Writer.WriteAsync(line, linked.Token).ConfigureAwait(false);
                }
            }
            catch (OperationCanceledException)
            {
                // expected when URL is found early or outer token fires
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Stream pump ended with error.");
            }
            finally
            {
                if (Interlocked.Decrement(ref pumpsRemaining) == 0)
                {
                    channel.Writer.TryComplete();
                }
            }
        }

        var stdoutPump = PumpAsync(process.StandardOutput);
        var stderrPump = PumpAsync(process.StandardError);

        try
        {
            await foreach (var line in channel.Reader.ReadAllAsync(linked.Token).ConfigureAwait(false))
            {
                if (!CdpUrlParser.TryParseLine(line, out var url) || string.IsNullOrEmpty(url))
                {
                    continue;
                }

                await linked.CancelAsync().ConfigureAwait(false);
                return url;
            }

            return null;
        }
        finally
        {
            if (!channel.Writer.TryComplete())
            {
                // already completed by pumps
            }

            await Task.WhenAll(stdoutPump, stderrPump).ConfigureAwait(false);
        }
    }

    private static async Task DrainStreamAsync(StreamReader reader)
    {
        try
        {
            while (await reader.ReadLineAsync().ConfigureAwait(false) is not null)
            {
            }
        }
        catch
        {
            // ignore
        }
    }
}

public sealed record QkAgentLaunchResult(string CdpUrl, Process? ChildProcess);
