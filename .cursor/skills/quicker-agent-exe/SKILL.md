---
name: quicker-agent-exe
description: >-
  Runs and interprets the quicker-agent Windows CLI (getquicker.net automation over CDP):
  session new/status/close, env vars, exit codes, and --json output. Use when the user
  mentions quicker-agent, quicker-agent.exe, qkagent, getquicker.net browser automation,
  CDP session files under LocalApplicationData, or publish/publish-agent.ps1.
disable-model-invocation: false
---

# quicker-agent.exe 使用说明（Skill）

## 何时使用本 Skill

在需要**通过命令行**操作本仓库发布的 **`quicker-agent.exe`**（创建/检查/关闭浏览器 CDP 会话、自动登录 getquicker.net）时，先按本文件执行；细节与故障排查见仓库根目录的 [AGENTS.md](../../../AGENTS.md) 与 [README.md](../../../README.md)。

## 前置条件

- **Windows**：发布脚本与当前发布目标为 win-x64。
- **可执行文件位置**：在仓库根执行 `publish/publish-agent.ps1` 后，主程序路径为 **`publish/agent/quicker-agent.exe`**（须保留**同目录全部依赖**，勿只拷贝单个 exe）。
- **PATH**：脚本可能已将 `publish/agent` 加入**用户 PATH**；若命令未找到，让用户**新开终端**或用**绝对路径**调用 exe。

## 配置（执行前）

1. 将 [env.example](../../../env.example) 复制为 **`.env`**，至少设置 **`QUICKER_EMAIL`**、**`QUICKER_PASSWORD`**。
2. `.env` 放在 **`quicker-agent.exe` 同目录**，或当前工作目录及其向上若干级父目录（与程序加载逻辑一致）。
3. **CDP 来源**二选一：
   - 设置 **`QKAGENT_CDP_URL`**：直接使用已有 Chromium CDP 地址，**不**启动 `qkagent`；
   - 或保证 **`qkagent`** 在 PATH，且能在约 **120 秒**内向 stdout/stderr 输出可解析的 CDP URL（JSON `{"cdp":"ws://..."}` 或裸 `http(s)://` / `ws(s)://` 行）。详见 README「qkagent contract」。

## Agent 调用约定

- **优先加 `--json`**：标准输出为单行 JSON，便于解析。
- **以退出码判断结果**：`0` 成功，`1` 错误，`2` 未实现（当前 **`action-doc`** 恒为占位）。
- **通过终端执行**本机上的 `quicker-agent.exe`（或 `quicker-agent`），解析最后一行或相关 stdout；勿假设跨机器路径。
- **勿**在日志、issue、提交内容中粘贴完整 `.env`、会话 JSON 或 CDP URL。

## 子命令速查

| 子命令 | 作用 |
|--------|------|
| `session new [--id <name>] [--json]` | 解析 CDP → Playwright 连接 → 登录 → 写入 `%LOCALAPPDATA%/quicker-agent/sessions/<id>.json` |
| `session status [--id <name>] [--json]` | 检查会话文件是否存在、CDP 是否仍可达（不可达时退出码 **1**） |
| `session close [--id <name>] [--json]` | 仅删除本地会话元数据；**不**结束浏览器 / `qkagent` |
| `action-doc get \| set ...` | 占位；退出码 **2**，JSON 中 `error` 为 `NOT_IMPLEMENTED` |

示例（发布目录下，机器可读）：

```powershell
./publish/agent/quicker-agent.exe session new --id default --json
./publish/agent/quicker-agent.exe session status --id default --json
```

## 与仓库开发的关系

- 从源码调试：`dotnet run --project QuickerAgent.Console -- session new --json`（需在含 `.env` 的目录或设置环境变量）。
- 构建解决方案：`dotnet build QuickerAgent.slnx`（需支持 SLNX 的 SDK）。

## 进一步阅读

- 面向 Agent 的逐步配置：[AGENTS.md](../../../AGENTS.md)
- 功能列表、环境变量表、发布说明：[README.md](../../../README.md)
