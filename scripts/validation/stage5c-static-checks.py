#!/usr/bin/env python3
from pathlib import Path
import sys

ROOT = Path(__file__).resolve().parents[2]
REQUIRED = [
    "backend/Garmetix.Api/Release/ReleaseStabilizationDtos.cs",
    "backend/Garmetix.Api/Release/ReleaseStabilizationEndpoints.cs",
    "frontend/garmetix-web/pages/release-stabilization/index.vue",
    "frontend/garmetix-web/public/docs/operator-user-manual.md",
    "frontend/garmetix-web/public/docs/go-live-smoke-test-checklist.md",
    "scripts/linux/smoke-test.sh",
    "scripts/windows/smoke-test.ps1",
    "Operator-User-Manual.md",
    "GoLive-Smoke-Test-Checklist.md",
]

missing = [path for path in REQUIRED if not (ROOT / path).exists()]
if missing:
    print("Missing Stage 5C files:")
    for path in missing:
        print(f" - {path}")
    sys.exit(1)

program = (ROOT / "backend/Garmetix.Api/Program.cs").read_text()
if "using Garmetix.Api.Release;" not in program or "app.MapReleaseStabilizationEndpoints();" not in program:
    print("Program.cs is missing Release Stabilization registration.")
    sys.exit(1)

shell = (ROOT / "frontend/garmetix-web/components/AppShell.vue").read_text()
if "/release-stabilization" not in shell:
    print("AppShell navigation is missing Release Stabilization link.")
    sys.exit(1)

endpoints = (ROOT / "backend/Garmetix.Api/Release/ReleaseStabilizationEndpoints.cs").read_text()
for token in ["/smoke-checks", "/demo-data/seed", "DemoOpening", "PRODUCT_HSN", "NEGATIVE_STOCK"]:
    if token not in endpoints:
        print(f"Release endpoint missing token: {token}")
        sys.exit(1)

print("Stage 5C static checks passed.")
