# quicker-agent

Command-line tool to **upload HTML** for a [getquicker.net](https://getquicker.net) shared action’s web intro. It starts **Chromium via Playwright** (same launch pattern as `QuickerDependencyService` in **quicker_build_net**: `Chromium.LaunchAsync`, headed by default), logs in, fills the Summernote editor, and saves.

**Automation agents:** read **[AGENTS.md](AGENTS.md)** first. Cursor skills: [`.cursor/skills/quicker-agent-exe/SKILL.md`](.cursor/skills/quicker-agent-exe/SKILL.md), [`.cursor/skills/qkagent-publish-exe/SKILL.md`](.cursor/skills/qkagent-publish-exe/SKILL.md).

## Requirements

- .NET 8 SDK for `dotnet run` / `dotnet build QuickerAgent.slnx` (SLNX needs a recent SDK, e.g. 9.0.200+).
- Windows x64 for `publish/publish-agent.ps1`.
- Playwright **Chromium** installed for the published app (the publish script runs `playwright.ps1 install chromium` when present).

## Configuration

1. Copy [`env.example`](env.example) to **`.env`** next to `qkagent.exe` or repo root (loader walks up a few parent directories).

| Variable | Description |
|----------|-------------|
| `QUICKER_EMAIL` | getquicker.net account (**must own** the shared action to see the editor). |
| `QUICKER_PASSWORD` | Account password. |
| `QKAGENT_HEADLESS` | Optional: `1` / `true` to run Chromium headless (default: headed window). |
| `QKAGENT_ACTION_DOC_*` | Optional overrides for page URL, editor/save selectors — see `env.example`. |

Do not commit `.env`.

## Usage

```powershell
# Machine-readable
.\qkagent.exe action-doc upload --code "<shared-guid>" --html .\intro.html --json

# Folder with action.yaml + description.html
.\qkagent.exe action-doc upload --dir .\samples\action-doc --json
```

### Exit codes

| Code | Meaning |
|------|---------|
| `0` | Success |
| `1` | Error (missing credentials, bad paths, login failure, editor/save not found, etc.) |

## Action folder layout

| File | Purpose |
|------|---------|
| `action.yaml` (or `meta.yaml` / `manifest.yaml`) | `sharedId` or `code`; optional `html` path (default `description.html`). |
| `description.html` | HTML for the intro. |

Example: [samples/action-doc/](samples/action-doc/).

## Publish

```powershell
.\publish\publish-agent.ps1
```

Output: `publish\agent\qkagent.exe` and dependencies. The script may append `publish\agent` to the user `PATH`.

## Repository

Upstream: [https://github.com/QuickerHub/quicker-agent](https://github.com/QuickerHub/quicker-agent)
