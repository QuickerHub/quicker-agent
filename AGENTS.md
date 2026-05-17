# quicker-agent — 给 AI Agent 的快速说明

本仓库 **`qkagent.exe`** 通过 **Playwright** 操作 getquicker.net 上已分享动作的 **网页简介（HTML）**：可 **抓取** 或 **上传/修改**。浏览器优先使用本机 **Chrome / Edge**，登录态保存在本地 **profile 目录**，一般只需登录一次。

详细用法见 [README.md](README.md)。Cursor Skill：[`.cursor/skills/quicker-agent-exe/SKILL.md`](.cursor/skills/quicker-agent-exe/SKILL.md)、发布：[`.cursor/skills/qkagent-publish-exe/SKILL.md`](.cursor/skills/qkagent-publish-exe/SKILL.md)。

## 1. 可执行文件在哪

```powershell
.\publish\publish-agent.ps1
```

产物：**`<repo>\publish\agent\qkagent.exe`**（须保留同目录全部依赖）。

## 2. 配置

1. 将 [env.example](env.example) 复制为 **`.env`**（与 exe 同目录或仓库根等，见程序加载逻辑）。
2. 必填 **`QUICKER_EMAIL`**、**`QUICKER_PASSWORD`**（账号须为要编辑动作的**作者**）。
3. 可选：
   - **`QKAGENT_HEADLESS=true`**：无界面运行；默认有界面。
   - **`QKAGENT_PROFILE_DIR`**：浏览器 profile（cookies）；默认 `%LOCALAPPDATA%\qkagent\browser-profile`。
   - **`QKAGENT_BROWSER_CHANNEL`**：强制 `chrome` / `msedge` / `chromium`；默认依次尝试 Chrome → Edge → Playwright Chromium。

若本机无 Chrome/Edge，发布脚本安装的 Playwright Chromium 可作为兜底。

## 3. 调用约定

- 优先 **`--json`**；退出码 **0 成功，1 失败**。

```text
# 抓取简介 HTML → 文件
qkagent.exe action-doc get (--code <sharedId> [--out <path>] | --dir <folder>) [--json]

# 上传/修改简介 HTML
qkagent.exe action-doc upload|set (--code <sharedId> --html <path> | --dir <folder>) [--json]
```

## 4. 架构要点

| 模块 | 职责 |
|------|------|
| `QuickerBrowserLauncher` | `LaunchPersistentContextAsync`；channel 顺序 chrome → msedge → bundled |
| `QuickerBrowserSession` | 复用 profile；`EnsureLoggedInAsync` 检测会话，失效则自动登录 |
| `ActionDescriptionService` | `GetHtmlAsync` / `SetHtmlAsync`（Summernote 读/写 + 保存） |

## 5. 注意

- 勿在日志中粘贴完整 `.env`。
- 简介编辑控件仅作者可见；失败时按 `env.example` 调整 `QKAGENT_ACTION_DOC_*`。

## 6. 开发者

`dotnet build QuickerAgent.slnx`
