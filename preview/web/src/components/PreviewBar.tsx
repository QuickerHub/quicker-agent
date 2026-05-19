import { actionPageUrl } from "../lib/previewDocument";
import type { ActionSummary } from "../types";
import { ThemeSwitcher } from "./ThemeSwitcher";

interface PreviewBarProps {
  action: ActionSummary | null;
}

export function PreviewBar({ action }: PreviewBarProps) {
  if (!action) return null;

  const url = actionPageUrl(action.id);
  const linkText = action.title ? `${action.title} · ${url}` : url;

  return (
    <header className="preview-bar">
      <span className="preview-bar-label">动作主页</span>
      <a
        className="preview-bar-link"
        href={url}
        target="_blank"
        rel="noopener noreferrer"
        title={url}
      >
        {linkText}
      </a>
      <ThemeSwitcher />
    </header>
  );
}
