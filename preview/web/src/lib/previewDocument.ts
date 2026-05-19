import type { EffectiveTheme } from "../types";

export const DEFAULT_PREVIEW_WIDTH = 720;

export const ACTION_PAGE_URL = "https://getquicker.net/Sharedaction?code=";

export function actionPageUrl(actionId: string): string {
  return `${ACTION_PAGE_URL}${encodeURIComponent(actionId)}`;
}

export function wrapPreviewDocument(
  html: string,
  effectiveTheme: EffectiveTheme,
  themeCss: string
): string {
  return `<!DOCTYPE html>
<html lang="zh-CN" data-theme="${effectiveTheme}">
<head>
<meta charset="utf-8">
<meta name="viewport" content="width=device-width, initial-scale=1">
<base target="_blank">
<style>
${themeCss}
  html, body {
    margin: 0;
    padding: 0;
    height: auto !important;
    min-height: 0;
    overflow: hidden;
    background: var(--bg-secondary, #f8f9fa);
  }
  body { font-family: "Microsoft YaHei", system-ui, sans-serif; }
</style>
</head>
<body>${html}</body>
</html>`;
}

export function measurePreviewContentHeight(doc: Document): number {
  const body = doc.body;
  if (!body) return 0;

  const root = body.querySelector(".qk-doc") ?? body.firstElementChild;
  if (root) {
    const view = doc.defaultView;
    const bodyRect = body.getBoundingClientRect();
    const rootRect = root.getBoundingClientRect();
    const marginBottom = view
      ? parseFloat(view.getComputedStyle(root).marginBottom) || 0
      : 0;
    return Math.ceil(rootRect.bottom - bodyRect.top + marginBottom);
  }

  return body.scrollHeight;
}
