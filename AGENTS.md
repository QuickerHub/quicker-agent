# quicker-agent — 给 AI Agent 的快速说明

本文件用于让自动化 Agent **零上下文** 也能正确调用本仓库发布的 **`qkagent.exe`**（仓库名仍为 quicker-agent）。详细说明见 [README.md](README.md)。

在 **Cursor** 中使用本仓库时，还可加载项目 Skill：**[`.cursor/skills/quicker-agent-exe/SKILL.md`](.cursor/skills/quicker-agent-exe/SKILL.md)**（CLI 用法）、**[`.cursor/skills/qkagent-publish-exe/SKILL.md`](.cursor/skills/qkagent-publish-exe/SKILL.md)**（改代码后发布 `qkagent.exe`）。

## 1. 可执行文件在哪

在仓库根目录执行发布脚本后（需 Windows + PowerShell）：

```powershell
.\publish\publish-agent.ps1
```

产物目录：**`<repo>\publish\agent\`**，主程序为 **`qkagent.exe`**（同目录为依赖 DLL，勿只拷贝单个 exe）。

## 2. 系统上如何配置

### PATH（推荐）

`publish-agent.ps1` 成功结束时，若本机用户 PATH 中尚无该目录，会把 **`publish\agent` 的绝对路径** 追加到**用户级 PATH**。之后新开终端可直接：

```powershell
qkagent.exe session new --json
```

若仍提示找不到命令：**关闭并重新打开终端**（或重新登录），再试。

不追加 PATH 时，请使用**完整路径**调用，例如：

`<repo>\publish\agent\qkagent.exe`

### 凭据与环境变量（必须）

1. 将 [env.example](env.example) 复制为 **`.env`**。
2. 推荐放置位置（程序启动时会加载，顺序见代码逻辑）：
   - 与 **`qkagent.exe` 同目录**的 `.env`（发布脚本若发现仓库根有 `.env` 会拷到 `publish\agent\`），或  
   - 当前工作目录及其**向上若干级**父目录中的 `.env`。
3. 至少填写：
   - **`QUICKER_EMAIL`** / **`QUICKER_PASSWORD`** — getquicker.net 登录（`session new` 必需）。

可选但常用：

- **`QKAGENT_CDP_URL`** — 若已有一个 Chromium 的 CDP WebSocket（或 HTTP 调试入口），设置后 **不会** 再启动子进程去要 CDP，适合联调。
- **`QKAGENT_COMMAND`** / **`QKAGENT_SESSION_NEW_ARGS`** — 未设置 `QKAGENT_CDP_URL` 时，用于启动**另一个**可执行文件，由其向 stdout/stderr 打出 CDP URL。默认命令名为 **`qkagent-host`**（**勿**与本 CLI 的 `qkagent.exe` 同名，否则会递归启动自身）；请改为你的浏览器启动器路径，或设置 `QKAGENT_CDP_URL`。
- **`QUICKER_AGENT_SESSION_ID`** — 省略 `--id` 时的默认会话 id（默认 `default`）。

### Playwright 浏览器（按需）

若通过本工具自带的 Playwright 去连 **非** 外部 CDP、需要自己起 Chromium，发布目录下的 **`playwright.ps1`** 可用于安装浏览器；发布脚本会尝试执行 `install chromium`。仅连接**外部已打开**的浏览器 CDP 时，可不强依赖本机 Playwright 浏览器安装。

## 3. Agent 应如何调用（机器友好）

优先使用 **`--json`**，便于解析 stdout；根据 **退出码** 判断结果：

| 退出码 | 含义 |
|--------|------|
| `0` | 成功 |
| `1` | 错误（缺凭据、无会话文件、登录失败、`session status` 时 CDP 不可达、`action-doc` 上传失败等） |

常用命令：

```text
qkagent.exe session new [--id <name>] [--json]
qkagent.exe session status [--id <name>] [--json]
qkagent.exe session close [--id <name>] [--json]
qkagent.exe action-doc upload (--code <sharedId> --html <path> | --dir <folder>) [--id <name>] [--json]
```

动作说明：每个动作一个目录，内含 **YAML**（`sharedId` / `code` + 可选 `html` 相对路径）与 **HTML** 正文；或用 `--code` + `--html` 直接指定。须先 `session new`，且账号须为动作**作者**。可选 `QKAGENT_ACTION_DOC_*` 环境变量见 [env.example](env.example)。

会话元数据（含 CDP URL，属敏感信息）默认在：

`%LOCALAPPDATA%\quicker-agent\sessions\<id>.json`

## 4. 给 Agent 的约束与提示

- **`session close`** 只删本地 JSON，**不会**结束浏览器或外部 CDP 启动器进程。
- 未配置 **`QKAGENT_CDP_URL`** 且 **`QKAGENT_COMMAND`** 指向的程序不可用或未在 120 秒内输出 CDP 时，`session new` 会失败；联调请先设 `QKAGENT_CDP_URL`，或正确配置 `QKAGENT_COMMAND`（见 README「CDP launcher contract」）。
- 不要提交或打印完整 **`.env`** 与 **会话 JSON** 到公开日志。

## 5. 人类开发者

构建解决方案：`dotnet build QuickerAgent.slnx`（需支持 SLNX 的 SDK，见 README）。
