# qkagent 概览

**qkagent** 是 quicker-agent 仓库的命令行工具，用 **Playwright** 自动化 getquicker.net 上已分享动作的**网页简介 HTML**（非 Quicker 客户端内的动作步骤）。

## 能做什么

| 操作 | 说明 |
|------|------|
| pull / get | 从 getquicker.net 读取简介 HTML |
| push / upload / set | 写回简介 HTML 并提交 |
| guide | 内嵌 Agent 操作指南（无需登录） |
| help | 机器可读命令表 |

## 不能做什么

- 不能编辑 Quicker 动作步骤（用 [quicker-rpc](https://github.com/QuickerHub/quicker-rpc) 的 `qkrpc`）。
- 不能编辑非作者账号下的动作简介。

## Agent 入口

```powershell
qkagent help --json
qkagent guide get --topic workflow --json
```

## 源文件在本仓库

动作页说明的**唯一源**是 quicker-agent 仓库：

```text
quicker-agent/
  actions/
    _shared/intro.css          # 共享样式（git）
    <sharedId>/
      page.html                # 源 HTML（git，Agent 只改这个）
      info.html                # 构建产物（本地，gitignore）
      meta.yaml                # pull 时生成
```

- **git 为准**：改 `page.html` → commit → `push` 上线。
- **勿**在 `%USERPROFILE%\.quicker\actions` 维护说明（仅当不在 quicker-agent 仓库且无 `actions/README.md` 时才 fallback 到该路径）。

## 本地文件布局

## 浏览器与登录

- 优先 **Chrome → Edge → Playwright Chromium**
- 登录态：`%LOCALAPPDATA%\qkagent\browser-profile`（一般只需登录一次）
- 默认 headless；调试设 `QKAGENT_HEADLESS=false`
