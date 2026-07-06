# qkagent CLI 命令

人类可读的 CLI 命令列表。机器可读版本：**`qkagent help --json`**。Agent 操作指南：**`qkagent guide get --topic workflow --json`**。

## 发现与指南

### help

输出 CLI 自描述（Agent 优先读 `--json`）。

```powershell
qkagent help --json
```

### guide get

读取内嵌 Markdown 指南（无需登录）。

```powershell
qkagent guide get --topic workflow --json
qkagent guide get --topic page-html --json
```

| topic | 说明 |
|-------|------|
| `workflow` | 修改动作页说明完整流程（**Agent 入口**） |
| `overview` | 工具概览 |
| `cli-setup` | 安装与 `.env` |
| `page-html` | `page.html` class 约定 |
| `build-push` | 构建与 push |
| `preview` | 本地预览 |

### guide search

```powershell
qkagent guide search --query "page.html" --json
qkagent guide search --json
```

---

## 动作简介（本仓库 actions/）

**在本仓库 `actions/<sharedId>/page.html` 编写说明**，提交 git，再 push 上线。需要 `.env` 凭据。

```powershell
# 编辑 actions/<sharedId>/page.html 后
qkagent push --code <sharedId> --json
```

`pull` 仅当尚无 `page.html` 时用于从线上导入。

### action-doc（完整动词）

```powershell
qkagent action-doc pull --code <sharedId> --json
qkagent action-doc push --code <sharedId> --json
qkagent action-doc get --code <sharedId> --out .\intro.html --json
qkagent action-doc upload --code <sharedId> --html .\intro.html --json
qkagent action-doc get --dir .\samples\action-doc --json
qkagent action-doc set --dir .\samples\action-doc --json
```

---

## 动作讨论区（action-topics）

处理分享动作下的用户反馈（`/Share/Actions/Topics?code=...`）：

```powershell
qkagent action-topics list --code <sharedId> [--include-archived] [--json]
qkagent action-topics get --id <topicId|ViewTopic URL> [--login] [--json]
qkagent action-topics reply --id <topicId> (--content <text> | --content-file <path>) [--json]
qkagent action-topics archive --id <topicId> [--json]
qkagent action-topics mark --id <topicId> [--status handled] [--json]
```

完整 triage → GitHub issue 流程见 Cursor skill **action-feedback-pipeline**；Chat 入口 **`/action-feedback`**。

---

## 退出码

| 码 | 含义 |
|----|------|
| 0 | 成功 |
| 1 | 失败 |

---

## 发布

```powershell
.\publish\publish-agent.ps1
```

产物：`publish\agent\qkagent.exe`
