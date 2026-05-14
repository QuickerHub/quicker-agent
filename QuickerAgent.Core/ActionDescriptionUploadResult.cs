namespace QuickerAgent.Core;

/// <summary>
/// Result of an action-description HTML upload attempt.
/// </summary>
public readonly record struct ActionDescriptionUploadResult(bool Ok, string? ErrorCode, string? Message)
{
    public static ActionDescriptionUploadResult Success() => new(true, null, null);

    public static ActionDescriptionUploadResult Fail(string code, string message) =>
        new(false, code, message);
}
