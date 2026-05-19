import type { ActionSummary, HtmlPayload, ThemePayload } from "../types";

async function parseJson<T>(res: Response): Promise<T> {
  if (!res.ok) {
    const text = await res.text();
    throw new Error(text || `HTTP ${res.status}`);
  }
  return res.json() as Promise<T>;
}

export async function fetchConfig(): Promise<{ actionsRoot: string }> {
  return parseJson(await fetch("/api/config"));
}

export async function fetchActions(): Promise<ActionSummary[]> {
  return parseJson(await fetch("/api/actions"));
}

export async function fetchActionHtml(actionId: string): Promise<HtmlPayload> {
  return parseJson(
    await fetch(`/api/actions/${encodeURIComponent(actionId)}/html`)
  );
}

export async function fetchActionPreview(actionId: string): Promise<HtmlPayload> {
  return parseJson(
    await fetch(`/api/actions/${encodeURIComponent(actionId)}/preview`)
  );
}

export async function saveActionHtml(
  actionId: string,
  html: string
): Promise<void> {
  await parseJson(
    await fetch(`/api/actions/${encodeURIComponent(actionId)}/html`, {
      method: "PUT",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({ html }),
    })
  );
}

export async function fetchTheme(refresh = false): Promise<ThemePayload> {
  const q = refresh ? "?refresh=true" : "";
  return parseJson(await fetch(`/api/theme${q}`));
}
