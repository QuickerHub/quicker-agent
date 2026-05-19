"""Parse Quicker fa: icon specs (same rules as preview/static/fa-icon.js)."""

from __future__ import annotations

import re
from dataclasses import dataclass

FA_STYLES: dict[str, str] = {
    "Light": "fal",
    "Solid": "fas",
    "Regular": "far",
    "Brands": "fab",
}

_PASCAL_KEBAB = re.compile(r"([a-z0-9])([A-Z])")


@dataclass(frozen=True)
class FaIconMeta:
    prefix: str
    icon_class: str
    color: str | None


def is_http_icon_url(icon_url: str | None) -> bool:
    if not icon_url:
        return False
    s = icon_url.strip().lower()
    return s.startswith(("http://", "https://", "//"))


def normalize_http_icon_url(icon_url: str | None) -> str | None:
    if not is_http_icon_url(icon_url):
        return None
    s = icon_url.strip()
    return f"https:{s}" if s.startswith("//") else s


def parse_quicker_fa_spec(spec: str | None) -> FaIconMeta | None:
    if not spec:
        return None
    raw = spec.strip()
    if not raw.lower().startswith("fa:"):
        return None

    body = raw[3:].strip()
    if not body:
        return None

    color: str | None = None
    hash_idx = body.find("#")
    if hash_idx >= 0:
        color = body[hash_idx:].strip() or None
        body = body[:hash_idx].rstrip(":").strip()
    else:
        parts = body.split(":")
        if len(parts) > 1:
            tail = parts[-1].strip()
            if tail and "_" not in tail and not tail[0].isupper():
                body = ":".join(parts[:-1]).strip()

    underscore = body.find("_")
    if underscore <= 0:
        return None

    style_key = body[:underscore]
    glyph = body[underscore + 1 :]
    if not glyph:
        return None

    prefix = FA_STYLES.get(style_key, "fal")
    icon_class = f"fa-{_pascal_to_kebab(glyph)}"
    return FaIconMeta(prefix=prefix, icon_class=icon_class, color=color)


def _pascal_to_kebab(name: str) -> str:
    text = name.replace("_", "-")
    return _PASCAL_KEBAB.sub(r"\1-\2", text).lower()
