"""Serve static preview UI and read/write local action info.html files."""

from __future__ import annotations

import os
import re
from datetime import datetime, timezone
from pathlib import Path

import uvicorn
from fastapi import FastAPI, HTTPException
from fastapi.responses import FileResponse
from fastapi.staticfiles import StaticFiles
from pydantic import BaseModel

STATIC_DIR = Path(__file__).resolve().parent / "static"
ACTION_ID_RE = re.compile(
    r"^[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}$"
)


def actions_root() -> Path:
    root = os.environ.get("QKAGENT_ACTIONS_ROOT", "").strip()
    if not root:
        root = str(Path.home() / ".quicker" / "actions")
    return Path(root).expanduser().resolve()


def resolve_action_dir(action_id: str) -> Path:
    action_id = action_id.strip()
    if not ACTION_ID_RE.fullmatch(action_id):
        raise HTTPException(status_code=400, detail="Invalid action id.")
    action_dir = (actions_root() / action_id).resolve()
    if actions_root() not in action_dir.parents and action_dir != actions_root():
        raise HTTPException(status_code=400, detail="Invalid action path.")
    return action_dir


def info_html_path(action_id: str) -> Path:
    return resolve_action_dir(action_id) / "info.html"


class ActionSummary(BaseModel):
    id: str
    htmlPath: str
    updatedAt: str | None
    sizeBytes: int | None


class HtmlPayload(BaseModel):
    html: str


app = FastAPI(title="qkagent HTML preview", version="0.1.0")


@app.get("/api/actions", response_model=list[ActionSummary])
def list_actions() -> list[ActionSummary]:
    root = actions_root()
    if not root.is_dir():
        return []

    items: list[ActionSummary] = []
    for entry in sorted(root.iterdir(), key=lambda p: p.name.lower()):
        if not entry.is_dir():
            continue
        html_file = entry / "info.html"
        if not html_file.is_file():
            continue
        if not ACTION_ID_RE.fullmatch(entry.name):
            continue
        stat = html_file.stat()
        items.append(
            ActionSummary(
                id=entry.name,
                htmlPath=str(html_file),
                updatedAt=datetime.fromtimestamp(stat.st_mtime, tz=timezone.utc).isoformat(),
                sizeBytes=stat.st_size,
            )
        )
    return items


@app.get("/api/actions/{action_id}/html")
def get_action_html(action_id: str) -> HtmlPayload:
    path = info_html_path(action_id)
    if not path.is_file():
        raise HTTPException(status_code=404, detail="info.html not found.")
    return HtmlPayload(html=path.read_text(encoding="utf-8"))


@app.put("/api/actions/{action_id}/html")
def put_action_html(action_id: str, body: HtmlPayload) -> dict[str, bool]:
    path = info_html_path(action_id)
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(body.html, encoding="utf-8", newline="\n")
    return {"ok": True}


@app.get("/api/config")
def get_config() -> dict[str, str]:
    return {"actionsRoot": str(actions_root())}


@app.get("/")
def index() -> FileResponse:
    return FileResponse(
        STATIC_DIR / "index.html",
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


def main() -> None:
    host = os.environ.get("QKAGENT_PREVIEW_HOST", "127.0.0.1")
    port = int(os.environ.get("QKAGENT_PREVIEW_PORT", "8765"))
    print(f"Actions root: {actions_root()}")
    print(f"Preview UI: http://{host}:{port}/")
    uvicorn.run(app, host=host, port=port, log_level="info")


if __name__ == "__main__":
    main()
