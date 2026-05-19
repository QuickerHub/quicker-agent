"""Fetch shared-action metadata from getquicker.net open API."""

from __future__ import annotations

import json
import logging
import urllib.error
import urllib.request
from dataclasses import dataclass
from typing import Final

logger = logging.getLogger(__name__)

ACTION_INFO_URL: Final[str] = (
    "https://getquicker.net/open/api/actions/getactioninfo?id={action_id}"
)
USER_AGENT: Final[str] = (
    "Mozilla/5.0 (compatible; qkagent-preview/0.1; +https://getquicker.net)"
)


@dataclass(frozen=True)
class ActionInfo:
    id: str
    title: str | None
    description: str | None
    author: str | None
    icon_url: str | None
    last_update_time_utc: str | None
    revision: int | None = None
    user_count: int | None = None
    vote_count: int | None = None
    # From getquicker.net action list HTML (action-icon-container + fal / img)
    list_icon_kind: str | None = None
    list_icon_fa_classes: str | None = None
    list_icon_img_url: str | None = None
    list_icon_color: str | None = None


# Cache successes only — do not cache failures (e.g. transient network errors).
_info_cache: dict[str, ActionInfo] = {}


def clear_action_info_cache() -> None:
    _info_cache.clear()


def fetch_action_info(action_id: str) -> ActionInfo | None:
    cached = _info_cache.get(action_id)
    if cached is not None:
        return cached

    info = _fetch_action_info_uncached(action_id)
    if info is not None:
        _info_cache[action_id] = info
    return info


def _fetch_action_info_uncached(action_id: str) -> ActionInfo | None:
    url = ACTION_INFO_URL.format(action_id=action_id)
    request = urllib.request.Request(
        url,
        headers={
            "User-Agent": USER_AGENT,
            "Accept": "application/json",
            "Referer": "https://getquicker.net/",
        },
    )
    try:
        with urllib.request.urlopen(request, timeout=30) as response:
            raw = response.read().decode("utf-8")
            payload = json.loads(raw)
    except urllib.error.HTTPError as exc:
        logger.warning("getactioninfo HTTP %s for %s", exc.code, action_id)
        return None
    except (urllib.error.URLError, TimeoutError) as exc:
        logger.warning("getactioninfo network error for %s: %s", action_id, exc)
        return None
    except (json.JSONDecodeError, ValueError) as exc:
        logger.warning("getactioninfo invalid JSON for %s: %s", action_id, exc)
        return None

    if not isinstance(payload, dict):
        logger.warning("getactioninfo unexpected payload for %s", action_id)
        return None

    if payload.get("error") or payload.get("success") is False:
        logger.warning("getactioninfo API error for %s: %s", action_id, payload)
        return None

    revision = payload.get("revision")
    user_count = payload.get("userCount")
    vote_count = payload.get("voteCount")

    info = ActionInfo(
        id=str(payload.get("id") or action_id),
        title=_str_or_none(payload.get("title")),
        description=_str_or_none(payload.get("description")),
        author=_str_or_none(payload.get("createUserNickName")),
        icon_url=_str_or_none(payload.get("icon")),
        last_update_time_utc=_str_or_none(payload.get("lastUpdateTimeUtc")),
        revision=int(revision) if isinstance(revision, int) else None,
        user_count=int(user_count) if isinstance(user_count, int) else None,
        vote_count=int(vote_count) if isinstance(vote_count, int) else None,
    )
    return _enrich_with_list_icon(info)


def _enrich_with_list_icon(info: ActionInfo) -> ActionInfo:
    from gq_web_icon import fetch_list_icon

    try:
        listed = fetch_list_icon(info.id)
    except Exception as exc:  # noqa: BLE001
        logger.warning("list icon scrape failed for %s: %s", info.id, exc)
        return info

    if listed is None:
        return info

    return ActionInfo(
        id=info.id,
        title=info.title,
        description=info.description,
        author=info.author,
        icon_url=info.icon_url,
        last_update_time_utc=info.last_update_time_utc,
        revision=info.revision,
        user_count=info.user_count,
        vote_count=info.vote_count,
        list_icon_kind=listed.kind,
        list_icon_fa_classes=listed.fa_classes,
        list_icon_img_url=listed.img_url,
        list_icon_color=listed.color,
    )


def _str_or_none(value: object) -> str | None:
    if value is None:
        return None
    text = str(value).strip()
    return text or None
