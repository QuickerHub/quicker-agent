---
name: action-topics-triage
description: >-
  List, read, reply, archive getquicker.net shared-action discussion topics via qkagent action-topics.
  Use when the user mentions 动作讨论, Share/Actions/Topics, 动作反馈, topic triage, or action-topics CLI.
disable-model-invocation: false
---

# 动作讨论区（Share/Actions/Topics）

通过 **`qkagent action-topics`**（Playwright）操作 getquicker 分享动作的讨论帖：`/Share/Actions/Topics?code=<sharedId>`。

## Agent 入口

```powershell
cd tools/qkagent
qkagent help --json
qkagent action-topics list --code <sharedId|Topics URL> --json
qkagent action-topics get --id <topicId|ViewTopic URL> [--login] --json
```

## 页面模型

| 页面 | URL |
|------|-----|
| 讨论列表 | `/Share/Actions/Topics?code={sharedId}` |
| 话题详情 | `/Common/Topics/ViewTopic/{topicId}` |
| 含已归档 | `...Topics?code={sharedId}&showAll=true` |

列表 DOM：`.question-list .question-item[data-tid]` · 标题 `a.question-title` · 分类/作者/浏览量在条目底部。

## 命令

```powershell
# 列出未归档话题
qkagent action-topics list --code 2d523add-5d30-4778-7740-08decd685f7e --json

# 含已归档
qkagent action-topics list --code <sharedId> --include-archived --json

# 读取详情（含 suggestedIssueLabels）
qkagent action-topics get --id 41029 --json

# 作者视角（暴露 AuthorControls / CanArchive）
qkagent action-topics get --id 41029 --login --json

# 回复（须 .env 登录）
qkagent action-topics reply --id 41029 --content "已记录，见 GitHub issue #N" --json

# 归档 / 标记已处理（动作作者）
qkagent action-topics archive --id 41029 --json
qkagent action-topics mark --id 41029 --status handled --json
```

## 分类 → GitHub 标签

`get --json` 返回 `suggestedIssueLabels`，默认映射：

| getquicker 分类 | labels |
|-----------------|--------|
| BUG反馈 / 异常报告 | `area:actions`, `type:bug` |
| 功能建议 / 动作需求 | `area:actions`, `type:feat` |
| 使用问题 | `area:actions`, `type:bug` |

按实际动作所属 area 调整（如 OCR 工作台 → 可能还有 `packages/ocr-studio` 相关 scope）。

## 凭据

`QUICKER_EMAIL` / `QUICKER_PASSWORD`（`.env`）。**list/get 可不登录**；reply/archive 须为动作作者账号。

## 与 QA 论坛区别

| | `qa post/edit` | `action-topics` |
|--|----------------|-----------------|
| 页面 | `/QA/...` 全站讨论区 | 某分享动作下的讨论 |
| 典型 URL | `/QA/Question/40752` | `/Common/Topics/ViewTopic/41029` |

动作用户反馈走 **action-topics**；全站 QA 走 **qa**。

## 归档失败时

1. `get --id ... --login --json` 查看 `CanArchive` 与 `AuthorControls`
2. 设 `QKAGENT_HEADLESS=false` 目视确认作者控件
3. 更新 `QuickerAgent.Core/ActionTopicsService.cs` 选择器
