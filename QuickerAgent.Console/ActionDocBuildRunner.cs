using System.Diagnostics;
using QuickerAgent.Core;

namespace QuickerAgent.Console;

/// <summary>
/// Runs action_doc_builder (page.html → info.html) before push when page.html exists.
/// </summary>
internal static class ActionDocBuildRunner
{
  public static bool TryBuild(string actionsRoot, string sharedId, out string? error)
  {
    error = null;
    var pagePath = Path.Combine(actionsRoot, sharedId, "page.html");
    if (!File.Exists(pagePath))
    {
      return true;
    }

    var repoRoot = ActionLocalStore.TryFindRepoRoot();
    if (repoRoot is null)
    {
      error = "page.html exists but repo root (actions/README.md) was not found; run build-action-docs.ps1 manually.";
      return false;
    }

    var builderProject = Path.Combine(repoRoot, "action_doc_builder");
    if (!Directory.Exists(builderProject))
    {
      error = $"action_doc_builder project not found at {builderProject}";
      return false;
    }

    var psi = new ProcessStartInfo
    {
      FileName = "uv",
      WorkingDirectory = repoRoot,
      RedirectStandardOutput = true,
      RedirectStandardError = true,
      UseShellExecute = false,
      CreateNoWindow = true,
    };
    psi.ArgumentList.Add("run");
    psi.ArgumentList.Add("--project");
    psi.ArgumentList.Add(builderProject);
    psi.ArgumentList.Add("python");
    psi.ArgumentList.Add("-m");
    psi.ArgumentList.Add("action_doc_builder.cli");
    psi.ArgumentList.Add("--id");
    psi.ArgumentList.Add(sharedId);
    psi.ArgumentList.Add("--force");

    using var process = Process.Start(psi);
    if (process is null)
    {
      error = "Failed to start uv for action doc build.";
      return false;
    }

    var stdout = process.StandardOutput.ReadToEnd();
    var stderr = process.StandardError.ReadToEnd();
    process.WaitForExit();
    if (process.ExitCode != 0)
    {
      error = string.IsNullOrWhiteSpace(stderr) ? stdout : stderr;
      return false;
    }

    return true;
  }
}
