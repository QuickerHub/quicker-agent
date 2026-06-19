---
name: action-doc-workflow
description: >-
  Edits getquicker.net shared-action intro HTML in quicker-agent actions/<sharedId>/page.html,
  builds info.html, and uploads with qkagent push. Use when the user asks to change 动作说明,
  动作页说明, Sharedaction description, page.html, or getquicker intro text.
disable-model-invocation: false
---

# 修改动作页说明（getquicker 简介）

通过 **`qkagent.exe`**（Playwright）更新 [getquicker.net](https://getquicker.net) 上**已分享动作**的网页简介 HTML。源文件在本仓库 **`actions/`**，提交 git；线上由 **`push`** 发布。

## 何时使用

- 用户给出 `Sharedaction?code=<guid>` 或分享动作链接，要改「简介 / 说明 / 动作页」
- 发布 QuickerAgent / qkrpc 相关动作后需同步安装说明
- **不要**用 `qkrpc` 改动作页说明（`qkrpc` 管 Quicker 内步骤；简介 HTML 走本流程）

## 前置

| 项 | 要求 |
|----|------|
| 工作目录 | **quicker-agent 仓库根**（存在 `actions/README.md`） |
| 账号 | `.env` 中 `QUICKER_EMAIL` / `QUICKER_PASSWORD`，须为动作**作者** |
| 工具 | `publish/agent/qkagent.exe`（`.\publish\publish-agent.ps1`）或 PATH 中的 `qkagent` |
| 环境变量 | 在 quicker-agent 内**不要**设 `QKAGENT_ACTIONS_ROOT` |

```powershell
qkagent help --json
qkagent guide get --topic workflow --json   # 机器可读完整流程
```

## 定位 sharedId

从分享链接取 GUID，例如：

- `https://getquicker.net/Sharedaction?code=aa5917ad-1256-4c73-7022-08debe3efcbe` → `aa5917ad-1256-4c73-7022-08debe3efcbe`（QuickerAgent / QuickerRpc 插件入口）

本地目录：`actions/<sharedId>/`。

## 标准流程（仓库为准）

```text
1. 编辑 actions/<sharedId>/page.html     # 唯一源文件，提交 git
2. （可选）.\scripts\build-action-docs.ps1 -Id <sharedId>
3. qkagent push --code <sharedId> --json   # 自动构建 info.html 并上传
```

```powershell
# 仅当尚无 page.html、需从线上拉参考时（写入 actions/<id>/page.html）：
qkagent pull --code <sharedId> --json
```

## 更新说明截图（本地图片 → CDN → page.html）

用户提供**本地图片路径**时，用 `action-doc image`（无需手改 URL）：

```powershell
# 上传图片，替换 page.html 中第 1 个 <img> 的 src
qkagent action-doc image --code <sharedId> --file "D:\path\screenshot.png" --json

# 按 alt 匹配（更稳）
qkagent action-doc image --code <sharedId> --file "D:\path\screenshot.png" --alt "主界面" --json

# 上传 + 改 page.html + push 一条龙
qkagent action-doc image --code <sharedId> --file "D:\path\screenshot.png" --push --json
```

| 参数 | 含义 |
|------|------|
| `--file` | 本地 png/jpg 等 |
| `--index` | 第几个 `<img>`（默认 0） |
| `--alt` | 匹配 `alt` 包含该文字的 `<img>`（优先于 index） |
| `--push` | 更新后自动 `push` |

说明：`pull` 优先读 Summernote 当前 HTML（与线上一致），不再只读易过期的「源代码」视图。

## 讨论区发帖 / 改帖

```powershell
qkagent qa post --title "标题" --category 功能建议 --content-file .\draft.md --json
qkagent qa edit --id 40752 --title "新标题" --content "更新正文" --json
# --id 也接受 URL：https://getquicker.net/QA/Question/40752
```

`qa edit` 至少提供一个更新字段：`--title`、`--category`、`--content`/`--content-file`、`--keywords`。

| 要做 | 不要做 |
|------|--------|
| 改 `page.html` + `_shared/intro.css` | 在 `%USERPROFILE%\.quicker\actions` 写说明 |
| `push` 上传构建结果 | 用 `upload --html` 上传仓库外临时 HTML |
| 提交 `page.html` | 提交 `info.html`（构建产物，已 gitignore） |

## page.html 写法

- 根节点：`<div class="qk-doc">`
- **禁止** inline `style="..."`
- 语义 class 见下表；样式只改 **`actions/_shared/intro.css`**

| Class | 用途 |
|-------|------|
| `qk-hero` / `qk-summary` | 标题与摘要 |
| `qk-section` | 章节（`h2`、`ul`/`ol`、`table`） |
| `qk-alert qk-alert--warning` | 警告条 |
| `qk-callout` | 如 `quicker:settings:AutoRunActions` 链接 |
| `qk-footnote` | 章节下小字 |
| `qk-links` / `qk-chip` | 底部外链按钮 |
| `qk-feedback` / `qk-qq` | QQ 群反馈行 |

示例：`actions/1abfcdc2-b98c-460c-7b7e-08deb0ad6916/page.html` · 指南：`qkagent guide get --topic page-html --json`

## 文案原则（与产品行为一致）

写说明前先弄清**用户运行动作时 Quicker 里会发生什么**（弹窗、自动运行、子程序、插件提示等），避免网页重复冗长步骤。

- **运行时已有引导**（如 QuickerAgent：运行动作后提示下载/更新）→ 网页以「运行本动作 → 按提示操作」为主；手动下载链接放到 **参考** 或 `qk-footnote`，标注「未看到提示时使用」
- **必须用户事先安装** 的依赖（.NET、独立安装包）→ 在安装章节写清版本与直链
- 安装顺序：先「安装动作并运行/自动运行」，再「桌面端/CLI」，与真实连接顺序一致

## 预览（可选）

```powershell
cd preview
.\run-dev.ps1
# http://127.0.0.1:5176/ — 改 page.html 后热更新
```

## 上传后

- `push` 成功即线上简介已更新；无需再 `pull` 验证
- 若 Summernote 有字数上限，精简章节或把次要链接移到「参考」

## 相关文件

- quicker-agent 仓库：`actions/README.md`、`AGENTS.md`
- 嵌入指南：`qkagent guide get --topic overview|page-html|build-push|preview --json`
- Cursor 用户 skill 安装：`pwsh -NoProfile -File ./scripts/install-cursor-user.ps1`
- Chat 快捷入口：`/action-info`
