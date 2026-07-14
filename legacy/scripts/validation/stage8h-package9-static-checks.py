from pathlib import Path
root = Path(__file__).resolve().parents[2]
css = (root / 'frontend/garmetix-web/assets/css/main.css').read_text()
required = [
    'Stage 8H Package 9: single-scroll page guardrails',
    '.dashboard-template-content',
    'overflow-y: visible',
    '.message-log-list',
    '.dashboard-access-matrix',
    'overflow-x: auto',
]
for token in required:
    if token not in css:
        raise SystemExit(f'Missing CSS guardrail token: {token}')

version = (root / 'frontend/garmetix-web/utils/appVersion.ts').read_text()
for token in ["APP_VERSION = '4.7.8'", 'Stage 8H Package 9 Single Scroll Layout Hotfix', 'GARMETIX-8H-20260617-4780']:
    if token not in version:
        raise SystemExit(f'Missing frontend version token: {token}')

backend = (root / 'backend/Garmetix.Api/AppInfo/AppInfoEndpoints.cs').read_text()
for token in ['Version = "4.7.8"', 'Stage 8H Package 9 Single Scroll Layout Hotfix', 'GARMETIX-8H-20260617-4780']:
    if token not in backend:
        raise SystemExit(f'Missing backend version token: {token}')

for rel in [
    'docs/stages/stage-8/Stage8H-Package9-SingleScrollLayout-v4.7.8-Notes.md',
    'docs/operations/Single-Scroll-Layout-v4.7.8.md',
]:
    if not (root / rel).exists():
        raise SystemExit(f'Missing doc: {rel}')
print('Stage 8H Package 9 static validation passed.')
