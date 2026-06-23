#!/usr/bin/env python3
"""Validate concrete Nuxt pages are discoverable from the modern and legacy sidebars."""
from __future__ import annotations

import re
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
WEB = ROOT / "frontend/garmetix-web"
PAGES_DIR = WEB / "pages"
SHELL_FILES = [
    WEB / "components/AppShell.vue",
    WEB / "components/AppShellLegacy.vue",
]

APPROVED_NO_MENU = {
    "/access-denied",  # Auth guard target; should not appear as a normal menu item.
}


def page_path(vue_file: Path) -> str:
    rel = vue_file.relative_to(PAGES_DIR).with_suffix("")
    parts = list(rel.parts)
    if any(part.startswith("[") and part.endswith("]") for part in parts):
        return ""
    if parts and parts[-1] == "index":
        parts = parts[:-1]
    return ("/" + "/".join(parts)).replace("//", "/") or "/"


def shell_routes(shell_file: Path) -> set[str]:
    text = shell_file.read_text(encoding="utf-8")
    routes = set(re.findall(r"to:\s*['\"]([^'\"]+)['\"]", text))
    return {route.rstrip("/") or "/" for route in routes}


def main() -> int:
    pages = sorted(
        path for path in (page_path(file) for file in PAGES_DIR.rglob("*.vue"))
        if path and path not in APPROVED_NO_MENU
    )
    failed = False
    for shell in SHELL_FILES:
        routes = shell_routes(shell)
        missing = [page for page in pages if page not in routes]
        if missing:
            failed = True
            print(f"Navigation menu coverage failed for {shell.relative_to(ROOT)}:", file=sys.stderr)
            for page in missing:
                print(f"  - {page}", file=sys.stderr)
    if failed:
        return 1
    print(f"Navigation menu coverage passed: {len(pages)} concrete pages are discoverable in both modern and legacy sidebars.")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
