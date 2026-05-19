export type ThemeMode = "auto" | "light" | "dark";

export type EffectiveTheme = "light" | "dark";

export interface ActionSummary {
  id: string;
  htmlPath: string;
  updatedAt: string | null;
  sizeBytes: number | null;
  title: string | null;
  summary: string | null;
  author: string | null;
  iconUrl: string | null;
  apiLastUpdateUtc: string | null;
  listIconKind: string | null;
  listIconFaClasses: string | null;
  listIconImgUrl: string | null;
  listIconColor: string | null;
}

export interface ThemePayload {
  light: Record<string, string>;
  dark: Record<string, string>;
  css: string;
  fetchedAt?: string | null;
  source?: string | null;
  siteCssHref?: string | null;
}

export interface HtmlPayload {
  html: string;
}
