---
name: staged-changes-commit-summary
description: Reads git staged changes (index); if the index is empty, stages all working-tree changes (`git add -A` at repo root) then proceeds. Produces a Chinese commit summary with feat(scope) title plus one line per feature with affected files (filename-only; allow prefix wildcard when many files), shows it in a fenced code block, and commits by default via git commit -F. Use when the user asks to summarize staged changes, write a commit message from the index, review what is staged, or commit staged changes.
---

# Staged Changes → Commit Summary

## When to use

Apply this workflow when the user wants a commit message or changelog-style summary **from the index after any auto-stage** (see Direct commit). Summary content always reflects **what ends up staged** for the commit.

## Steps

1. **Ensure there is something to summarize**
   - If `git diff --cached --name-only` is non-empty, use the index as-is.
   - If the index is **empty** but the working tree has changes (e.g. `git status` shows modified/untracked files), run **`git add -A`** from the **repository root**, then re-check `git diff --cached`. If still empty, refuse (nothing to commit).
   - Prefer `git diff --cached` (same as `git diff --staged`) for reading.
   - If the diff is huge, start with `git diff --cached --stat` and `git diff --cached --name-only`, then open targeted hunks or paths.

2. **Read the diff**
   - Understand *behavioral* changes (new capability, bug fix, refactor, move/rename) from hunks, not only filenames.
   - Treat renames (`R` in `git status`) as one logical change spanning old and new paths when appropriate.

3. **Group into “features / operations”**
   - One bullet line = one user-visible or merge-reviewable unit (e.g. “migrate CodeEditor to ActionDesigner”, “remove deprecated helper”, “adjust DI registration”).
   - Merge tiny edits in the same area into one line; split unrelated concerns.

4. **Resolve “影响文件” display names**
   - Take **all paths touched in the staged diff** as one set (same scope as the whole message).
   - Always list **filename only** (last segment after `/`), never repo-relative path and never full path.
   - If many files share the same stable prefix, you may use **prefix wildcard** (e.g. `FormEdit*`、`ActionDesignerWindow.*`) to keep bullets readable.
   - Even when duplicate filenames exist in different directories, still keep filename-only output; if needed, merge by wildcard or add concise text context in the bullet description instead of paths.

5. **Choose `scope`**
   - Use a short module or folder token aligned with the repo (examples: `action-designer`, `stepengine`, `search`, `ui`, `core`).
   - If multiple scopes are equal, pick the dominant area or use `core`.

6. **First line type**
   - Default to **`feat(<scope>): <中文一句话总结>`** as requested.
   - If staged content is *clearly* not a feature (e.g. only docs, build scripts, pure refactor with no new capability), switch the type to match Conventional Commits (`fix`, `refactor`, `chore`, `docs`, `perf`, `test`) but **keep the same structure**: `<type>(<scope>): <中文一句话总结>`.

## Output format (required)

### Message contents

The commit text itself must follow this shape (suitable for `git commit -F` or manual use):

```text
feat(<scope>): <中文总结，概括本次暂存区整体价值>

- <功能或操作描述 1>；影响文件：<fileOrPath1>；<fileOrPath2>
- <功能或操作描述 2>；影响文件：<fileOrPath1>；<fileOrPath2>
```

Rules:

- **Title**: one line, no trailing period unless it is natural Chinese punctuation.
- **Body**: each line starts with `- `, **one capability/operation per line**, and ends with **`；影响文件：`** followed by **`；`-separated filename tokens** chosen by step 4 (filename-only; wildcard allowed; never output paths).
- List only files **touched in the staged diff** for that bullet (if a line is purely mechanical across many files, you may list the main entries and add “等” only when the list would be unreadably long).
- Language: **Chinese** for descriptions; **English** only inside code identifiers or when quoting symbols.

### Presentation (required)

- Show the **final** message to the user inside **one Markdown fenced code block** labeled `text` (fence as ` ```text ` … ` ``` `).
- Chat clients may or may not soft-wrap code blocks; do **not** rely on UI wrapping. Keep each `-` line **reasonably short** (prefer splitting into additional `-` bullets or tightening wording) so the block stays readable without extreme horizontal scrolling.
- Do **not** wrap by breaking the `- ` prefix pattern mid-bullet.

## Follow-up options (required)

After the fenced block, always append a short **“下一步”** section in Chinese with **numbered options**, for example:

1. **已直接提交（默认）**：代理已按当前暂存区内容执行提交，并在回复中报告 `git` 结果。
2. **仅输出不提交**：若用户明确要求“只生成说明不提交”，代理仅输出代码块与建议，不执行 `git commit`。
3. **修改提交说明后再提交**：用户说明要改的标题/条目，代理重新阅读暂存区并输出新版代码块，然后按默认流程直接提交。

Adjust wording if the user’s language is not Chinese, and keep the options semantically equivalent.

## Direct commit (default behavior)

When this skill is invoked for staged-summary/commit intent, execute direct commit by default. Only skip commit when the user explicitly requests output-only behavior (e.g. “只生成提交说明，不提交”).

1. **Re-check staged state** at the git repo root. If `git diff --cached --name-only` is empty:
   - If the working tree has changes, run **`git add -A`** from the repo root once, then re-check the index.
   - If still empty, refuse (clean tree; nothing to commit) and tell the user there is nothing to stage.
2. **Write the exact commit message** to a UTF-8 text file outside tracked product dirs when possible (prefer OS temp, e.g. Windows `%TEMP%\quicker-staged-commit-msg.txt`, macOS/Linux `/tmp/quicker-staged-commit-msg.txt`). Avoid committing helper files.
3. Run from the repository root:

```bash
git commit -F "<path-to-utf8-message-file>"
```

4. **Staging rule**: Do **not** run extra `git add` after the index is already non-empty at the start of the flow (respect user’s prior partial stage). Only use `git add -A` when the index was **empty** and you need to populate it. Do **not** amend unless the user explicitly asked for amend.
5. Report `git` stdout/stderr and whether it succeeded. On failure, do not claim the commit happened. If auto-stage ran, briefly note that in the reply (e.g. 已执行 `git add -A` 暂存全部更改).

## Quality bar

- Summarize **what users or maintainers gain**, not low-level diff narration (“add using”, “rename variable”) unless that *is* the feature.
- Do not invent files or behaviors not present in the staged diff.
- If the index was empty and the tree was clean, refuse; do not fabricate a message.
- After auto-stage, summarize only what appears in the staged diff (same as before).

## Commands reference

```bash
git diff --cached --stat
git diff --cached --name-only
git diff --cached
# When index is empty but there are working-tree changes (from repo root):
git add -A
# Default behavior:
git commit -F /path/to/utf8-message.txt
```

## Example (illustrative)

Below is the **message body only**; in a real reply it must appear inside its own ` ```text ` fenced block, immediately followed by the required **下一步** numbered options (not shown here).

```text
feat(action-designer): 集中表格设计窗口到设计器模块

- 将表格管理/设计相关窗口从主程序迁移到 ActionDesigner，减少主工程 UI 耦合；影响文件：TableManageWindow.xaml.cs；Quicker.csproj
- 为旧入口保留兼容转调或删除无用引用（以暂存区为准）；影响文件：TableModuleCompatibilityStubs.cs
```

（若暂存区内同时存在多个同名文件（如 `App.config`），仍只写文件名；必要时可写 `App*.config` 这类前缀通配符，且不写任何路径。）
