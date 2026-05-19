"""Fetch getquicker.net theme CSS variables from site.css (same source as Sharedaction pages)."""

from __future__ import annotations

import json
import logging
import re
import urllib.request
from datetime import datetime, timezone
from pathlib import Path
from typing import Any

logger = logging.getLogger(__name__)

THEME_CACHE_PATH = Path(__file__).resolve().parent / "static" / "gq-theme-vars.json"
SITE_CSS_URL = "https://getquicker.net/assets/css/site.css"

GQ_THEME_VAR_NAMES: tuple[str, ...] = (
    "--bg-primary",
    "--bg-secondary",
    "--bg-tertiary",
    "--bg-card",
    "--bg-input",
    "--bg-code",
    "--bg-pre",
    "--bg-hover",
    "--bg-quote",
    "--text-primary",
    "--text-secondary",
    "--text-muted",
    "--text-lighter",
    "--border-color",
    "--border-light",
    "--border-dark",
    "--link-color",
    "--link-hover",
    "--shadow-color",
    "--success-bg",
    "--warning-bg",
    "--info-bg",
    "--danger-bg",
    "--code-text",
    "--code-border",
)


def _parse_vars_from_site_css(css_text: str) -> dict[str, dict[str, str]]:
    def block_vars(selector_pattern: str) -> dict[str, str]:
        match = re.search(
            rf"{selector_pattern}\s*\{{([^}}]+)\}}",
            css_text,
            re.IGNORECASE,
        )
        if not match:
            return {}
        block = match.group(1)
        out: dict[str, str] = {}
        for name in GQ_THEME_VAR_NAMES:
            m = re.search(rf"{re.escape(name)}\s*:\s*([^;}}]+)", block)
            if m:
                out[name] = m.group(1).strip()
        return out

    return {
        "light": block_vars(r":root"),
        "dark": block_vars(r"\[data-theme=dark\]"),
    }


def fetch_theme_vars() -> dict[str, Any]:
    req = urllib.request.Request(
        SITE_CSS_URL,
        headers={"User-Agent": "qkagent-preview/1.0"},
    )
    with urllib.request.urlopen(req, timeout=30) as resp:
        css_text = resp.read().decode("utf-8", "replace")

    parsed = _parse_vars_from_site_css(css_text)
    if not parsed["light"] or not parsed["dark"]:
        raise RuntimeError("Could not parse theme variables from site.css")

    return {
        **parsed,
        "siteCssHref": SITE_CSS_URL,
        "fetchedAt": datetime.now(timezone.utc).isoformat(),
        "source": "site.css",
    }


def load_cached_theme() -> dict[str, Any] | None:
    if not THEME_CACHE_PATH.is_file():
        return None
    try:
        return json.loads(THEME_CACHE_PATH.read_text(encoding="utf-8"))
    except (OSError, json.JSONDecodeError) as exc:
        logger.warning("Could not read theme cache: %s", exc)
        return None


def save_theme_cache(payload: dict[str, Any]) -> Path:
    THEME_CACHE_PATH.parent.mkdir(parents=True, exist_ok=True)
    THEME_CACHE_PATH.write_text(
        json.dumps(payload, ensure_ascii=False, indent=2),
        encoding="utf-8",
        newline="\n",
    )
    return THEME_CACHE_PATH


def ensure_theme_cache(*, refresh: bool = False) -> dict[str, Any]:
    if not refresh:
        cached = load_cached_theme()
        if cached and cached.get("light") and cached.get("dark"):
            return cached

    payload = fetch_theme_vars()
    save_theme_cache(payload)
    return payload


def theme_vars_to_css(payload: dict[str, Any]) -> str:
    light = payload.get("light") or {}
    dark = payload.get("dark") or {}

    def block(selector: str, vars_map: dict[str, str]) -> str:
        lines = [f"{selector} {{"]
        for name, value in vars_map.items():
            lines.append(f"  {name}: {value};")
        lines.append("}")
        return "\n".join(lines)

    return "\n".join(
        [
            block(":root", light),
            block('[data-theme="dark"]', dark),
        ]
    )


def main() -> None:
    logging.basicConfig(level=logging.INFO)
    path = save_theme_cache(fetch_theme_vars())
    print(f"Wrote {path}")


if __name__ == "__main__":
    main()
