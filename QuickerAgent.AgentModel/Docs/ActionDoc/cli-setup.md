# qkagent 环境与安装

## 发布 exe

在 quicker-agent 仓库根目录：

```powershell
.\publish\publish-agent.ps1
```

产物：`publish\agent\qkagent.exe`（须保留同目录全部依赖）。脚本可能将 `publish\agent` 加入用户 PATH。

## .env 配置

复制 `env.example` 为 `.env`（与 exe 同目录或仓库根；程序向上搜索数层父目录）。

| 变量 | 必填 | 说明 |
|------|------|------|
| `QUICKER_EMAIL` | 是 | getquicker.net 账号（须为动作作者） |
| `QUICKER_PASSWORD` | 是 | 密码 |
| `QKAGENT_HEADLESS` | 否 | 默认 true；false 时可见浏览器便于调试 |
| `QKAGENT_PROFILE_DIR` | 否 | 浏览器 profile；默认 `%LOCALAPPDATA%\qkagent\browser-profile` |
| `QKAGENT_BROWSER_CHANNEL` | 否 | 强制 `chrome` / `msedge` / `chromium` |
| `QKAGENT_ACTIONS_ROOT` | 否 | 覆盖 pull/push 本地根。**在 quicker-agent 仓库内通常不设**——自动用 `<repo>/actions` |

**勿**在日志或 commit 中粘贴完整 `.env`。

## 验证

```powershell
qkagent help --json
qkagent guide get --topic workflow --json
# 需要凭据：
qkagent action-doc pull --code <sharedId> --json
```

## 退出码

| 码 | 含义 |
|----|------|
| 0 | 成功 |
| 1 | 失败（缺凭据、路径错误、登录失败、页面控件未找到等） |

所有操作命令建议加 **`--json`**，stdout 为结构化 JSON。
