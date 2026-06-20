from pathlib import Path
import re

root = Path(__file__).resolve().parents[2]
path = root / 'frontend/garmetix-web/utils/appVersion.ts'
text = path.read_text(encoding='utf-8')

checks = []

def add(name: str, ok: bool):
    checks.append((name, ok))

for const_name in ['APP_STAGE', 'APP_RELEASE_NAME']:
    line = next((ln.strip() for ln in text.splitlines() if ln.strip().startswith(f'export const {const_name} =')), '')
    uses_unsafe_single_quote = bool(re.match(rf"export const {const_name}\s*=\s*'[^']*'[^']*'", line)) or ("Today's" in line and f"{const_name} = '" in line)
    add(f'{const_name} avoids unsafe single-quoted apostrophe literal', bool(line) and not uses_unsafe_single_quote)

add('APP_VERSION remains present', "export const APP_VERSION = '4.10.12'" in text or "export const APP_VERSION = '4.10.13'" in text or "export const APP_VERSION = '4.10.14'" in text or "export const APP_VERSION = '4.10.15'" in text or "export const APP_VERSION = '4.10.16'" in text or "export const APP_VERSION = '4.10.17'" in text or "export const APP_VERSION = '4.10.18'" in text or "export const APP_VERSION = '4.10.19'" in text or "export const APP_VERSION = '4.10.20'" in text or "export const APP_VERSION = '4.10.21'" in text)
add('Stage label preserved', 'Stage 10B Excel Import Export Center' in text or 'Stage 10 Complete Final Acceptance' in text or 'Stage 10G Navigation Menu Coverage' in text or 'Stage 10H Runtime Bug Fix Pack' in text or 'Stage 10I Store Operations Cash Closing Repair' in text or 'Stage 10J Real Excel Import Export Engine' in text)

failed = [name for name, ok in checks if not ok]
for name, ok in checks:
    print(('PASS' if ok else 'FAIL') + f': {name}')
if failed:
    raise SystemExit('App version string safety validation failed: ' + ', '.join(failed))
print('App version string safety validation passed.')
