"""CLI: page.html + intro.css → info.html."""

from __future__ import annotations

import argparse
import sys

from .build import build_all
from .paths import resolve_actions_root


def main(argv: list[str] | None = None) -> int:
    parser = argparse.ArgumentParser(
        description="Inline actions/_shared/intro.css into actions/<id>/page.html → info.html"
    )
    parser.add_argument("--root", help="Actions root directory")
    parser.add_argument("--id", help="Build one action folder")
    parser.add_argument("--force", action="store_true", help="Rewrite info.html")
    args = parser.parse_args(argv)

    root = resolve_actions_root(args.root)
    try:
        built = build_all(root, action_id=args.id, force=args.force)
    except FileNotFoundError as exc:
        print(exc, file=sys.stderr)
        return 1

    if not built:
        print(f"No page.html under {root}")
        return 0

    for name in built:
        print(f"built {name}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
