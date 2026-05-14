---
name: quicker-agent-exe
description: >-
  qkagent.exe: Playwright launches Chromium, logs into getquicker.net, uploads HTML
  for a shared action intro (action-doc upload). No CDP session files.
disable-model-invocation: false
---

# qkagent.exe（精简）

## 作用

**`action-doc upload`**：本机启动 Chromium → 登录 → 打开动作页 → 写入 Summernote → 保存。实现方式对齐 quicker_build_net 的 **`Chromium.LaunchAsync`**（有界面、非 CDP 附着）。

## 前置

- `publish/agent/qkagent.exe`（整目录依赖）。
- `.env`：`QUICKER_EMAIL`、`QUICKER_PASSWORD`；可选 `QKAGENT_HEADLESS=true`。
- Playwright Chromium 已安装（见发布脚本）。

## 命令

```text
qkagent.exe action-doc upload (--code <sharedId> --html <path> | --dir <folder>) [--json]
```

优先 **`--json`**；退出码 **0 / 1**。

## 文档

[README.md](../../../README.md)、[AGENTS.md](../../../AGENTS.md)、[env.example](../../../env.example)

发布 exe：[qkagent-publish-exe/SKILL.md](../qkagent-publish-exe/SKILL.md)
