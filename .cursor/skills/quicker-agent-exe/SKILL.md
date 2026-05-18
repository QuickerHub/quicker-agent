---
name: quicker-agent-exe
description: >-
  qkagent.exe: Playwright with system Chrome/Edge and persistent profile; pull/push or get/upload
  shared action intro HTML on getquicker.net.
disable-model-invocation: false
---

# qkagent.exe

## 作用

- **`action-doc pull` / `push`**：本地工作流（默认 `%USERPROFILE%\.quicker\actions\<id>\info.html`）。
- **`action-doc get` / `upload|set`**：任意路径读写 HTML。
- **抓取路径**：登录 → 动作页 → **编辑信息** → Summernote **源代码**（`.note-codable`）读/写，UTF-8 无 BOM 落盘；失败回退 `summernote('code')`。
- **发布路径**：`push` / `upload` 写回 HTML 后点击页面底部 **「更新动作信息」**（回退「保存」）。

浏览器：**Chrome → Edge → Playwright Chromium**；登录态：`%LOCALAPPDATA%\qkagent\browser-profile`。

## 修改动作说明（三步）

```text
qkagent.exe action-doc pull --code <sharedId> [--json]
# 编辑 ...\.quicker\actions\<sharedId>\info.html
qkagent.exe action-doc push --code <sharedId> [--json]
```

详见 [action-doc-workflow/SKILL.md](../action-doc-workflow/SKILL.md)。

## 前置

- `publish/agent/qkagent.exe`（整目录依赖）。
- `.env`：`QUICKER_EMAIL`、`QUICKER_PASSWORD`；可选 `QKAGENT_HEADLESS`、`QKAGENT_PROFILE_DIR`、`QKAGENT_ACTIONS_ROOT`、`QKAGENT_BROWSER_CHANNEL`。
- 本机 Chrome/Edge 或已安装 Playwright Chromium。

## 命令

```text
qkagent.exe action-doc pull|push --code <sharedId> [--json]
qkagent.exe action-doc get|upload|set (--code ... | --dir <folder>) [--json]
```

优先 **`--json`**；退出码 **0 / 1**。

## 文档

[README.md](../../../README.md)、[AGENTS.md](../../../AGENTS.md)、[env.example](../../../env.example)

发布 exe：[qkagent-publish-exe/SKILL.md](../qkagent-publish-exe/SKILL.md)
