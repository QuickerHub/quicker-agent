using System.Text.Json.Serialization;

namespace QuickerAgent.Core;

/// <summary>
/// Persisted session metadata for reconnecting over CDP.
/// </summary>
public sealed class SessionRecord
{
    [JsonPropertyName("sessionId")]
    public string SessionId { get; init; } = string.Empty;

    [JsonPropertyName("cdpUrl")]
    public string CdpUrl { get; init; } = string.Empty;

    [JsonPropertyName("createdUtc")]
    public DateTimeOffset CreatedUtc { get; init; }

    /// <summary>
    /// Optional PID of the qkagent child process (if launched by this tool).
    /// </summary>
    [JsonPropertyName("qkAgentProcessId")]
    public int? QkAgentProcessId { get; init; }
}
