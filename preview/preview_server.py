"""Serve static preview UI and read/write local action info.html files."""

from __future__ import annotations

import logging
import os
import re
from datetime import datetime, timezone
from pathlib import Path

import uvicorn
from action_info_api import fetch_action_info

logging.basicConfig(level=logging.INFO)
logger = logging.getLogger("preview_server")
from fastapi import FastAPI, HTTPException
from fastapi.responses import FileResponse
from fastapi.staticfiles import StaticFiles
from pydantic import BaseModel

STATIC_DIR = Path(__file__).resolve().parent / "static"
WEB_DIST_DIR = Path(__file__).resolve().parent / "web" / "dist"
ACTION_ID_RE = re.compile(
    r"^[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}$"
)


def actions_root() -> Path:
    from actions_paths import resolve_actions_root

    return resolve_actions_root()


def resolve_action_dir(action_id: str) -> Path:
    action_id = action_id.strip()
    if not ACTION_ID_RE.fullmatch(action_id):
        raise HTTPException(status_code=400, detail="Invalid action id.")
    action_dir = (actions_root() / action_id).resolve()
    if actions_root() not in action_dir.parents and action_dir != actions_root():
        raise HTTPException(status_code=400, detail="Invalid action path.")
    return action_dir


PAGE_HTML = "page.html"
INFO_HTML = "info.html"


def page_html_path(action_id: str) -> Path:
    return resolve_action_dir(action_id) / PAGE_HTML


def info_html_path(action_id: str) -> Path:
    return resolve_action_dir(action_id) / INFO_HTML


def compile_action_html(action_id: str) -> str:
    from compile_page import compile_page

    page_path = page_html_path(action_id)
    if not page_path.is_file():
        raise HTTPException(status_code=404, detail="page.html not found.")
    root = actions_root()
    return compile_page(page_path.read_text(encoding="utf-8"), root)


class ActionSummary(BaseModel):
    id: str
    htmlPath: str
    updatedAt: str | None
    sizeBytes: int | None
    title: str | None = None
    summary: str | None = None
    author: str | None = None
    iconUrl: str | None = None
    apiLastUpdateUtc: str | None = None
    # getquicker.net action list row markup (see site.css .action-icon-container)
    listIconKind: str | None = None
    listIconFaClasses: str | None = None
    listIconImgUrl: str | None = None
    listIconColor: str | None = None


class HtmlPayload(BaseModel):
    html: str


class FaIconMetaResponse(BaseModel):
    prefix: str
    iconClass: str
    color: str | None = None


class ThemeVarsResponse(BaseModel):
    light: dict[str, str]
    dark: dict[str, str]
    css: str
    fetchedAt: str | None = None
    source: str | None = None
    siteCssHref: str | None = None


def summary_from_dir(action_dir: Path) -> ActionSummary | None:
    page_file = action_dir / PAGE_HTML
    html_file = action_dir / INFO_HTML
    if not page_file.is_file() and not html_file.is_file():
        return None
    stat_source = page_file if page_file.is_file() else html_file
    if not ACTION_ID_RE.fullmatch(action_dir.name):
        return None

    stat = stat_source.stat()
    summary = ActionSummary(
        id=action_dir.name,
        htmlPath=str(page_file if page_file.is_file() else html_file),
        updatedAt=datetime.fromtimestamp(stat.st_mtime, tz=timezone.utc).isoformat(),
        sizeBytes=stat.st_size,
    )

    info = fetch_action_info(action_dir.name)
    if info is None:
        logger.warning("getactioninfo returned no data for %s", action_dir.name)
        return summary

    return summary.model_copy(
        update={
            "title": info.title,
            "summary": info.description,
            "author": info.author,
            "iconUrl": info.icon_url,
            "apiLastUpdateUtc": info.last_update_time_utc,
            "listIconKind": info.list_icon_kind,
            "listIconFaClasses": info.list_icon_fa_classes,
            "listIconImgUrl": info.list_icon_img_url,
            "listIconColor": info.list_icon_color,
        }
    )


app = FastAPI(title="qkagent HTML preview", version="0.3.0")


@app.get("/api/actions", response_model=list[ActionSummary])
def list_actions() -> list[ActionSummary]:
    root = actions_root()
    if not root.is_dir():
        return []

    items: list[ActionSummary] = []
    for entry in sorted(root.iterdir(), key=lambda p: p.name.lower()):
        if not entry.is_dir():
            continue
        summary = summary_from_dir(entry)
        if summary is not None:
            items.append(summary)
    return items


@app.get("/api/actions/{action_id}", response_model=ActionSummary)
def get_action(action_id: str) -> ActionSummary:
    action_dir = resolve_action_dir(action_id)
    summary = summary_from_dir(action_dir)
    if summary is None:
        raise HTTPException(status_code=404, detail="page.html not found.")
    return summary


@app.get("/api/actions/{action_id}/html")
def get_action_html(action_id: str) -> HtmlPayload:
    """Return editable source page.html."""
    path = page_html_path(action_id)
    if path.is_file():
        return HtmlPayload(html=path.read_text(encoding="utf-8"))
    legacy = info_html_path(action_id)
    if legacy.is_file():
        return HtmlPayload(html=legacy.read_text(encoding="utf-8"))
    raise HTTPException(status_code=404, detail="page.html not found.")


@app.get("/api/actions/{action_id}/preview")
def get_action_preview(action_id: str) -> HtmlPayload:
    """Return compiled HTML (CSS inlined) for iframe preview."""
    resolve_action_dir(action_id)
    if page_html_path(action_id).is_file():
        return HtmlPayload(html=compile_action_html(action_id))
    legacy = info_html_path(action_id)
    if legacy.is_file():
        return HtmlPayload(html=legacy.read_text(encoding="utf-8"))
    raise HTTPException(status_code=404, detail="page.html not found.")


@app.put("/api/actions/{action_id}/html")
def put_action_html(action_id: str, body: HtmlPayload) -> dict[str, bool]:
    """Save source page.html (class-based HTML, not inlined)."""
    path = page_html_path(action_id)
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(body.html, encoding="utf-8", newline="\n")
    return {"ok": True}


@app.get("/api/config")
def get_config() -> dict[str, str]:
    return {"actionsRoot": str(actions_root())}


@app.get("/api/theme", response_model=ThemeVarsResponse)
def get_theme(refresh: bool = False) -> ThemeVarsResponse:
    """Theme CSS variables from getquicker.net site.css (cached in static/)."""
    from gq_theme_fetch import ensure_theme_cache, theme_vars_to_css

    payload = ensure_theme_cache(refresh=refresh)
    return ThemeVarsResponse(
        light=payload.get("light") or {},
        dark=payload.get("dark") or {},
        css=theme_vars_to_css(payload),
        fetchedAt=payload.get("fetchedAt"),
        source=payload.get("source"),
        siteCssHref=payload.get("siteCssHref"),
    )


@app.get("/api/icons/fa", response_model=FaIconMetaResponse)
def get_fa_icon_meta(spec: str) -> FaIconMetaResponse:
    """Parse Quicker fa: spec to Font Awesome 5 classes (same CDN as getquicker.net)."""
    from fa_icon import parse_quicker_fa_spec

    meta = parse_quicker_fa_spec(spec)
    if meta is None:
        raise HTTPException(status_code=400, detail="Invalid or unsupported fa: icon spec.")
    return FaIconMetaResponse(
        prefix=meta.prefix,
        iconClass=meta.icon_class,
        color=meta.color,
    )


def _spa_index() -> Path:
    built = WEB_DIST_DIR / "index.html"
    if built.is_file():
        return built
    return STATIC_DIR / "index.html"


@app.get("/")
def index() -> FileResponse:
    return FileResponse(
        _spa_index(),
        media_type="text/html; charset=utf-8",
    )


class Utf8StaticFiles(StaticFiles):
    async def get_response(self, path: str, scope):  # type: ignore[override]
        response = await super().get_response(path, scope)
        if path.endswith(".js"):
            response.headers["content-type"] = "text/javascript; charset=utf-8"
        elif path.endswith(".css"):
            response.headers["content-type"] = "text/css; charset=utf-8"
        return response


app.mount("/static", Utf8StaticFiles(directory=STATIC_DIR), name="static")

if (WEB_DIST_DIR / "assets").is_dir():
    app.mount(
        "/assets",
        StaticFiles(directory=WEB_DIST_DIR / "assets"),
        name="web-assets",
    )


def main() -> None:
    host = os.environ.get("QKAGENT_PREVIEW_HOST", "127.0.0.1")
    port = int(os.environ.get("QKAGENT_PREVIEW_PORT", "8765"))
    print(f"Actions root: {actions_root()}")
    if (WEB_DIST_DIR / "index.html").is_file():
        print(f"Preview UI (built): http://{host}:{port}/")
    else:
        print(f"Preview API: http://{host}:{port}/")
        print("  UI not built — run: cd preview/web && npm install && npm run build")
        print("  Dev with HMR: preview/run-dev.ps1  →  http://127.0.0.1:5176/")
    uvicorn.run(app, host=host, port=port, log_level="info")


if __name__ == "__main__":
    main()
