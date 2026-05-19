namespace QuickerAgent.Core;

/// <summary>
/// Local on-disk layout for per-action description HTML under the user profile.
/// Default: &lt;repo&gt;/actions/&lt;sharedId&gt;/info.html when README exists, else %USERPROFILE%\.quicker\actions.
/// </summary>
public sealed class ActionLocalStore
{
  public const string PageHtmlFileName = "page.html";
  public const string InfoHtmlFileName = "info.html";
  public const string MetaYamlFileName = "meta.yaml";
  public const string ActionsReadmeMarker = "README.md";
  public const string RepoActionsFolderName = "actions";

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
      root = TryFindRepoActionsRoot()
             ?? Path.Combine(
               Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
               ".quicker",
               "actions");
    }
    else if (!Path.IsPathRooted(root))
    {
      var baseDir = TryFindRepoRoot() ?? Directory.GetCurrentDirectory();
      root = Path.GetFullPath(Path.Combine(baseDir, root));
    }

    return new ActionLocalStore(root);
  }

  /// <summary>
  /// Walks up from cwd for actions/README.md (repo layout).
  /// </summary>
  public static string? TryFindRepoActionsRoot()
  {
    var repo = TryFindRepoRoot();
    return repo is null ? null : Path.Combine(repo, RepoActionsFolderName);
  }

  public static string? TryFindRepoRoot()
  {
    var dir = Directory.GetCurrentDirectory();
    for (var i = 0; i < 12; i++)
    {
      var marker = Path.Combine(dir, RepoActionsFolderName, ActionsReadmeMarker);
      if (File.Exists(marker))
      {
        return dir;
      }

      var parent = Directory.GetParent(dir);
      if (parent is null)
      {
        break;
      }

      dir = parent.FullName;
    }

    return null;
  }

  public string GetActionDirectory(string sharedActionId) =>
    Path.Combine(ActionsRoot, SanitizeActionId(sharedActionId));

  public string GetPageHtmlPath(string sharedActionId) =>
    Path.Combine(GetActionDirectory(sharedActionId), PageHtmlFileName);

  public string GetInfoHtmlPath(string sharedActionId) =>
    Path.Combine(GetActionDirectory(sharedActionId), InfoHtmlFileName);

  public string GetMetaYamlPath(string sharedActionId) =>
    Path.Combine(GetActionDirectory(sharedActionId), MetaYamlFileName);

  /// <summary>
  /// Ensures the action folder exists and writes minimal meta.yaml (sharedId + html path only).
  /// Action title/icon/description come from the open API at preview time, not from this file.
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
