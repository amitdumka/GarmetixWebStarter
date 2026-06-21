from pathlib import Path

root = Path(__file__).resolve().parents[2]
required = [
    'backend/Garmetix.Api/Production/Stage10CompleteEndpoints.cs',
    'frontend/garmetix-web/pages/barcode-final-acceptance/index.vue',
    'frontend/garmetix-web/pages/gst-production/index.vue',
    'frontend/garmetix-web/pages/google-drive-backup/index.vue',
    'frontend/garmetix-web/pages/audit-trail-final/index.vue',
    'frontend/garmetix-web/pages/stage10-final-acceptance/index.vue',
    'scripts/linux/stage10-complete-final-drill.sh',
]
missing = [path for path in required if not (root / path).exists()]
if missing:
    raise SystemExit('Missing Stage 10 complete files: ' + ', '.join(missing))

content = (root / 'backend/Garmetix.Api/Production/Stage10CompleteEndpoints.cs').read_text()
for token in [
    'MapBarcodeAcceptanceEndpoints',
    'print-center',
    'final-acceptance',
    'MapGstProductionAcceptanceEndpoints',
    'MapGstProductionAcceptanceEndpoints',
    'final-acceptance',
    'MapGoogleDriveBackupAcceptanceEndpoints',
    'MapGoogleDriveBackupAcceptanceEndpoints',
    'final-acceptance',
    'MapAuditTrailFinalAcceptanceEndpoints',
    'MapAuditTrailFinalAcceptanceEndpoints',
    'final-acceptance',
    'MapStage10CompleteEndpoints',
    'MapStage10CompleteEndpoints',
    'final-acceptance',
]:
    if token not in content:
        raise SystemExit(f'Missing Stage 10 backend token: {token}')

program = (root / 'backend/Garmetix.Api/Program.cs').read_text()
for token in [
    'app.MapBarcodeAcceptanceEndpoints();',
    'app.MapGstProductionAcceptanceEndpoints();',
    'app.MapGoogleDriveBackupAcceptanceEndpoints();',
    'app.MapAuditTrailFinalAcceptanceEndpoints();',
    'app.MapStage10CompleteEndpoints();',
]:
    if token not in program:
        raise SystemExit(f'Missing Program mapping: {token}')

routes = (root / 'frontend/garmetix-web/composables/useAccessControl.ts').read_text()
for route in ['/barcode-final-acceptance', '/gst-production', '/google-drive-backup', '/audit-trail-final', '/stage10-final-acceptance']:
    if route not in routes:
        raise SystemExit(f'Missing route access rule: {route}')

shell = (root / 'frontend/garmetix-web/components/AppShell.vue').read_text()
for route in ['/barcode-final-acceptance', '/gst-production', '/google-drive-backup', '/audit-trail-final', '/stage10-final-acceptance']:
    if route not in shell:
        raise SystemExit(f'Missing shell nav item: {route}')

app_version = (root / 'frontend/garmetix-web/utils/appVersion.ts').read_text()
allowed_versions = [f"APP_VERSION = '4.10.{patch}'" for patch in range(13, 31)]
if not any(version in app_version for version in allowed_versions):
    raise SystemExit('App version metadata not updated for Stage 10 release family.')

catalog = (root / 'backend/Garmetix.Api/Testing/TestAutomationCatalog.cs').read_text()
for code in ['STAGE10C_BARCODE_FINAL_ACCEPTANCE', 'STAGE10D_GST_EINVOICE_ACCEPTANCE', 'STAGE10E_GOOGLE_DRIVE_BACKUP_ACCEPTANCE', 'STAGE10F_AUDIT_TRAIL_ACCEPTANCE', 'STAGE10_FINAL_ACCEPTANCE']:
    if code not in catalog:
        raise SystemExit(f'Missing test automation code: {code}')

print('Stage 10 complete static validation passed.')
