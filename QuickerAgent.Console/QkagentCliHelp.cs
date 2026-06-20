using System.Reflection;
using System.Text.Json;
using QuickerAgent.Core;

namespace QuickerAgent.Console;

/// <summary>Machine-readable CLI reference for scripts and AI agents.</summary>
internal static class QkagentCliHelp
{
  public static void WriteJson(TextWriter output)
  {
    output.WriteLine(JsonSerializer.Serialize(Build(), QkagentJson.HelpOutput));
  }

  private static object Build()
  {
    var store = ActionLocalStore.FromEnvironment();
    var actionsRootHint = store.ActionsRoot;
    var repoActionsRoot = ActionLocalStore.TryFindRepoActionsRoot();

    return new
    {
      name = "qkagent",
      version = GetCliVersion(),
      discovery = "qkagent help --json",
      agentWorkflow =
        "edit <repo>/actions/<sharedId>/page.html in git → qkagent push --code <sharedId> (auto-build info.html)",
      actionDocGuideTopic = "workflow",
      sourceOfTruth = repoActionsRoot is not null
        ? $"{repoActionsRoot}/<sharedId>/page.html (git-tracked in quicker-agent repo)"
        : $"{actionsRootHint}/<sharedId>/page.html",
      agentRules = new[]
      {
        "Edit page.html under repo actions/ only; commit to git; do not maintain docs in ~/.quicker/actions when repo actions/ exists.",
        "Never treat info.html as source; it is build output from page.html + intro.css.",
        "pull writes page.html (Summernote HTML, not stale source view); default workflow is edit page.html then push.",
        "Use action-doc image --file <path> to upload screenshots and patch page.html <img> src.",
        "Do not upload ad-hoc HTML via upload --html when the action folder exists in repo actions/.",
      },
      jsonFlag = "Append --json for structured stdout on operational commands.",
      exitCodes = new Dictionary<string, string>
      {
        ["0"] = "success",
        ["1"] = "error",
      },
      env = new object[]
      {
        Env("QUICKER_EMAIL", required: true, "getquicker.net account; must own the shared action."),
        Env("QUICKER_PASSWORD", required: true, "Account password."),
        Env("QKAGENT_HEADLESS", required: false, "Default true; set false for visible browser debugging."),
        Env("QKAGENT_PROFILE_DIR", required: false, "Browser profile (cookies). Default: %LOCALAPPDATA%\\qkagent\\browser-profile."),
        Env("QKAGENT_BROWSER_CHANNEL", required: false, "Force chrome | msedge | chromium."),
        Env("QKAGENT_ACTIONS_ROOT", required: false,
          repoActionsRoot is not null
            ? $"Optional override. Detected repo actions root: {repoActionsRoot} (prefer unset)."
            : $"pull/push local root. Current default: {actionsRootHint}"),
      },
      localLayout = new
      {
        repoRoot = ActionLocalStore.TryFindRepoRoot(),
        actionsRoot = actionsRootHint,
        sharedCss = "actions/_shared/intro.css",
        sourceHtml = "actions/<sharedId>/page.html",
        builtHtml = "actions/<sharedId>/info.html",
        meta = "actions/<sharedId>/meta.yaml",
        gitTrack = new[] { "actions/_shared/intro.css", "actions/<sharedId>/page.html" },
        gitIgnore = new[] { "actions/<sharedId>/info.html" },
      },
      commands = new object[]
      {
        Cmd("help", "Emit machine-readable CLI reference.", "qkagent help --json",
          opts: [Option("json", "Required for JSON output.", required: true)]),

        Cmd("guide get", "Read embedded action-doc guides (start: workflow).", "qkagent guide get --topic <id> [--json]",
          opts:
          [
            Option("topic", "Topic id (workflow, overview, cli-setup, page-html, build-push, preview)."),
            Option("json", "Structured output."),
          ]),

        Cmd("guide search", "Search action-doc guides.", "qkagent guide search [--query <keyword>] [--limit 10] [--json]",
          opts:
          [
            Option("query", "Keyword.", shortName: "q"),
            Option("limit", "Max results.", defaultValue: "10"),
            Option("json", "Structured output."),
          ]),

        Cmd("action-doc pull", "Fetch intro HTML into actions/<id>/info.html (+ meta.yaml).", "qkagent action-doc pull --code <sharedId> [--json]",
          opts: ActionDocPullPushOpts()),

        Cmd("action-doc push", "Build page.html in repo actions/, upload info.html to getquicker.net.", "qkagent action-doc push --code <sharedId> [--json]",
          opts: ActionDocPullPushOpts()),

        Cmd("action-doc image",
          "Upload a local image to getquicker CDN and update page.html <img> src (optional --push).",
          "qkagent action-doc image --code <sharedId> --file <path> [--index 0] [--alt <text>] [--push] [--json]",
          opts: ActionDocImageOpts()),

        Cmd("action-doc get", "Fetch intro HTML to arbitrary path.", "qkagent action-doc get (--code <sharedId> [--out path] | --dir <folder>) [--json]",
          opts: ActionDocGetUploadOpts()),

        Cmd("action-doc upload", "Upload HTML from --html or manifest dir.", "qkagent action-doc upload (--code <sharedId> --html <path> | --dir <folder>) [--json]",
          opts: ActionDocGetUploadOpts()),

        Cmd("action-doc set", "Alias for upload when using --dir.", "qkagent action-doc set --dir <folder> [--json]",
          opts: ActionDocGetUploadOpts()),

        Cmd("pull", "Shorthand for action-doc pull.", "qkagent pull --code <sharedId> [--json]",
          opts: ActionDocPullPushOpts()),

        Cmd("push", "Shorthand for action-doc push.", "qkagent push --code <sharedId> [--json]",
          opts: ActionDocPullPushOpts()),

        Cmd("qa post", "Create a new topic on getquicker.net/QA.", "qkagent qa post (--title <text> | --title-file <path>) --category <id|name> (--content <html> | --content-file <path>) [--keywords <text>] [--json]",
          opts: QaPostOpts()),

        Cmd("qa edit", "Edit an existing QA topic (author only).", "qkagent qa edit --id <questionId|url> [--title <text> | --title-file <path>] [--category <id|name>] [--content <html> | --content-file <path>] [--keywords <text>] [--json]",
          opts: QaEditOpts()),
      },
    };
  }

  private static object[] ActionDocPullPushOpts() =>
  [
    Option("code", "Shared action id (GUID)."),
    Option("dir", "Folder with meta.yaml / action.yaml."),
    Option("json", "Structured output."),
  ];

  private static object[] ActionDocGetUploadOpts() =>
  [
    Option("code", "Shared action id (GUID)."),
    Option("html", "HTML file path for upload/set with --code."),
    Option("dir", "Folder with manifest YAML + description.html."),
    Option("out", "Output path for get with --code (default: ./description.html)."),
    Option("json", "Structured output."),
  ];

  private static object[] ActionDocImageOpts() =>
  [
    Option("code", "Shared action id (GUID)."),
    Option("file", "Local image file path."),
    Option("index", "Zero-based <img> index in page.html.", defaultValue: "0"),
    Option("alt", "Match <img> by alt attribute substring."),
    Option("push", "Build and push after updating page.html."),
    Option("json", "Structured output."),
  ];

  private static object[] QaPostOpts() =>
  [
    Option("title", "Topic title."),
    Option("category", "Category id or Chinese name (e.g. 4 or 功能建议)."),
    Option("content", "Topic body (HTML or plain text)."),
    Option("content-file", "Path to HTML or plain-text body file."),
    Option("keywords", "Optional comma-separated keywords."),
    Option("json", "Structured output."),
  ];

  private static object[] QaEditOpts() =>
  [
    Option("id", "Question id or /QA/Question/{id} URL."),
    Option("title", "New title."),
    Option("category", "New category id or Chinese name."),
    Option("content", "New body (HTML or plain text)."),
    Option("content-file", "Path to new body file."),
    Option("keywords", "New keywords (pass empty string to clear)."),
    Option("json", "Structured output."),
  ];

  private static object Cmd(string name, string summary, string usage, object[] opts) =>
    new { name, summary, usage, options = opts };

  private static object Env(string name, bool required, string description) =>
    new { name, required, description };

  private static object Option(
    string name,
    string description,
    string? shortName = null,
    string? defaultValue = null,
    bool required = false) =>
    new
    {
      name,
      shortName,
      description,
      defaultValue,
      required = required ? true : (bool?)null,
    };

  private static string GetCliVersion()
  {
    var assembly = Assembly.GetExecutingAssembly();
    var informational = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
    if (!string.IsNullOrWhiteSpace(informational))
    {
      return informational;
    }

    return assembly.GetName().Version?.ToString() ?? "unknown";
  }
}
