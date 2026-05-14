# quicker-agent

Command-line tool for automating [getquicker.net](https://getquicker.net) in a **single persistent browser session** (Chrome DevTools Protocol). It is intended to be invoked repeatedly by an **AI agent** or scripts: stable verbs, exit codes, and optional `--json` output.

**AI / automation agents:** read **[AGENTS.md](AGENTS.md)** first (exe location, PATH, `.env`, exit codes, `--json`). Cursor **project skill** (auto-discovery when relevant): **[`.cursor/skills/quicker-agent-exe/SKILL.md`](.cursor/skills/quicker-agent-exe/SKILL.md)**.

## Features (current)

- **`session new`** — Resolve a CDP endpoint (from `QKAGENT_CDP_URL` or by running `qkagent`), connect with Playwright, **log in** using the same selectors as the `QuickerDependencyService` login flow in the local **quicker_build_net** repository, then save session metadata under `%LOCALAPPDATA%\quicker-agent\sessions\`.
- **`session status`** — Load the session file and check whether the CDP endpoint is still reachable.
- **`session close`** — Delete the local session metadata file (does **not** terminate the browser / `qkagent` process).
- **`action-doc`** — Placeholder for future read/write of action documentation (`exit code 2` = not implemented).

## Requirements

- .NET 8 SDK (for development and `dotnet run`). The repo uses **`QuickerAgent.slnx`** (XML solution); build with `dotnet build QuickerAgent.slnx`. Opening or building `.slnx` requires a compatible .NET SDK (9.0.200+; tested with .NET 10).
- Windows x64 for the provided publish script (same layout as `quicker_build_net`).
- Playwright **Chromium** when you rely on this process to launch/control its own browser build (`dotnet publish` output includes Playwright CLI; run `install chromium` as in the publish script). If you only **attach** to a browser started by `qkagent`, you may not need the Playwright browser install.

## Configuration

1. Copy [`env.example`](env.example) to `.env` next to `quicker-agent.exe` or in the current / parent directories (the loader walks up a few levels, same idea as `quicker_build_net`).
2. Set at least:

| Variable | Description |
|----------|-------------|
| `QUICKER_EMAIL` | getquicker.net account email |
| `QUICKER_PASSWORD` | Account password |
| `QKAGENT_CDP_URL` | *(optional)* WebSocket or HTTP CDP URL; if set, **`qkagent` is not spawned** (useful for tests or custom launchers). |
| `QKAGENT_COMMAND` | *(optional)* Executable to spawn when `QKAGENT_CDP_URL` is unset (default: `qkagent`). |
| `QKAGENT_SESSION_NEW_ARGS` | *(optional)* Extra arguments for that executable. |
| `QUICKER_AGENT_SESSION_ID` | *(optional)* Default session id when `--id` is omitted (default: `default`). |
| `QUICKER_AGENT_SESSION_DIR` | *(optional)* Override directory for session JSON files. |

**Security:** Session JSON contains the CDP URL (full control of the browser). Do not commit session files or `.env`.

## Usage

```powershell
# Create session, run qkagent (unless QKAGENT_CDP_URL is set), log in, save metadata
.\quicker-agent.exe session new --id mysession

# Machine-readable
.\quicker-agent.exe session new --id mysession --json

# Check CDP still works
.\quicker-agent.exe session status --id mysession --json

# Remove local metadata only
.\quicker-agent.exe session close --id mysession
```

### Exit codes

| Code | Meaning |
|------|--------|
| `0` | Success |
| `1` | Error (missing credentials, no session file, login failure, unreachable CDP for `status`, etc.) |
| `2` | Command not implemented (`action-doc`) |

## Publish (reference: quicker_build_net)

From the repository root:

```powershell
.\publish\publish-agent.ps1
```

Output: `publish\agent\quicker-agent.exe` plus dependencies. The script optionally copies `.env`, copies `env.example`, runs `pwsh -File playwright.ps1 install chromium` when `playwright.ps1` is present in the output folder (or falls back to `Microsoft.Playwright.CLI.dll`), and appends the publish folder to the user `PATH` when missing.

## Repository

Upstream: [https://github.com/QuickerHub/quicker-agent](https://github.com/QuickerHub/quicker-agent)

## `qkagent` contract

The launcher waits up to **120 seconds** for a line on **stdout or stderr** that contains:

- A JSON object with a `cdp` string property, e.g. `{"cdp":"ws://127.0.0.1:9222/devtools/browser/..."}`, or  
- A plain `http(s)://` or `ws(s)://` URL.

If your `qkagent` prints the URL differently, set `QKAGENT_CDP_URL` or adjust `QKAGENT_COMMAND` / arguments and output format.
