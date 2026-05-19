import { useCallback, useEffect, useMemo, useState } from "react";
import {
  fetchActions,
  fetchActionHtml,
  fetchConfig,
  saveActionHtml,
} from "./api/client";
import { PreviewBar } from "./components/PreviewBar";
import { PreviewFrame } from "./components/PreviewFrame";
import { Sidebar } from "./components/Sidebar";
import { useTheme } from "./hooks/useTheme";
import type { ActionSummary } from "./types";

export default function App() {
  const { effective, mode } = useTheme();
  const [actionsRoot, setActionsRoot] = useState("");
  const [actions, setActions] = useState<ActionSummary[]>([]);
  const [selectedId, setSelectedId] = useState<string | null>(null);
  const [htmlSource, setHtmlSource] = useState("");
  const [previewVersion, setPreviewVersion] = useState(0);

  const actionsById = useMemo(
    () => new Map(actions.map((a) => [a.id, a])),
    [actions]
  );

  const selectedAction = selectedId ? actionsById.get(selectedId) ?? null : null;

  const reloadActions = useCallback(async () => {
    const list = await fetchActions();
    setActions(list);
    return list;
  }, []);

  const selectAction = useCallback(async (id: string) => {
    setSelectedId(id);
    const params = new URLSearchParams(window.location.search);
    params.set("id", id);
    const next = `${window.location.pathname}?${params}`;
    window.history.replaceState(null, "", next);
    try {
      const data = await fetchActionHtml(id);
      setHtmlSource(data.html);
    } catch (e) {
      console.error(e);
    }
  }, []);

  useEffect(() => {
    document.title = "qkagent · 动作简介预览";
    void fetchConfig().then((c) => setActionsRoot(c.actionsRoot));
    void reloadActions().then((list) => {
      const id = new URLSearchParams(window.location.search).get("id");
      if (id && list.some((a) => a.id === id)) {
        void selectAction(id);
      }
    });
  }, [reloadActions, selectAction]);

  useEffect(() => {
    setPreviewVersion((v) => v + 1);
  }, [effective, mode]);

  const saveCurrent = useCallback(async () => {
    if (!selectedId) return;
    try {
      await saveActionHtml(selectedId, htmlSource);
      await reloadActions();
      setPreviewVersion((v) => v + 1);
    } catch (e) {
      console.error(e);
    }
  }, [selectedId, htmlSource, reloadActions]);

  useEffect(() => {
    const onKeyDown = (e: KeyboardEvent) => {
      if ((e.ctrlKey || e.metaKey) && e.key === "s") {
        e.preventDefault();
        void saveCurrent();
      }
    };
    window.addEventListener("keydown", onKeyDown);
    return () => window.removeEventListener("keydown", onKeyDown);
  }, [saveCurrent]);

  return (
    <div className="app">
      <Sidebar
        actionsRoot={actionsRoot}
        actions={actions}
        selectedId={selectedId}
        onSelect={(id) => void selectAction(id)}
      />
      <main className="main">
        <textarea
          className="html-editor"
          value={htmlSource}
          hidden
          readOnly
          aria-hidden
        />
        <section className="preview-area">
          <PreviewBar action={selectedAction} />
          <PreviewFrame actionId={selectedId} reloadToken={previewVersion} />
        </section>
      </main>
    </div>
  );
}
