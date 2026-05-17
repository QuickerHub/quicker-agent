---
name: quicker-agent-exe
description: >-
  qkagent.exe: Playwright with system Chrome/Edge and persistent profile; get or upload
  shared action intro HTML on getquicker.net.
disable-model-invocation: false
---

# qkagent.exe

## 作用

- **`action-doc get`**：登录（或复用 profile）→ 打开动作页 → 读取 Summernote HTML → 写入文件。
- **`action-doc upload` / `set`**：读取本地 HTML → 写入编辑器 → 保存。

浏览器：**Chrome → Edge → Playwright Chromium**；登录态目录默认 `%LOCALAPPDATA%\qkagent\browser-profile`。

## 前置

- `publish/agent/qkagent.exe`（整目录依赖）。
- `.env`：`QUICKER_EMAIL`、`QUICKER_PASSWORD`；可选 `QKAGENT_HEADLESS`、`QKAGENT_PROFILE_DIR`、`QKAGENT_BROWSER_CHANNEL`。
- 本机 Chrome/Edge 或已安装 Playwright Chromium。

## 命令

```text
qkagent.exe action-doc get (--code <sharedId> [--out <path>] | --dir <folder>) [--json]
qkagent.exe action-doc upload|set (--code <sharedId> --html <path> | --dir <folder>) [--json]
```

优先 **`--json`**；退出码 **0 / 1**。

## 文档

[README.md](../../../README.md)、[AGENTS.md](../../../AGENTS.md)、[env.example](../../../env.example)

发布 exe：[qkagent-publish-exe/SKILL.md](../qkagent-publish-exe/SKILL.md)
