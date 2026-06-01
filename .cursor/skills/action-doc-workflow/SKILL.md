---
name: action-doc-workflow
description: >-
  Edit actions/<id>/page.html + shared intro.css, build info.html, qkagent push.
disable-model-invocation: false
---

# 修改动作说明工作流

**源文件在本仓库 `actions/` 目录编写并提交 git。** 不要改 `%USERPROFILE%\.quicker\actions`，不要用 `upload --html` 上传临时文件替代 `page.html`。

## 文件

| 路径 | 说明 |
|------|------|
| `actions/_shared/intro.css` | 统一样式表 |
| `actions/<sharedId>/page.html` | **源 HTML**（class 语义化，无 inline style） |
| `actions/<sharedId>/info.html` | **构建产物**（CSS 已内联，用于 push） |

仓库根下自动识别 `actions/`；**在 quicker-agent 仓库内不要设** `QKAGENT_ACTIONS_ROOT`。

## 流程

```powershell
# 编辑 actions/<shared-guid>/page.html（参考 actions/1abfcdc2-…/page.html）
.\scripts\build-action-docs.ps1 -Id "<shared-guid>"   # 可选；push 会自动构建
qkagent push --code "<shared-guid>" --json

# 仅当尚无 page.html 时：
# qkagent pull --code "<shared-guid>" --json
```

## 样式 class 速查

- `qk-doc` 根容器
- `qk-alert qk-alert--warning` 警告
- `qk-feedback`、`qk-qq` 反馈行
- `qk-hero`、`qk-summary` 标题区
- `qk-section` 章节；`qk-links`、`qk-chip` 底部链接

`<code>` / `<kbd>` 样式见 `intro.css`。

## 文档

[actions/README.md](../../../actions/README.md)
