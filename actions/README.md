# Action intro documents (getquicker.net)

编辑 **`page.html`** + 共享 **`_shared/intro.css`**，构建生成 **`info.html`**（已 gitignore，不上传仓库），`qkagent push` 上传编译结果。

## 目录

```
actions/
  _shared/intro.css       # 全站样式（只改这一处即可统一外观）
  <shared-guid>/
    page.html             # 源文件（提交到 git）
    info.html             # 构建产物（本地生成，勿提交）
    meta.yaml             # qkagent pull 时生成
```

## 用法

```powershell
# 1. 编辑 actions/<id>/page.html
# 2. 构建（push 前也会自动执行）
.\scripts\build-action-docs.ps1
# 或单个：.\scripts\build-action-docs.ps1 -Id <guid>
# 3. 发布
qkagent push --code <shared-guid>
```

## page.html 写法

根节点 `class="qk-doc"`，其余用约定 class，**不要写 inline style**。

| Class | 用途 |
|-------|------|
| `qk-alert qk-alert--warning` | 警告条 |
| `qk-banner` / `qk-banner__*` | 深色推广条（剪贴板稳定版→n10） |
| `qk-feedback` / `qk-qq` | QQ 群反馈行 |
| `qk-hero` / `qk-summary` | 标题与摘要 |
| `qk-section` | 章节（`h2`、`ul`/`ol`、`table`、`h3`） |
| `qk-links` / `qk-chip` | 底部链接按钮 |
| `qk-callout` | 设置链接等小块 |
| `qk-footnote` | 章节下小字说明 |

`<code>`、`<kbd>` 样式在 `intro.css` 中统一（含 `!important`）。

示例：`1abfcdc2-b98c-460c-7b7e-08deb0ad6916/page.html`

## 预览

开发（Vite HMR，改前端自动热更新）：

```powershell
cd preview
.\run-dev.ps1
# 浏览器打开 http://127.0.0.1:5176/
```

生产式（仅 API + 已构建静态页）：

```powershell
cd preview/web && npm install && npm run build
cd ..
uv run python preview_server.py
# http://127.0.0.1:8765/
```

预览读取 `page.html` 并内联 CSS；外部修改 `page.html` 后约 2 秒自动刷新预览。
