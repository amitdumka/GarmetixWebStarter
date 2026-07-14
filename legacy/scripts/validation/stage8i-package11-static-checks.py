from pathlib import Path
root = Path(__file__).resolve().parents[2]

checks = {
    'frontend/garmetix-web/composables/useServerDocumentPrint.ts': [
        'function apiUrl(path: string)',
        "['localhost', '127.0.0.1', '0.0.0.0']",
        'window.location.origin',
        'return { fetchPdf, printPdf, downloadPdf, apiUrl }',
    ],
    'frontend/garmetix-web/pages/store-day/index.vue': [
        'const documentPrint = useServerDocumentPrint()',
        'await documentPrint.printPdf(`petty-cash-sheets/${id}/pdf`)',
        "feedback.failed('Petty cash print failed', error)",
    ],
    'docs/planning/TODO.md': [
        'Completed in v4.9.10',
        'Fixed Store Day Petty Cash print opening',
    ],
}

for rel, tokens in checks.items():
    text = (root / rel).read_text()
    for token in tokens:
        if token not in text:
            raise SystemExit(f'Missing {token} in {rel}')

store_day = (root / 'frontend/garmetix-web/pages/store-day/index.vue').read_text()
if 'window.open(url' in store_day or 'localhost:3000' in store_day:
    raise SystemExit('Store Day still has raw window.open URL print path.')

version = (root / 'frontend/garmetix-web/utils/appVersion.ts').read_text()
for token in ["APP_VERSION = '4.9.10'", 'Stage 8I Package 11 Petty Cash Print URL Hotfix', 'GARMETIX-8I-20260619-49100']:
    if token not in version:
        raise SystemExit(f'Missing frontend version token: {token}')

backend = (root / 'backend/Garmetix.Api/AppInfo/AppInfoEndpoints.cs').read_text()
for token in ['Version = "4.9.10"', 'Stage 8I Package 11 Petty Cash Print URL Hotfix', 'GARMETIX-8I-20260619-49100']:
    if token not in backend:
        raise SystemExit(f'Missing backend version token: {token}')

for rel in [
    'docs/stages/stage-8/Stage8I-Package11-PettyCashPrintUrl-v4.9.10-Notes.md',
    'docs/operations/Petty-Cash-Print-Url-Hotfix-v4.9.10.md',
]:
    if not (root / rel).exists():
        raise SystemExit(f'Missing doc: {rel}')

print('Stage 8I Package 11 static validation passed.')
