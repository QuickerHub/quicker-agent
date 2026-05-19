---
name: action-doc-workflow
description: >-
  Edit actions/<id>/page.html + shared intro.css, build info.html, qkagent push.
disable-model-invocation: false
---

# 修改动作说明工作流

## 文件

| 路径 | 说明 |
|------|------|
| `actions/_shared/intro.css` | 统一样式表 |
| `actions/<sharedId>/page.html` | **源 HTML**（class 语义化，无 inline style） |
| `actions/<sharedId>/info.html` | **构建产物**（CSS 已内联，用于 push） |

仓库根下自动识别 `actions/`；可设 `QKAGENT_ACTIONS_ROOT=actions`。

## 流程

```powershell
qkagent pull --code "<shared-guid>" --json   # 可选
# 编辑 page.html（参考 actions/1abfcdc2-…/page.html）
.\scripts\build-action-docs.ps1 -Id "<shared-guid>"
qkagent push --code "<shared-guid>" --json
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
