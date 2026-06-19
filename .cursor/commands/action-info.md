# action-info — 操作 getquicker 动作页说明

Apply the **quicker-agent-exe** skill (and **action-doc-workflow** when editing `page.html`). **Run `qkagent` in the terminal yourself** — do not only describe steps for the user.

## User context

Use whatever the user typed after `/action-info` (shared action GUID, getquicker Sharedaction URL, action name, or edit request). If no target id/link is given, ask once.

## Bootstrap

```powershell
Get-Command qkagent -ErrorAction SilentlyContinue
qkagent help --json
qkagent guide get --topic workflow --json
```

If `qkagent` is missing, from **`tools/qkagent/`** in quicker-workspace run:

```powershell
cd tools/qkagent
pwsh -NoProfile -File ./publish/publish-agent.ps1
```

Then open a **new terminal** (PATH update) and retry.

## Workflow

1. Resolve **sharedId** (GUID) from user context or Sharedaction URL (`?code=`).
2. Work in **`tools/qkagent/`** (directory containing `actions/README.md`). `cd` there when editing sources.
3. **Edit + publish** (default): edit `actions/<sharedId>/page.html` (styles: `actions/_shared/intro.css`), then:

   ```powershell
   qkagent apply --dir actions/<sharedId> --json
   ```

4. **Pull** only when `actions/<sharedId>/page.html` does not exist yet:

   ```powershell
   qkagent pull --code <sharedId> --json
   ```

4b. **Replace a screenshot** when the user gives a **local image path**:

   ```powershell
   qkagent action-doc image --code <sharedId> --file "<path>" [--alt "主界面"] [--push] --json
   ```

4c. **Edit a QA topic** (author only):

   ```powershell
   qkagent qa edit --id <questionId|url> [--title ...] [--content ...] [--category ...] [--json]
   ```

5. Prefer **`--json`** on every command. Exit code **0** = success, **1** = failure.

## Credentials

`QUICKER_EMAIL` and `QUICKER_PASSWORD` in `.env` (`tools/qkagent/` root or next to `qkagent.exe`). Account must **own** the shared action.

## Do not

- Use `qkrpc` to change getquicker intro HTML (use `qkagent`).
- Use `push --code` when repo `actions/<sharedId>/` exists (prefer `apply --dir` so `info.html` is built).
- Upload ad-hoc HTML via `upload --html` when the action folder exists in repo `actions/`.
- Commit `info.html` (build output); source is `page.html` only.

For page-html class reference: `qkagent guide get --topic page-html --json`
