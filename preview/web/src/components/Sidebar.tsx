import type { ActionSummary } from "../types";
import { ActionIcon } from "./ActionIcon";

interface SidebarProps {
  actionsRoot: string;
  actions: ActionSummary[];
  selectedId: string | null;
  onSelect: (id: string) => void;
}

export function Sidebar({
  actionsRoot,
  actions,
  selectedId,
  onSelect,
}: SidebarProps) {
  return (
    <aside className="sidebar">
      <div className="sidebar-header">
        <h1>动作简介预览</h1>
        <p>{actionsRoot || "…"}</p>
      </div>
      <ul className="action-list">
        {actions.length === 0 ? (
          <li className="empty-hint">
            未找到动作。先执行 qkagent pull --code &lt;id&gt;
          </li>
        ) : (
          actions.map((action) => {
            const title = action.title ?? `${action.id.slice(0, 8)}…`;
            const date = action.updatedAt
              ? new Date(action.updatedAt).toLocaleString()
              : "";
            const author = action.author ? ` · ${action.author}` : "";

            return (
              <li key={action.id}>
                <button
                  type="button"
                  className={`action-item-btn${
                    selectedId === action.id ? " active" : ""
                  }`}
                  onClick={() => onSelect(action.id)}
                >
                  <ActionIcon action={action} />
                  <span className="action-item-body">
                    <span className="action-item-title">{title}</span>
                    {action.summary ? (
                      <span className="action-item-summary">
                        {action.summary}
                      </span>
                    ) : null}
                    <span className="meta">
                      {date}
                      {author}
                    </span>
                  </span>
                </button>
              </li>
            );
          })
        )}
      </ul>
    </aside>
  );
}
