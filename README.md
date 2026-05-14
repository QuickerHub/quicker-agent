# quicker-agent

Command-line tool for automating [getquicker.net](https://getquicker.net) in a **single persistent browser session** (Chrome DevTools Protocol). It is intended to be invoked repeatedly by an **AI agent** or scripts: stable verbs, exit codes, and optional `--json` output.

**AI / automation agents:** read **[AGENTS.md](AGENTS.md)** first (exe `qkagent.exe`, PATH, `.env`, exit codes, `--json`). Cursor **project skills:** [`.cursor/skills/quicker-agent-exe/SKILL.md`](.cursor/skills/quicker-agent-exe/SKILL.md) (CLI usage), [`.cursor/skills/qkagent-publish-exe/SKILL.md`](.cursor/skills/qkagent-publish-exe/SKILL.md) (publish after code changes).

## Features (current)

- **`session new`** — Resolve a CDP endpoint (from `QKAGENT_CDP_URL` or by running `QKAGENT_COMMAND`, default **`qkagent-host`**), connect with Playwright, **log in** using the same selectors as the `QuickerDependencyService` login flow in the local **quicker_build_net** repository, then save session metadata under `%LOCALAPPDATA%\quicker-agent\sessions\`.
- **`session status`** — Load the session file and check whether the CDP endpoint is still reachable.
- **`session close`** — Delete the local session metadata file (does **not** terminate the browser or the external CDP launcher process).
- **`action-doc upload`** — After `session new`, uploads HTML from a file into the getquicker.net shared-action intro editor (Summernote). Supports either `--code` + `--html`, or `--dir` with a small YAML manifest (see below).

## Requirements

- .NET 8 SDK (for development and `dotnet run`). The repo uses **`QuickerAgent.slnx`** (XML solution); build with `dotnet build QuickerAgent.slnx`. Opening or building `.slnx` requires a compatible .NET SDK (9.0.200+; tested with .NET 10).
- Windows x64 for the provided publish script (same layout as `quicker_build_net`).
- Playwright **Chromium** when you rely on this process to launch/control its own browser build (`dotnet publish` output includes Playwright CLI; run `install chromium` as in the publish script). If you only **attach** over CDP to a browser started elsewhere, you may not need the Playwright browser install.

## Configuration

1. Copy [`env.example`](env.example) to `.env` next to `qkagent.exe` or in the current / parent directories (the loader walks up a few levels, same idea as `quicker_build_net`).
2. Set at least:

| Variable | Description |
|----------|-------------|
| `QUICKER_EMAIL` | getquicker.net account email |
| `QUICKER_PASSWORD` | Account password |
| `QKAGENT_CDP_URL` | *(optional)* WebSocket or HTTP CDP URL; if set, **no** `QKAGENT_COMMAND` process is spawned. |
| `QKAGENT_COMMAND` | *(optional)* Executable to spawn when `QKAGENT_CDP_URL` is unset (default: **`qkagent-host`** — must not be this CLI's `qkagent.exe`). |
| `QKAGENT_SESSION_NEW_ARGS` | *(optional)* Extra arguments for that executable. |
| `QUICKER_AGENT_SESSION_ID` | *(optional)* Default session id when `--id` is omitted (default: `default`). |
| `QUICKER_AGENT_SESSION_DIR` | *(optional)* Override directory for session JSON files. |

**Security:** Session JSON contains the CDP URL (full control of the browser). Do not commit session files or `.env`.

## Usage

```powershell
# Create session (CDP from QKAGENT_CDP_URL or QKAGENT_COMMAND), log in, save metadata
.\qkagent.exe session new --id mysession

# Machine-readable
.\qkagent.exe session new --id mysession --json

# Check CDP still works
.\qkagent.exe session status --id mysession --json

# Remove local metadata only
.\qkagent.exe session close --id mysession

# Upload intro HTML (requires prior session new; account must own the shared action)
.\qkagent.exe action-doc upload --code "<shared-guid>" --html .\intro.html --id mysession --json
.\qkagent.exe action-doc upload --dir .\path\to\action-folder --json
```

### Exit codes

| Code | Meaning |
|------|--------|
| `0` | Success |
| `1` | Error (missing credentials, no session file, login failure, unreachable CDP for `status`, `action-doc` upload/editor errors, etc.) |

## Action description (folder layout)

One directory per shared action:

| File | Purpose |
|------|---------|
| `action.yaml` (or `meta.yaml` / `manifest.yaml`) | Keys: `sharedId` (or `code`) = GUID from the share link; optional `html` = relative path to the HTML file (default `description.html`). |
| `description.html` | HTML fragment for the action intro (same as you would paste into the web editor). |

Example layout: [samples/action-doc/](samples/action-doc/).

Upload (from repo root; use your real session id if not `default`):

```powershell
.\publish\agent\qkagent.exe action-doc upload --dir .\samples\action-doc --json
# or explicit paths:
.\publish\agent\qkagent.exe action-doc upload --code "<shared-guid>" --html .\path\to\body.html --json
```

You must be logged in as the **owner** of that shared action. The site only shows the rich-text editor in that case. If the editor or save button cannot be found, set the optional environment variables in `env.example` under `QKAGENT_ACTION_DOC_*`.

## Publish (reference: quicker_build_net)

From the repository root:

```powershell
.\publish\publish-agent.ps1
```

**Agent workflow:** after substantive code changes to the console/core/publish script, run the above (or follow **[`.cursor/skills/qkagent-publish-exe/SKILL.md`](.cursor/skills/qkagent-publish-exe/SKILL.md)**) so `publish/agent/qkagent.exe` stays in sync unless the user opts out.

Output: `publish\agent\qkagent.exe` plus dependencies. The script optionally copies `.env`, copies `env.example`, runs `pwsh -File playwright.ps1 install chromium` when `playwright.ps1` is present in the output folder (or falls back to `Microsoft.Playwright.CLI.dll`), and **appends `publish\agent` to the user `PATH`** when missing so you can run `qkagent.exe` from any terminal (restart the terminal after first publish).

## Repository

Upstream: [https://github.com/QuickerHub/quicker-agent](https://github.com/QuickerHub/quicker-agent)

## CDP launcher contract (`QKAGENT_COMMAND`)

The shipped CLI is **`qkagent.exe`**. It must not spawn itself: either set **`QKAGENT_CDP_URL`**, or set **`QKAGENT_COMMAND`** to a *different* executable that prints a CDP URL (default command name is **`qkagent-host`** — put your real browser-launcher there or override `QKAGENT_COMMAND` to its full path).

That launcher process waits up to **120 seconds** for a line on **stdout or stderr** that contains:

- A JSON object with a `cdp` string property, e.g. `{"cdp":"ws://127.0.0.1:9222/devtools/browser/..."}`, or  
- A plain `http(s)://` or `ws(s)://` URL.

If your CDP launcher prints the URL differently, set `QKAGENT_CDP_URL` or adjust `QKAGENT_COMMAND` / arguments and output format.
