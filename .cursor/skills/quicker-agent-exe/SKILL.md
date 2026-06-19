---
name: quicker-agent-exe
description: >-
  Runs the local qkagent CLI (Playwright) to pull/push getquicker.net shared-action intro HTML.
  Use when the user mentions qkagent, action-info, 动作说明, action-doc, page.html,
  getquicker intro, QA 发帖, getquicker QA, or /action-info.
disable-model-invocation: false
---

# qkagent

`qkagent` 是本机 CLI。**Agent 在终端直接执行**，不要只写说明让用户手动跑。

## Agent 入口

```powershell
Get-Command qkagent -ErrorAction SilentlyContinue
qkagent help --json
qkagent guide get --topic workflow --json
```

找不到命令时，在 **quicker-agent** 仓库执行 `pwsh -NoProfile -File ./publish/publish-agent.ps1`，新开终端后再试。

## 作用

- **`pull` / `push`**：读写 quicker-agent **`actions/<sharedId>/`**（源文件 `page.html`，上传 `info.html`）。
- **`qa post`** / **`qa edit`**：在 getquicker.net 讨论区发表或修改话题（TinyMCE 正文）。
- **`get` / `upload|set`**：任意路径读写 HTML。
- **抓取**：登录 → 编辑页 → Summernote **源代码** 读/写，UTF-8 无 BOM。
- **发布**：写回 HTML 后点击 **「更新动作信息」**。

浏览器：**Chrome → Edge → Playwright Chromium**；登录态：`%LOCALAPPDATA%\qkagent\browser-profile`。

## 常用一条命令

```powershell
# 编辑 actions/<sharedId>/page.html 后：
qkagent push --code <sharedId> --json
# pull 仅当 actions/<sharedId>/page.html 尚不存在
qkagent pull --code <sharedId> --json
```

编辑 `page.html` 的 class 约定与完整流程：读 **action-doc-workflow** skill，或 `qkagent guide get --topic page-html --json`。

## 前置

- PATH 中的 `qkagent`，或 `quicker-agent/publish/agent/qkagent.exe`（整目录依赖）。
- `.env`：`QUICKER_EMAIL`、`QUICKER_PASSWORD`；可选 `QKAGENT_HEADLESS`、`QKAGENT_PROFILE_DIR`、`QKAGENT_ACTIONS_ROOT`、`QKAGENT_BROWSER_CHANNEL`。
- 本机 Chrome/Edge 或 Playwright Chromium。

## 命令

```text
qkagent pull|push --code <sharedId> [--json]
qkagent qa post --title <text> --category <id|name> (--content <html> | --content-file <path>) [--json]
qkagent qa edit --id <questionId|url> [--title <text>] [--category <id|name>] [--content <html> | --content-file <path>] [--json]
qkagent action-doc get|upload|set (--code ... | --dir <folder>) [--json]
```

优先 **`--json`**；退出码 **0 / 1**。

## Cursor 安装

仓库源文件在 `.cursor/skills/quicker-agent-exe/`。同步到用户目录（可重复覆盖）：

```powershell
pwsh -NoProfile -File ./scripts/install-cursor-user.ps1
```

Chat 快捷入口：`/action-info`（源文件 `.cursor/commands/action-info.md`）。

发布 exe：`pwsh -NoProfile -File ./publish/publish-agent.ps1`（含 PATH + 可选 Cursor 用户资源安装）。
