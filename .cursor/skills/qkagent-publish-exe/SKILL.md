---
name: qkagent-publish-exe
description: >-
  Publishes QuickerAgent.Console to publish/agent as win-x64 self-contained qkagent.exe,
  copies .env and env.example when present, runs Playwright chromium install when
  playwright.ps1 exists, and appends publish/agent to user PATH when missing. Use after
  editing QuickerAgent.Console, QuickerAgent.Core, QuickerAgent.slnx, publish/publish-agent.ps1,
  or when the user asks to publish, release, ship, or refresh the qkagent executable.
disable-model-invocation: false
---

# qkagent → publish-agent.ps1

## When to run

Run the publish script after edits that affect the shipped CLI or its publish layout, including:

- `QuickerAgent.Console/` (entry, verbs, `Program.cs`)
- `QuickerAgent.Core/` (Playwright, login, session, CDP launcher, action-doc upload)
- `QuickerAgent.Console.csproj` / `QuickerAgent.Core.csproj` / `QuickerAgent.slnx`
- `publish/publish-agent.ps1`

**Skip** automatic publish for: documentation-only changes limited to unrelated files, `.gitignore` only, or when the user explicitly says not to publish.

## Command

From repository root (or any path; the script locates the repo via `QuickerAgent.Console/QuickerAgent.Console.csproj`):

```powershell
pwsh -NoProfile -File ./publish/publish-agent.ps1
```

Wait for exit code **0**. On failure, read script output, fix the issue, then re-run.

## What the script does (short)

- `dotnet publish` of `QuickerAgent.Console` to `publish/agent/` (non-single-file, `win-x64`, self-contained)
- Copies `.env` from repo root to `publish/agent/` when present
- Copies `env.example` to `publish/agent/`
- Runs `pwsh -File playwright.ps1 install chromium` when `playwright.ps1` exists in the publish folder, else tries `dotnet exec Microsoft.Playwright.CLI.dll` when that DLL exists
- Appends the resolved `publish/agent` directory to the **user** `PATH` if not already there (user may need a new terminal)

## After success

Confirm briefly: success and output path `publish/agent/qkagent.exe` (note: restart terminal if `PATH` was updated).

## Version bumps

This script does **not** bump `Version` in the `.csproj`. When the user wants a released version number change, edit `<Version>` in `QuickerAgent.Console.csproj` (or agreed project file) in the same change set, then run the publish script.
