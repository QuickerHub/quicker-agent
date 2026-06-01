# 修改动作页说明流程（Agent 必读）

通过 **`qkagent.exe`** 抓取/上传 getquicker.net 上**已分享动作**的网页简介 HTML。命令细节见 **`qkagent help --json`**；本文只规定**顺序与硬约束**。

## 硬约束：在本仓库编写

**动作说明的源文件必须在 quicker-agent 仓库的 `actions/` 目录里编写并提交 git。**

| 要做 | 不要做 |
|------|--------|
| 编辑 `<repo>/actions/<sharedId>/page.html` | 在 `%USERPROFILE%\.quicker\actions` 或其它目录写说明 |
| 改 `<repo>/actions/_shared/intro.css` 统一样式 | 直接改 `info.html`（仅为构建产物） |
| `push` 把构建结果上传到 getquicker.net | 用 `upload --html` 上传仓库外的临时 HTML |

在 quicker-agent 仓库内工作时，**不要**设置 `QKAGENT_ACTIONS_ROOT`——程序会自动识别 `<repo>/actions`（依据 `actions/README.md`）。

---

## 前置

1. 在 **quicker-agent 仓库根** 或子目录运行 qkagent。
2. `.env` 中配置 `QUICKER_EMAIL`、`QUICKER_PASSWORD`（须为动作**作者**账号）。
3. 已发布 `qkagent.exe`（`publish/publish-agent.ps1`）。

```powershell
qkagent guide get --topic cli-setup --json
```

---

## 流程总览（仓库工作流）

```text
1. 编辑 actions/<sharedId>/page.html     # 唯一源文件，提交 git
2. （可选）.\scripts\build-action-docs.ps1 -Id <sharedId>
3. qkagent push --code <sharedId> --json   # 自动构建 info.html 并上传
```

**pull 仅用于**首次从线上导入 HTML、或该动作尚无 `page.html` 时；已有 `page.html` 时**跳过 pull**，以仓库为准。

**禁止**直接改 `info.html` 作为源文件——它是构建产物。源文件永远是 **`page.html`** + **`actions/_shared/intro.css`**。

---

## 1. 定位动作

记下 getquicker.net 分享动作的 **sharedId**（GUID），例如 `1abfcdc2-b98c-460c-7b7e-08deb0ad6916`。

本地目录：`actions/<sharedId>/`（`meta.yaml` 在 pull 时生成）。

---

## 2. 同步线上（极少需要）

仅当 **`actions/<sharedId>/page.html` 尚不存在**、需要从 getquicker.net 拉取现有 HTML 作参考时：

```powershell
qkagent pull --code <sharedId> --json
```

写入 `actions/<sharedId>/info.html` 与 `meta.yaml`。**之后仍应把内容迁移到 `page.html` 并在仓库维护**；不要长期只改 `info.html`。

---

## 3. 编辑 page.html

```powershell
qkagent guide get --topic page-html --json
```

硬约束：

- 根节点 `class="qk-doc"`
- **不要** inline style
- 样式 class 见 `page-html` 指南与 `actions/_shared/intro.css`

参考示例：`actions/1abfcdc2-b98c-460c-7b7e-08deb0ad6916/page.html`

---

## 4. 构建 info.html

```powershell
.\scripts\build-action-docs.ps1 -Id <sharedId>
```

`push` 若检测到 `page.html` 会**自动构建**；构建失败则 push 中止。

---

## 5. 上传到 getquicker.net

```powershell
qkagent action-doc push --code <sharedId> --json
# 简写：qkagent push --code <sharedId> --json
```

Playwright 流程：登录 → 动作编辑页 → **编辑信息** → Summernote **源代码** → 写 HTML → **更新动作信息**。

---

## 6. 底层命令（非仓库工作流，慎用）

以下命令读写**任意路径** HTML，**不**走 `actions/<id>/page.html` 仓库约定。Agent 在 quicker-agent 仓库内应**优先用上面的仓库工作流**，勿用本节替代编辑 `page.html`。

```powershell
qkagent action-doc get --code <sharedId> --out .\intro.html --json
qkagent action-doc upload --code <sharedId> --html .\intro.html --json
```

---

## 相关指南

`overview` · `cli-setup` · `page-html` · `build-push` · `preview`
