from pathlib import Path
root = Path(__file__).resolve().parents[2]

checks = {
    'backend/Garmetix.Api/Production/PrintAcceptanceEndpoints.cs': [
        'MapPrintAcceptanceEndpoints',
        'PrintAcceptanceStatusDto',
        'Voucher PDF',
        'Cash Voucher PDF',
        'Petty Cash Sheet PDF',
        'Purchase Inward PDF',
        'Tailoring Order / Invoice Print',
        'GST Return Export / CA Review',
    ],
    'backend/Garmetix.Api/Production/PermissionAcceptanceEndpoints.cs': [
        'MapPermissionAcceptanceEndpoints',
        'PermissionAcceptanceStatusDto',
        'Admin/Owner',
        'ScopedUsers',
        'RoleCoverage',
    ],
    'backend/Garmetix.Api/Program.cs': [
        'app.MapPrintAcceptanceEndpoints();',
        'app.MapPermissionAcceptanceEndpoints();',
    ],
    'frontend/garmetix-web/pages/print-final-acceptance/index.vue': [
        'Print Final Acceptance',
        'print-acceptance/status',
        'PRINT_ACCEPTANCE_KEY',
        'Open sample',
    ],
    'frontend/garmetix-web/pages/permission-final-acceptance/index.vue': [
        'Permission Final Acceptance',
        'permission-acceptance/status',
        'PERMISSION_ACCEPTANCE_KEY',
        'Role Coverage',
    ],
    'frontend/garmetix-web/components/AppShell.vue': [
        '/print-final-acceptance',
        '/permission-final-acceptance',
    ],
}
for rel, tokens in checks.items():
    text = (root / rel).read_text()
    for token in tokens:
        if token not in text:
            raise SystemExit(f'Missing {token} in {rel}')

version = (root / 'frontend/garmetix-web/utils/appVersion.ts').read_text()
for token in ["APP_VERSION = '4.9.1'", 'Stage 8I Package 2 Print & Permission Final Acceptance', 'GARMETIX-8I-20260618-4910']:
    if token not in version:
        raise SystemExit(f'Missing frontend version token: {token}')

backend = (root / 'backend/Garmetix.Api/AppInfo/AppInfoEndpoints.cs').read_text()
for token in ['Version = "4.9.1"', 'Stage 8I Package 2 Print & Permission Final Acceptance', 'GARMETIX-8I-20260618-4910']:
    if token not in backend:
        raise SystemExit(f'Missing backend version token: {token}')

for rel in [
    'docs/stages/stage-8/Stage8I-Package2-PrintPermissionAcceptance-v4.9.1-Notes.md',
    'docs/operations/Print-Permission-Final-Acceptance-v4.9.1.md',
]:
    if not (root / rel).exists():
        raise SystemExit(f'Missing doc: {rel}')

print('Stage 8I Package 2 static validation passed.')
