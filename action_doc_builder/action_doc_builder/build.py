"""Build info.html from page.html + shared intro.css (CSS inlined)."""

from __future__ import annotations

import re
from pathlib import Path

import css_inline

from .paths import list_action_dirs, resolve_actions_root

SOURCE_FILE = "page.html"
OUTPUT_FILE = "info.html"
SHARED_CSS = Path("_shared") / "intro.css"


def _load_css(actions_root: Path) -> str:
    css_path = actions_root / SHARED_CSS
    if not css_path.is_file():
        raise FileNotFoundError(f"Missing shared stylesheet: {css_path}")
    return css_path.read_text(encoding="utf-8")


def _wrap_document(fragment: str) -> str:
    body = fragment.strip()
    if not body.lower().startswith("<!doctype") and not body.lower().startswith("<html"):
        body = f"<html><head><meta charset=\"utf-8\"></head><body>{body}</body></html>"
    return body


def _extract_body(html: str) -> str:
    match = re.search(r"<body[^>]*>(.*)</body>", html, re.DOTALL | re.IGNORECASE)
    if match:
        return match.group(1).strip()
    return html.strip()


def inline_page_html(page_html: str, css: str) -> str:
    document = _wrap_document(page_html)
    inlined = css_inline.inline(document, extra_css=css)
    return _extract_body(inlined)


def build_one(action_dir: Path, *, actions_root: Path, css: str, force: bool = False) -> bool:
    source = action_dir / SOURCE_FILE
    if not source.is_file():
        return False

    page_html = source.read_text(encoding="utf-8")
    html = inline_page_html(page_html, css)

    out_path = action_dir / OUTPUT_FILE
    if not force and out_path.is_file() and out_path.read_text(encoding="utf-8") == html:
        return True

    out_path.write_text(html + "\n", encoding="utf-8", newline="\n")
    return True


def build_all(
    actions_root: Path | None = None,
    *,
    action_id: str | None = None,
    force: bool = False,
) -> list[str]:
    root = actions_root or resolve_actions_root()
    css = _load_css(root)
    built: list[str] = []

    dirs = list_action_dirs(root)
    if action_id:
        dirs = [root / action_id]

    for action_dir in dirs:
        if not action_dir.is_dir():
            continue
        if build_one(action_dir, actions_root=root, css=css, force=force):
            built.append(action_dir.name)

    return built
