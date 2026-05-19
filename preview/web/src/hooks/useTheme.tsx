import {
  createContext,
  useCallback,
  useContext,
  useEffect,
  useMemo,
  useState,
  type ReactNode,
} from "react";
import { fetchTheme } from "../api/client";
import type { EffectiveTheme, ThemeMode, ThemePayload } from "../types";

const STORAGE_KEY = "qkagent-preview-theme";

interface ThemeContextValue {
  mode: ThemeMode;
  effective: EffectiveTheme;
  payload: ThemePayload | null;
  setMode: (mode: ThemeMode) => void;
  refreshTheme: () => Promise<void>;
}

const ThemeContext = createContext<ThemeContextValue | null>(null);

function readStoredMode(): ThemeMode {
  const saved = localStorage.getItem(STORAGE_KEY);
  if (saved === "light" || saved === "dark" || saved === "auto") return saved;
  return "auto";
}

function resolveEffective(mode: ThemeMode, prefersDark: boolean): EffectiveTheme {
  if (mode === "auto") return prefersDark ? "dark" : "light";
  return mode;
}

function applyShellTheme(payload: ThemePayload, effective: EffectiveTheme): void {
  const vars = effective === "dark" ? payload.dark : payload.light;
  const root = document.documentElement;
  for (const [name, value] of Object.entries(vars)) {
    root.style.setProperty(name, value);
  }
  root.setAttribute("data-theme", effective);
}

export function ThemeProvider({ children }: { children: ReactNode }) {
  const [mode, setModeState] = useState<ThemeMode>(readStoredMode);
  const [payload, setPayload] = useState<ThemePayload | null>(null);
  const [prefersDark, setPrefersDark] = useState(
    () => window.matchMedia("(prefers-color-scheme: dark)").matches
  );

  const effective = useMemo(
    () => resolveEffective(mode, prefersDark),
    [mode, prefersDark]
  );

  const load = useCallback(async (refresh = false) => {
    const data = await fetchTheme(refresh);
    setPayload(data);
    applyShellTheme(data, resolveEffective(mode, prefersDark));
  }, [mode, prefersDark]);

  useEffect(() => {
    void load();
  }, [load]);

  useEffect(() => {
    if (payload) applyShellTheme(payload, effective);
  }, [payload, effective]);

  useEffect(() => {
    const mq = window.matchMedia("(prefers-color-scheme: dark)");
    const onChange = () => setPrefersDark(mq.matches);
    mq.addEventListener("change", onChange);
    return () => mq.removeEventListener("change", onChange);
  }, []);

  const setMode = useCallback((next: ThemeMode) => {
    setModeState(next);
    localStorage.setItem(STORAGE_KEY, next);
  }, []);

  const value = useMemo(
    () => ({
      mode,
      effective,
      payload,
      setMode,
      refreshTheme: () => load(true),
    }),
    [mode, effective, payload, setMode, load]
  );

  return (
    <ThemeContext.Provider value={value}>{children}</ThemeContext.Provider>
  );
}

export function useTheme(): ThemeContextValue {
  const ctx = useContext(ThemeContext);
  if (!ctx) throw new Error("useTheme must be used within ThemeProvider");
  return ctx;
}
