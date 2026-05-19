"""Resolve actions root (mirrors action_doc_builder.paths)."""

from __future__ import annotations

import os
from pathlib import Path

ACTIONS_README = "README.md"
DEFAULT_RELATIVE = "actions"


def repo_root_from(start: Path | None = None) -> Path | None:
    current = (start or Path.cwd()).resolve()
    for _ in range(12):
        if (current / DEFAULT_RELATIVE / ACTIONS_README).is_file():
            return current
        if current.parent == current:
            break
        current = current.parent
    return None


def resolve_actions_root() -> Path:
    env = os.environ.get("QKAGENT_ACTIONS_ROOT", "").strip()
    if env:
        root = Path(env).expanduser()
        if not root.is_absolute():
            base = repo_root_from() or Path.cwd()
            root = (base / root).resolve()
        else:
            root = root.resolve()
        return root

    for start in (Path.cwd(), Path(__file__).resolve().parent.parent):
        found = repo_root_from(start)
        if found is not None:
            return (found / DEFAULT_RELATIVE).resolve()

    return (Path.home() / ".quicker" / "actions").resolve()
