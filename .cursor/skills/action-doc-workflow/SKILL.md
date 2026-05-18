---
name: action-doc-workflow
description: >-
  Edit getquicker.net action intro HTML via local ~/.quicker/actions/<id>/info.html:
  qkagent action-doc pull, user edits, then action-doc push.
disable-model-invocation: false
---

# 修改动作说明工作流

## 本地目录约定

| 路径 | 说明 |
|------|------|
| `%USERPROFILE%\.quicker\actions\<sharedId>\info.html` | 从网站拉取的简介 HTML（默认文件名） |
| `%USERPROFILE%\.quicker\actions\<sharedId>\meta.yaml` | `pull` 时写入：`sharedId`、`html: info.html` |

可通过环境变量 **`QKAGENT_ACTIONS_ROOT`** 覆盖根目录（仍使用 `<id>/info.html` 子结构）。

与浏览器 profile（`%LOCALAPPDATA%\qkagent\browser-profile`）分离：profile 存登录态，`.quicker\actions` 存可编辑内容。

## 三步流程

1. **拉取**（网站 → 本地）

```powershell
qkagent.exe action-doc pull --code "<shared-guid>" --json
```

2. **编辑** `info.html`（按用户要求改文案、结构、样式等）。

3. **发布**（本地 → 网站）

```powershell
qkagent.exe action-doc push --code "<shared-guid>" --json
```

## 与底层命令的关系

| 工作流 | 等价底层 |
|--------|----------|
| `pull` | `get --code <id> --out <actionsRoot>/<id>/info.html` + 写 `meta.yaml` |
| `push` | `upload --code <id> --html <actionsRoot>/<id>/info.html` |

仍可使用 `get` / `upload` / `--dir`（仓库内 `samples/action-doc`）做项目内管理。

## 前置

- `qkagent.exe` 已发布，`.env` 含 `QUICKER_EMAIL` / `QUICKER_PASSWORD`。
- 账号为动作作者。

## 文档

[README.md](../../../README.md)、[AGENTS.md](../../../AGENTS.md)
