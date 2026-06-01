# 构建与 push

## 源文件在仓库

在 quicker-agent 中，**只编辑** `actions/<sharedId>/page.html`（及 `actions/_shared/intro.css`）。`info.html` 由构建生成，**不要**当作源文件修改或提交。

## 构建 info.html

`page.html` + `intro.css` → `info.html`（CSS 内联，供 getquicker.net 使用）。

```powershell
.\scripts\build-action-docs.ps1              # 全部
.\scripts\build-action-docs.ps1 -Id <guid>   # 单个
```

Python 包 `action_doc_builder` 也可直接调用：

```powershell
uv run --project action_doc_builder python -m action_doc_builder --id <guid>
```

## push 自动构建

`action-doc push` 在发现 `actions/<id>/page.html` 时会**先构建**再上传。构建失败返回 `ACTION_DOC_BUILD_ERROR`。

## push 命令

```powershell
qkagent action-doc push --code <sharedId> --json
# 简写
qkagent push --code <sharedId> --json
```

也可 `--dir <folder>`（含 `meta.yaml` / `action.yaml`）。

## 底层 upload（任意 HTML 路径）

```powershell
qkagent action-doc upload --code <sharedId> --html .\path\to\info.html --json
qkagent action-doc set --dir .\samples\action-doc --json
```

## 文件角色

| 文件 | 角色 |
|------|------|
| `page.html` | **源**（提交 git） |
| `info.html` | **产物**（本地生成，勿提交） |
| `meta.yaml` | sharedId 等元数据（pull 生成） |
