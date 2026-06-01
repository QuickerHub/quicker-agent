# 本地预览

修改 `page.html` 后可在浏览器预览（读取 page + 内联 CSS，非 getquicker.net 真实页面）。

## 开发模式（HMR）

```powershell
cd preview
.\run-dev.ps1
# http://127.0.0.1:5176/
```

## 生产式预览

```powershell
cd preview/web && npm install && npm run build
cd ..
uv run python preview_server.py
# http://127.0.0.1:8765/
```

外部修改 `page.html` 后约 2 秒自动刷新。

预览通过后再 `action-doc push` 上传。
