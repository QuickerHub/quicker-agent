---
name: quicker-agent-exe
description: >-
  qkagent.exe on Windows: CDP session (session new/status/close) and action-doc upload
  (HTML intro for getquicker.net shared actions). Use for QKAGENT_CDP_URL, publish/agent,
  or publish/publish-agent.ps1.
disable-model-invocation: false
---

# qkagent.exe（精简）

## 何时用

需要在本机调用 **`qkagent.exe`**：建立/检查 CDP 会话、登录 getquicker.net、或**把本地 HTML 上传到已分享动作的简介**。

## 前置

- Windows，可执行文件：`publish/agent/qkagent.exe`（发布脚本生成，须**整目录**依赖）。
- `.env`：`QUICKER_EMAIL`、`QUICKER_PASSWORD`；CDP 用 `QKAGENT_CDP_URL` 或 `QKAGENT_COMMAND`（勿让子进程指向本 `qkagent.exe`）。

## 命令

| 子命令 | 作用 |
|--------|------|
| `session new \| status \| close` | 与 [AGENTS.md](../../../AGENTS.md) 一致 |
| `action-doc upload` | 读 HTML，连已保存会话的浏览器，登录后打开动作页，写入 Summernote 并保存 |

上传二选一：

- `--code <sharedId> --html <文件路径>`
- `--dir <文件夹>`：内含 `action.yaml`（或 `meta.yaml` / `manifest.yaml`），字段 `sharedId`（或 `code`），可选 `html`（默认 `description.html`）

优先加 **`--json`**；退出码 **0 成功，1 失败**。

## 文档

- [AGENTS.md](../../../AGENTS.md)、[README.md](../../../README.md)、[env.example](../../../env.example) 中 `QKAGENT_ACTION_DOC_*`
- 改代码后发布：[qkagent-publish-exe/SKILL.md](../qkagent-publish-exe/SKILL.md)
