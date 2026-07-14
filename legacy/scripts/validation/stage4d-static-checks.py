#!/usr/bin/env python3
"""Stage 4D static validation checks.

This script does not replace `dotnet build` or `npm run build`. It verifies that
Stage 4D source additions are wired into the API, frontend navigation, and
validation dashboard before Docker build is attempted.
"""
from __future__ import annotations

import re
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]

CHECKS = [
    ("backend/Garmetix.Api/Validation/DataConsistencyDtos.cs", "record DataConsistencyIssueDto"),
    ("backend/Garmetix.Api/Validation/DataConsistencyEndpoints.cs", "MapDataConsistencyEndpoints"),
    ("backend/Garmetix.Api/Validation/DataConsistencyEndpoints.cs", "NEGATIVE_STOCK"),
    ("backend/Garmetix.Api/Validation/DataConsistencyEndpoints.cs", "DUPLICATE_DOCUMENT_SEQUENCE"),
    ("backend/Garmetix.Api/Validation/DataConsistencyEndpoints.cs", "JOURNAL_UNBALANCED"),
    ("backend/Garmetix.Api/Program.cs", "using Garmetix.Api.Validation;"),
    ("backend/Garmetix.Api/Program.cs", "app.MapDataConsistencyEndpoints();"),
    ("frontend/garmetix-web/components/AppShell.vue", "'/data-consistency'"),
    ("frontend/garmetix-web/pages/data-consistency/index.vue", "Data Consistency Verification"),
    ("frontend/garmetix-web/pages/data-consistency/index.vue", "data-consistency/issues"),
]


def balanced_braces(path: Path) -> bool:
    # Lightweight check only. This intentionally counts raw braces because
    # C# interpolated strings make regex string stripping unreliable.
    text = path.read_text(encoding="utf-8")
    balance = 0
    for char in text:
        if char == "{":
            balance += 1
        elif char == "}":
            balance -= 1
            if balance < 0:
                return False
    return balance == 0


def main() -> int:
    failures: list[str] = []
    for rel, needle in CHECKS:
        path = ROOT / rel
        if not path.exists():
            failures.append(f"Missing file: {rel}")
            continue
        text = path.read_text(encoding="utf-8")
        if needle not in text:
            failures.append(f"Missing marker in {rel}: {needle}")

    for rel in [
        "backend/Garmetix.Api/Validation/DataConsistencyDtos.cs",
        "backend/Garmetix.Api/Validation/DataConsistencyEndpoints.cs",
    ]:
        path = ROOT / rel
        if path.exists() and not balanced_braces(path):
            failures.append(f"Unbalanced braces: {rel}")

    if failures:
        print("Stage 4D static validation failed:")
        for failure in failures:
            print(f" - {failure}")
        return 1

    print("Stage 4D static validation passed.")
    return 0


if __name__ == "__main__":
    sys.exit(main())
