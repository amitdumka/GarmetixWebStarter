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

version_line = next((ln.strip() for ln in text.splitlines() if ln.strip().startswith('export const APP_VERSION =')), '')
stage_line = next((ln.strip() for ln in text.splitlines() if ln.strip().startswith('export const APP_STAGE =')), '')
add('APP_VERSION remains present', bool(re.match(r"export const APP_VERSION\s*=\s*'\d+\.\d+\.\d+'", version_line)))
add('Stage label preserved', 'Stage ' in stage_line and len(stage_line) > len('export const APP_STAGE = '))

failed = [name for name, ok in checks if not ok]
for name, ok in checks:
    print(('PASS' if ok else 'FAIL') + f': {name}')
if failed:
    raise SystemExit('App version string safety validation failed: ' + ', '.join(failed))
print('App version string safety validation passed.')
