using System.Text.Json;

namespace QuickerAgent.Core;

/// <summary>
/// Reads and writes session JSON files under the session directory.
/// </summary>
public sealed class SessionStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
    };

    private readonly string _rootDir;

    public SessionStore(string? rootDirOverride = null)
    {
        _rootDir = rootDirOverride?.Trim() ?? GetDefaultSessionRoot();
    }

    public static string GetDefaultSessionRoot()
    {
        var baseDir = Environment.GetEnvironmentVariable("QUICKER_AGENT_SESSION_DIR");
        if (!string.IsNullOrWhiteSpace(baseDir))
        {
            return Path.GetFullPath(baseDir.Trim());
        }

        var local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        return Path.Combine(local, "quicker-agent", "sessions");
    }

    public string GetSessionFilePath(string sessionId)
    {
        var id = SessionIdHelper.Normalize(sessionId);
        return Path.Combine(_rootDir, $"{id}.json");
    }

    public async Task<SessionRecord?> TryLoadAsync(string sessionId, CancellationToken cancellationToken = default)
    {
        var path = GetSessionFilePath(sessionId);
        if (!File.Exists(path))
        {
            return null;
        }

        await using var stream = File.OpenRead(path);
        return await JsonSerializer.DeserializeAsync<SessionRecord>(stream, JsonOptions, cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task SaveAsync(SessionRecord record, CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(_rootDir);
        var path = GetSessionFilePath(record.SessionId);
        await using var stream = File.Create(path);
        await JsonSerializer.SerializeAsync(stream, record, JsonOptions, cancellationToken).ConfigureAwait(false);
    }

    public Task DeleteAsync(string sessionId, CancellationToken cancellationToken = default)
    {
        var path = GetSessionFilePath(sessionId);
        if (File.Exists(path))
        {
            File.Delete(path);
        }

        return Task.CompletedTask;
    }
}
