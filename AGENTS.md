# quicker-agent — 给 AI Agent 的快速说明

本仓库 **`qkagent.exe`** 只做一件事：用 **Playwright 直接启动 Chromium**（与 `quicker_build_net` 里 `QuickerDependencyService` 的 `LaunchAsync` 方式一致），登录 getquicker.net，把本地 **HTML** 写入已分享动作的简介并保存。**不**再使用 CDP 会话文件或 `session new`。

详细用法见 [README.md](README.md)。Cursor Skill：[`.cursor/skills/quicker-agent-exe/SKILL.md`](.cursor/skills/quicker-agent-exe/SKILL.md)、发布：[`.cursor/skills/qkagent-publish-exe/SKILL.md`](.cursor/skills/qkagent-publish-exe/SKILL.md)。

## 1. 可执行文件在哪

```powershell
.\publish\publish-agent.ps1
```

产物：**`<repo>\publish\agent\qkagent.exe`**（须保留同目录全部依赖）。

## 2. 配置

1. 将 [env.example](env.example) 复制为 **`.env`**（与 exe 同目录或仓库根等，见程序加载逻辑）。
2. 必填 **`QUICKER_EMAIL`**、**`QUICKER_PASSWORD`**（账号须为要编辑动作的**作者**）。
3. 可选 **`QKAGENT_HEADLESS=true`**：无界面运行；默认 **有界面**，便于观察自动化。

首次发布请确保本机已安装 Playwright Chromium（发布脚本会尝试执行 `install chromium`）。

## 3. 调用约定

- 优先 **`--json`**；退出码 **0 成功，1 失败**。

```text
qkagent.exe action-doc upload (--code <sharedId> --html <path> | --dir <folder>) [--json]
```

## 4. 注意

- 勿在日志中粘贴完整 `.env`。
- 简介编辑控件仅作者可见；失败时按 `env.example` 调整 `QKAGENT_ACTION_DOC_*`。

## 5. 开发者

`dotnet build QuickerAgent.slnx`
