#!/usr/bin/env python3
"""Validate that concrete Nuxt pages have explicit frontend access rules.

This is a browserless safety check: it prevents a future page from becoming
available through the default "no explicit page rule" branch without being
reviewed in useAccessControl.ts.
"""
from __future__ import annotations

import re
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
PAGES_DIR = ROOT / "frontend/garmetix-web/pages"
ACCESS_FILE = ROOT / "frontend/garmetix-web/composables/useAccessControl.ts"

APPROVED_EXCEPTIONS = {
    "/access-denied",  # public denial/explanation page handled by auth.global.ts
    "/[module]",      # legacy dynamic placeholder; real modules should use concrete pages
}


def page_path(vue_file: Path) -> str:
    rel = vue_file.relative_to(PAGES_DIR).with_suffix("")
    parts = list(rel.parts)
    if parts and parts[-1] == "index":
        parts = parts[:-1]
    return ("/" + "/".join(parts)).replace("//", "/") or "/"


def extract_rules(source: str) -> list[tuple[str, bool]]:
    rules: list[tuple[str, bool]] = []
    for match in re.finditer(r"\{\s*path:\s*'([^']+)'(?P<body>.*?)\}", source, flags=re.DOTALL):
        path = match.group(1).rstrip("/") or "/"
        exact = bool(re.search(r"exact:\s*true", match.group("body")))
        rules.append((path, exact))
    return rules


def covered_by_rule(path: str, rules: list[tuple[str, bool]]) -> bool:
    cleaned = path.rstrip("/") or "/"
    for rule_path, exact in rules:
        if exact and cleaned == rule_path:
            return True
        if not exact and (cleaned == rule_path or cleaned.startswith(f"{rule_path}/")):
            return True
    return False


def main() -> int:
    source = ACCESS_FILE.read_text(encoding="utf-8")
    rules = extract_rules(source)
    pages = sorted(page_path(file) for file in PAGES_DIR.rglob("*.vue"))

    missing = [path for path in pages if path not in APPROVED_EXCEPTIONS and not covered_by_rule(path, rules)]
    if missing:
        print("Frontend route access audit failed. Add explicit routeRules entries for:", file=sys.stderr)
        for path in missing:
            print(f"  - {path}", file=sys.stderr)
        return 1

    if "No explicit page rule is configured" not in source:
        print("Expected access-control fallback message was not found; audit script should be reviewed.", file=sys.stderr)
        return 1

    print(f"Frontend route access audit passed: {len(pages)} page routes checked, {len(APPROVED_EXCEPTIONS)} approved exceptions.")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
