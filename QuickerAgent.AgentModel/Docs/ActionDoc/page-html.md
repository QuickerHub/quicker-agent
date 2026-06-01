# page.html 写法

编辑 **`actions/<sharedId>/page.html`**，样式来自 **`actions/_shared/intro.css`**。构建时 CSS 内联进 `info.html` 再 push。

## 硬约束

1. 根元素：`<div class="qk-doc">…</div>`
2. **禁止 inline style**（`style="..."`）
3. 用约定 class 表达语义；`<code>`、`<kbd>` 样式在 `intro.css` 统一

## Class 速查

| Class | 用途 |
|-------|------|
| `qk-doc` | 根容器 |
| `qk-alert qk-alert--warning` | 警告条 |
| `qk-banner` / `qk-banner__*` | 深色推广条 |
| `qk-feedback` / `qk-qq` | QQ 群反馈行 |
| `qk-hero` / `qk-summary` | 标题与摘要 |
| `qk-section` | 章节（`h2`、`ul`/`ol`、`table`、`h3`） |
| `qk-links` / `qk-chip` | 底部链接按钮 |
| `qk-callout` | 设置链接等小块 |
| `qk-footnote` | 章节下小字说明 |

## 示例结构

```html
<div class="qk-doc">
  <div class="qk-hero">
    <h1>动作标题</h1>
    <p class="qk-summary">一句话说明</p>
  </div>
  <section class="qk-section">
    <h2>功能</h2>
    <ul>...</ul>
  </section>
</div>
```

完整示例：`actions/1abfcdc2-b98c-460c-7b7e-08deb0ad6916/page.html`

## 改样式

只改 **`actions/_shared/intro.css`** 即可统一所有动作页外观；不要复制 CSS 到每个 `page.html`。
