from pathlib import Path
root = Path(__file__).resolve().parents[2]

checks = {
    'backend/Garmetix.Api/Seeds/CompanyMergeEndpoints.cs': [
        'MapCompanyMergeEndpoints',
        'PreviewAfSmartAsync',
        'ApplyAfSmartAsync',
        'Aadwika Fashion',
        'Aadwika Fashion MBO',
        'Smart Menswear',
        'Shalini is excluded',
        'UpdateByScopeColumnAsync',
    ],
    'backend/Garmetix.Api/Program.cs': [
        'app.MapCompanyMergeEndpoints();',
    ],
    'frontend/garmetix-web/pages/af-ss/index.vue': [
        'Aadwika + Smart Menswear Merge',
        'company-merge/af-smart/preview',
        'company-merge/af-smart/apply',
        'Preview merge',
        'Apply merge',
    ],
    'docs/planning/TODO.md': [
        'Completed in v4.9.4',
        'Aadwika Fashion + Smart Menswear merge utility',
    ],
}
for rel, tokens in checks.items():
    text = (root / rel).read_text()
    for token in tokens:
        if token not in text:
            raise SystemExit(f'Missing {token} in {rel}')

version = (root / 'frontend/garmetix-web/utils/appVersion.ts').read_text()
for token in ["APP_VERSION = '4.9.4'", 'Stage 8I Package 5 Aadwika Smart Merge', 'GARMETIX-8I-20260618-4940']:
    if token not in version:
        raise SystemExit(f'Missing frontend version token: {token}')

backend = (root / 'backend/Garmetix.Api/AppInfo/AppInfoEndpoints.cs').read_text()
for token in ['Version = "4.9.4"', 'Stage 8I Package 5 Aadwika Smart Merge', 'GARMETIX-8I-20260618-4940']:
    if token not in backend:
        raise SystemExit(f'Missing backend version token: {token}')

for rel in [
    'docs/stages/stage-8/Stage8I-Package5-AfSmartMerge-v4.9.4-Notes.md',
    'docs/operations/Aadwika-Smart-Merge-v4.9.4.md',
]:
    if not (root / rel).exists():
        raise SystemExit(f'Missing doc: {rel}')

print('Stage 8I Package 5 static validation passed.')
