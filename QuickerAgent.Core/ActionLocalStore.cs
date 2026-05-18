namespace QuickerAgent.Core;

/// <summary>
/// Local on-disk layout for per-action description HTML under the user profile.
/// Default: %USERPROFILE%\.quicker\actions\&lt;sharedId&gt;\info.html
/// </summary>
public sealed class ActionLocalStore
{
  public const string InfoHtmlFileName = "info.html";
  public const string MetaYamlFileName = "meta.yaml";

  public ActionLocalStore(string actionsRoot)
  {
    ArgumentException.ThrowIfNullOrWhiteSpace(actionsRoot);
    ActionsRoot = Path.GetFullPath(actionsRoot);
  }

  /// <summary>
  /// Root directory containing one folder per shared action id.
  /// </summary>
  public string ActionsRoot { get; }

  public static ActionLocalStore FromEnvironment()
  {
    var root = Environment.GetEnvironmentVariable("QKAGENT_ACTIONS_ROOT");
    if (string.IsNullOrWhiteSpace(root))
    {
      root = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        ".quicker",
        "actions");
    }

    return new ActionLocalStore(root);
  }

  public string GetActionDirectory(string sharedActionId) =>
    Path.Combine(ActionsRoot, SanitizeActionId(sharedActionId));

  public string GetInfoHtmlPath(string sharedActionId) =>
    Path.Combine(GetActionDirectory(sharedActionId), InfoHtmlFileName);

  public string GetMetaYamlPath(string sharedActionId) =>
    Path.Combine(GetActionDirectory(sharedActionId), MetaYamlFileName);

  /// <summary>
  /// Ensures the action folder exists and writes a minimal meta.yaml for tooling.
  /// </summary>
  public async Task WriteMetaAsync(string sharedActionId, CancellationToken cancellationToken = default)
  {
    var dir = GetActionDirectory(sharedActionId);
    Directory.CreateDirectory(dir);

    var metaPath = GetMetaYamlPath(sharedActionId);
    var id = SanitizeActionId(sharedActionId);
    var content =
      $"# Managed by qkagent action-doc pull/push\n" +
      $"sharedId: \"{id}\"\n" +
      $"html: {InfoHtmlFileName}\n" +
      $"updatedAt: \"{DateTimeOffset.UtcNow:O}\"\n";

    await File.WriteAllTextAsync(metaPath, content, cancellationToken).ConfigureAwait(false);
  }

  public static string SanitizeActionId(string sharedActionId)
  {
    ArgumentException.ThrowIfNullOrWhiteSpace(sharedActionId);

    var id = sharedActionId.Trim();
    if (id.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0
        || id.Contains("..", StringComparison.Ordinal))
    {
      throw new ArgumentException($"Invalid shared action id for a local folder name: {sharedActionId}");
    }

    return id;
  }
}
