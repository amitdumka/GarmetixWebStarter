import json
from pathlib import Path

root = Path(__file__).resolve().parents[2]
failures = []


def read(path: str) -> str:
    return (root / path).read_text(encoding="utf-8")


def add(name: str, ok: bool) -> None:
    print(("PASS" if ok else "FAIL") + f" - {name}")
    if not ok:
        failures.append(name)


package = json.loads(read("frontend/garmetix-web/package.json"))
package_lock = json.loads(read("frontend/garmetix-web/package-lock.json"))
app_info = read("backend/Garmetix.Api/AppInfo/AppInfoEndpoints.cs")
app_version = read("frontend/garmetix-web/utils/appVersion.ts")
api_project = read("backend/Garmetix.Api/Garmetix.Api.csproj")
kiosk_project = read("apps/Garmetix.AttendanceKiosk/Garmetix.AttendanceKiosk.csproj")
bridge_project = read("apps/Garmetix.FingerprintBridge/Garmetix.FingerprintBridge.csproj")
mock_project = read("apps/Garmetix.MantraMockService/Garmetix.MantraMockService.csproj")
readme = read("README.md")
roadmap = read("docs/planning/CURRENT-ROADMAP.md")

root_lock = package_lock["packages"][""]

add(
    "release identity v4.11.19",
    all(token in app_info for token in ['Version = "4.11.19"', "Stage 11D-4 Factory Reset Admin Identity Fix", "GARMETIX-11D4-20260624-4119"])
    and "APP_VERSION = '4.11.19'" in app_version
    and "Stage 11D-4 Factory Reset Admin Identity Fix" in app_version
    and "GARMETIX-11D4-20260624-4119" in app_version
    and "<Version>4.11.19</Version>" in api_project
    and "<ApplicationDisplayVersion>4.11.19</ApplicationDisplayVersion>" in kiosk_project
    and "<ApplicationVersion>4119</ApplicationVersion>" in kiosk_project
    and "<Version>4.11.19</Version>" in bridge_project
    and "<Version>4.11.19</Version>" in mock_project,
)
add("package version bumped", package["version"] == "4.11.19" and package_lock["version"] == "4.11.19" and root_lock["version"] == "4.11.19")
add("Nuxt UI 4.9 pinned", package["dependencies"]["@nuxt/ui"] == "^4.9.0" and root_lock["dependencies"]["@nuxt/ui"] == "^4.9.0")
add("Nuxt 4 stack retained", package["dependencies"]["nuxt"] == "^4.4.8" and package["dependencies"]["vue"] == "^3.5.38")
add("Tailwind and Lucide retained", package["dependencies"]["tailwindcss"] == "^4.3.1" and package["dependencies"]["@iconify-json/lucide"] == "^1.2.114")
add("lockfile resolves Nuxt UI 4.9.0", package_lock["packages"]["node_modules/@nuxt/ui"]["version"] == "4.9.0")
add("docs record Nuxt UI 4.9 update", "Nuxt UI to `4.9.0`" in readme and "Current version: 4.11.19" in roadmap)

if failures:
    raise SystemExit("Stage 11D-2 Nuxt UI 4.9 validation failed: " + ", ".join(failures))

print("Stage 11D-2 Nuxt UI 4.9 validation passed.")
