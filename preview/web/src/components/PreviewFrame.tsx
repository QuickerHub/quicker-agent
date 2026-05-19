import { useCallback, useEffect, useRef, useState } from "react";
import { fetchActionPreview } from "../api/client";
import { useTheme } from "../hooks/useTheme";
import {
  DEFAULT_PREVIEW_WIDTH,
  measurePreviewContentHeight,
  wrapPreviewDocument,
} from "../lib/previewDocument";

const POLL_MS = 2000;

interface PreviewFrameProps {
  actionId: string | null;
  /** Bump to force reload preview (e.g. theme change). */
  reloadToken: number;
}

export function PreviewFrame({ actionId, reloadToken }: PreviewFrameProps) {
  const { effective, payload } = useTheme();
  const iframeRef = useRef<HTMLIFrameElement>(null);
  const [srcDoc, setSrcDoc] = useState("");
  const lastHashRef = useRef("");

  const resizeFrame = useCallback(() => {
    const iframe = iframeRef.current;
    const doc = iframe?.contentDocument;
    if (!iframe || !doc) return;
    iframe.style.height = "0px";
    const height = measurePreviewContentHeight(doc);
    iframe.style.height = `${Math.max(height, 120)}px`;
  }, []);

  const loadPreview = useCallback(async () => {
    if (!actionId) {
      setSrcDoc("");
      return;
    }
    try {
      const data = await fetchActionPreview(actionId);
      const hash = `${data.html.length}:${data.html.slice(0, 64)}`;
      if (hash === lastHashRef.current) return;
      lastHashRef.current = hash;

      const themeCss = payload?.css ?? "";
      setSrcDoc(
        wrapPreviewDocument(data.html, effective, themeCss)
      );
    } catch (e) {
      console.error(e);
      setSrcDoc(
        wrapPreviewDocument(
          `<p style="color:#b91c1c">Preview failed</p>`,
          effective,
          payload?.css ?? ""
        )
      );
    }
  }, [actionId, effective, payload?.css]);

  useEffect(() => {
    lastHashRef.current = "";
    void loadPreview();
  }, [loadPreview, reloadToken]);

  useEffect(() => {
    if (!actionId) return;
    const timer = window.setInterval(() => void loadPreview(), POLL_MS);
    const onFocus = () => void loadPreview();
    window.addEventListener("focus", onFocus);
    return () => {
      window.clearInterval(timer);
      window.removeEventListener("focus", onFocus);
    };
  }, [actionId, loadPreview]);

  const onLoad = () => {
    requestAnimationFrame(() => {
      resizeFrame();
      const doc = iframeRef.current?.contentDocument;
      doc?.querySelectorAll("img").forEach((img) => {
        if (!img.complete) {
          img.addEventListener("load", () => resizeFrame(), { once: true });
        }
      });
    });
  };

  if (!actionId) {
    return (
      <div className="preview-wrap site-themed preview-wrap--empty">
        <p className="empty-hint">从左侧选择一个动作</p>
      </div>
    );
  }

  return (
    <div className="preview-wrap site-themed">
      <div
        className="preview-frame-outer site-chrome"
        style={{ maxWidth: DEFAULT_PREVIEW_WIDTH }}
      >
        <iframe
          ref={iframeRef}
          title="preview"
          sandbox="allow-same-origin"
          srcDoc={srcDoc}
          onLoad={onLoad}
        />
      </div>
    </div>
  );
}
