from pathlib import Path

root = Path(__file__).resolve().parents[2]
checks = {
    'backend/Garmetix.Api/AppInfo/AppInfoEndpoints.cs': ['4.12.33', 'Stage 11D-118 Public Privacy and Terms Pages', 'GARMETIX-11D118-20260630-4233'],
    'frontend/garmetix-web/utils/appVersion.ts': ["APP_VERSION = '4.12.33'", 'Stage 11D-118 Public Privacy and Terms Pages', 'GARMETIX-11D118-20260630-4233'],
    'frontend/garmetix-web/package.json': ['"version": "4.12.33"'],
    'backend/Garmetix.Api/Garmetix.Api.csproj': ['<Version>4.12.33</Version>', '4.12.33-stage11d118-public-privacy-terms-pages'],
    'frontend/garmetix-web/pages/privacy/index.vue': ['Privacy Policy', 'Aadwika Fashion', 'WhatsApp'],
    'frontend/garmetix-web/pages/terms/index.vue': ['Terms of Usage', 'Aadwika Fashion', 'WhatsApp'],
    'frontend/garmetix-web/pages/privcy/index.vue': ["navigateTo('/privacy'"],
    'frontend/garmetix-web/middleware/auth.global.ts': ["'/privacy'", "'/privcy'", "'/terms'"],
    'docs/stages/stage-11/Stage11D118-Public-Privacy-Terms-Pages-v4.12.33.md': ['Stage 11D-118', 'v4.12.33']
}
missing = []
for rel, needles in checks.items():
    text = (root / rel).read_text(encoding='utf-8')
    for needle in needles:
        if needle not in text:
            missing.append(f'{rel}: missing {needle!r}')
if missing:
    print('Stage 11D-118 validation FAILED')
    raise SystemExit('\n'.join(missing))
print('Stage 11D-118 validation passed')
