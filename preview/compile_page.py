"""Compile page.html with shared intro.css (same as action_doc_builder)."""

from __future__ import annotations

from pathlib import Path

from action_doc_builder.build import _load_css, inline_page_html


def compile_page(page_html: str, actions_root: Path) -> str:
    return inline_page_html(page_html, _load_css(actions_root))
