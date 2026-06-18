from pathlib import Path
root = Path(__file__).resolve().parents[2]
page = root / 'frontend/garmetix-web/pages/post-go-live-acceptance/index.vue'
text = page.read_text()
if "~/composables/useApi" in text or "useApi()" in text:
    raise SystemExit('post-go-live acceptance page still imports missing useApi composable')
if 'useGarmetixApi()' not in text:
    raise SystemExit('post-go-live acceptance page does not use existing useGarmetixApi composable')
missing = []
for path in (root / 'frontend/garmetix-web').rglob('*'):
    if path.suffix in {'.vue', '.ts', '.js'}:
        data = path.read_text(errors='ignore')
        if "~/composables/useApi" in data or "from '/composables/useApi'" in data:
            missing.append(str(path.relative_to(root)))
if missing:
    raise SystemExit('Missing useApi import remains in: ' + ', '.join(missing))
print('Stage 8H Package 3 compile hotfix static validation passed.')
