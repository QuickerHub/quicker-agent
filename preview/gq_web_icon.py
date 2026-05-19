"""Resolve action icons from getquicker.net HTML (action list pages)."""

from __future__ import annotations

import logging
import re
import urllib.request
from dataclasses import dataclass

logger = logging.getLogger(__name__)

LIST_URL = "https://getquicker.net/Exe/16/Actions?page={page}&sort=update"
USER_AGENT = (
    "Mozilla/5.0 (compatible; qkagent-preview/0.1; +https://getquicker.net)"
)

_I_TAG_RE = re.compile(
    r'<i\s+class="(?P<classes>[^"]+)"[^>]*\bsharedAction="(?P<id>[0-9a-fA-F-]{36})"',
    re.IGNORECASE,
)
_IMG_TAG_RE = re.compile(
    r'<img\s+class="action-icon"\s+src="(?P<src>[^"]+)"[^>]*\bsharedAction="(?P<id>[0-9a-fA-F-]{36})"',
    re.IGNORECASE,
)


@dataclass(frozen=True)
class GqListIcon:
    """Icon as rendered on getquicker action list rows."""

    kind: str  # "fa" | "img"
    fa_classes: str | None = None
    img_url: str | None = None
    color: str | None = None


_icon_cache: dict[str, GqListIcon | None] = {}


def fetch_list_icon(action_id: str, *, max_pages: int = 8) -> GqListIcon | None:
    action_id = action_id.strip().lower()
    if action_id in _icon_cache:
        return _icon_cache[action_id]

    found: GqListIcon | None = None
    for page in range(1, max_pages + 1):
        found = _parse_list_page(action_id, page)
        if found is not None:
            break

    _icon_cache[action_id] = found
    return found


def _parse_list_page(action_id: str, page: int) -> GqListIcon | None:
    url = LIST_URL.format(page=page)
    request = urllib.request.Request(url, headers={"User-Agent": USER_AGENT})
    try:
        html = urllib.request.urlopen(request, timeout=30).read().decode(
            "utf-8", "replace"
        )
    except OSError as exc:
        logger.warning("action list fetch failed page=%s: %s", page, exc)
        return None

    if action_id not in html.lower():
        return None

    for match in _I_TAG_RE.finditer(html):
        if match.group("id").lower() != action_id:
            continue
        classes = " ".join(match.group("classes").split())
        color = _extract_inline_color(match.group(0))
        return GqListIcon(kind="fa", fa_classes=classes, color=color)

    for match in _IMG_TAG_RE.finditer(html):
        if match.group("id").lower() != action_id:
            continue
        return GqListIcon(kind="img", img_url=match.group("src"))

    return None


def _extract_inline_color(tag_html: str) -> str | None:
    match = re.search(r"color\s*:\s*([^;\"']+)", tag_html, re.IGNORECASE)
    if not match:
        return None
    return match.group(1).strip() or None
