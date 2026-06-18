from pathlib import Path
root = Path(__file__).resolve().parents[2]
purchase = (root / 'frontend/garmetix-web/pages/purchase/index.vue').read_text()
for token in ['quickPrintPurchaseInvoice', 'quickDownloadPurchaseInvoice', 'i-lucide-printer', 'i-lucide-download']:
    if token not in purchase:
        raise SystemExit(f'Missing purchase register follow-up token: {token}')
version = (root / 'frontend/garmetix-web/utils/appVersion.ts').read_text()
for token in ["APP_VERSION = '4.7.6'", 'Stage 8H Package 7 Purchase Register Follow-up', 'GARMETIX-8H-20260617-4760']:
    if token not in version:
        raise SystemExit(f'Missing frontend version token: {token}')
backend = (root / 'backend/Garmetix.Api/AppInfo/AppInfoEndpoints.cs').read_text()
for token in ['Version = "4.7.6"', 'Stage 8H Package 7 Purchase Register Follow-up', 'GARMETIX-8H-20260617-4760']:
    if token not in backend:
        raise SystemExit(f'Missing backend version token: {token}')
for rel in [
    'docs/stages/stage-8/Stage8H-Package7-PurchaseRegisterFollowup-v4.7.6-Notes.md',
    'docs/operations/Purchase-Register-Followup-v4.7.6.md'
]:
    if not (root / rel).exists():
        raise SystemExit(f'Missing doc: {rel}')
print('Stage 8H Package 7 static validation passed.')
