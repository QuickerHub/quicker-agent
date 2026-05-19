import { useTheme } from "../hooks/useTheme";
import type { ThemeMode } from "../types";

const MODES: { mode: ThemeMode; label: string }[] = [
  { mode: "auto", label: "跟随系统" },
  { mode: "light", label: "浅色" },
  { mode: "dark", label: "暗色" },
];

export function ThemeSwitcher() {
  const { mode, setMode } = useTheme();

  return (
    <div className="theme-switcher" role="group" aria-label="preview theme">
      {MODES.map(({ mode: m, label }) => (
        <button
          key={m}
          type="button"
          className={`theme-btn${mode === m ? " active" : ""}`}
          onClick={() => setMode(m)}
        >
          {label}
        </button>
      ))}
    </div>
  );
}
