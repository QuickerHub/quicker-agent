# quicker-agent

Command-line tool to **get or update HTML** for a [getquicker.net](https://getquicker.net) shared action’s web intro. It uses **Playwright** with a **persistent browser profile**, preferring **system Chrome or Edge**, then bundled Chromium. After the first successful login, cookies are reused until the session expires (then the tool logs in again automatically).

**Automation agents:** read **[AGENTS.md](AGENTS.md)** first. Cursor skills: [`.cursor/skills/quicker-agent-exe/SKILL.md`](.cursor/skills/quicker-agent-exe/SKILL.md), [`.cursor/skills/qkagent-publish-exe/SKILL.md`](.cursor/skills/qkagent-publish-exe/SKILL.md).

## Requirements

- .NET 8 SDK for `dotnet run` / `dotnet build QuickerAgent.slnx` (SLNX needs a recent SDK, e.g. 9.0.200+).
- Windows x64 for `publish/publish-agent.ps1`.
- **Google Chrome** or **Microsoft Edge** recommended (used first). Playwright **Chromium** is installed as fallback when you run the publish script.

## Configuration

1. Copy [`env.example`](env.example) to **`.env`** next to `qkagent.exe` or repo root (loader walks up a few parent directories).

| Variable | Description |
|----------|-------------|
| `QUICKER_EMAIL` | getquicker.net account (**must own** the shared action to see the editor). |
| `QUICKER_PASSWORD` | Account password. |
| `QKAGENT_HEADLESS` | Optional: `1` / `true` to run without a window (default: headed). |
| `QKAGENT_PROFILE_DIR` | Optional: persistent browser profile path (cookies). Default: `%LOCALAPPDATA%\qkagent\browser-profile`. |
| `QKAGENT_BROWSER_CHANNEL` | Optional: force `chrome`, `msedge`, or `chromium`. Default: try Chrome → Edge → Chromium. |
| `QKAGENT_ACTIONS_ROOT` | Optional: root for **pull/push** local files. Default: `<repo>/actions` when present, else `%USERPROFILE%\.quicker\actions`. |

Page UI labels (编辑信息、源代码、更新动作信息) are fixed in `QuickerAgent.Core/GetQuickerActionDocPage.cs`.

Do not commit `.env`.

## Usage

### Edit workflow (recommended)

Source files live in **[`actions/`](actions/)** — edit **`page.html`** (semantic HTML + CSS classes), build **`info.html`** (inlined styles), then push.

```powershell
# 1. Pull from getquicker.net (optional sync)
.\qkagent.exe pull --code "<shared-guid>" --json

# 2. Edit actions/<shared-guid>/page.html  (shared styles: actions/_shared/intro.css)

# 3. Build info.html (CSS inlined for getquicker.net)
.\scripts\build-action-docs.ps1 -Id "<shared-guid>"

# 4. Push to getquicker.net
.\qkagent.exe push --code "<shared-guid>" --json
```

See [actions/README.md](actions/README.md) for HTML class reference. Agent skill: [`.cursor/skills/action-doc-workflow/SKILL.md`](.cursor/skills/action-doc-workflow/SKILL.md).

### Low-level commands

```powershell
.\qkagent.exe action-doc get --code "<shared-guid>" --out .\intro.html --json
.\qkagent.exe action-doc get --dir .\samples\action-doc --json
.\qkagent.exe action-doc upload --code "<shared-guid>" --html .\intro.html --json
.\qkagent.exe action-doc set --dir .\samples\action-doc --json
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
| `description.html` | HTML for the intro (`get` writes here; `upload` reads from here). |

Example: [samples/action-doc/](samples/action-doc/).

## Publish

```powershell
.\publish\publish-agent.ps1
```

Output: `publish\agent\qkagent.exe` and dependencies. The script may append `publish\agent` to the user `PATH`.

## Repository

Upstream: [https://github.com/QuickerHub/quicker-agent](https://github.com/QuickerHub/quicker-agent)
