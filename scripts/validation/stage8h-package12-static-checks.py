from pathlib import Path
root = Path(__file__).resolve().parents[2]
endpoint = (root / 'backend/Garmetix.Api/GstReturns/GstReturnEndpoints.cs').read_text()
for token in [
    'SendGstReportsReviewAsync',
    'GstReportReviewSendRequest',
    'BuildGstReportsSummaryText',
    'GstReportReviewSendResponse',
    'Garmetix GST book reports review'
]:
    if token not in endpoint:
        raise SystemExit(f'Missing GST compile hotfix token: {token}')

for rel in [
    'frontend/garmetix-web/server/api/_nuxt_icon/lucide.json.get.ts',
    'frontend/garmetix-web/server/routes/_nuxt_icon/lucide.json.get.ts'
]:
    text = (root / rel).read_text()
    for token in ['@iconify-json/lucide/icons.json', 'return loadLucideCollection()', 'cache-control']:
        if token not in text:
            raise SystemExit(f'Missing icon endpoint token {token} in {rel}')

version = (root / 'frontend/garmetix-web/utils/appVersion.ts').read_text()
for token in ["APP_VERSION = '4.8.1'", 'Stage 8H Package 12 Compile and Lucide Icon Reliability Hotfix', 'GARMETIX-8H-20260618-4810']:
    if token not in version:
        raise SystemExit(f'Missing frontend version token: {token}')

backend = (root / 'backend/Garmetix.Api/AppInfo/AppInfoEndpoints.cs').read_text()
for token in ['Version = "4.8.1"', 'Stage 8H Package 12 Compile and Lucide Icon Reliability Hotfix', 'GARMETIX-8H-20260618-4810']:
    if token not in backend:
        raise SystemExit(f'Missing backend version token: {token}')

print('Stage 8H Package 12 static validation passed.')
