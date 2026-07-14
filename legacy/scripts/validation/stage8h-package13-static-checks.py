from pathlib import Path
root = Path(__file__).resolve().parents[2]

composable = root / 'frontend/garmetix-web/composables/useGstReviewContact.ts'
if not composable.exists():
    raise SystemExit('Missing GST review contact composable')
text = composable.read_text()
for token in ['garmetix:gst-review-contact:v1', 'garmetix:gst-review-share-log:v1', 'addLog', 'applyTo', 'save']:
    if token not in text:
        raise SystemExit(f'Missing composable token: {token}')

for rel in ['frontend/garmetix-web/pages/gst-returns/index.vue', 'frontend/garmetix-web/pages/gst-reports/index.vue']:
    page = (root / rel).read_text()
    for token in ['useGstReviewContact', 'Save as default CA contact', 'recentGstShareLogs', 'gstReviewContact.addLog']:
        if token not in page:
            raise SystemExit(f'Missing {token} in {rel}')

version = (root / 'frontend/garmetix-web/utils/appVersion.ts').read_text()
for token in ["APP_VERSION = '4.8.2'", 'Stage 8H Package 13 GST CA Workflow Polish', 'GARMETIX-8H-20260618-4820']:
    if token not in version:
        raise SystemExit(f'Missing frontend version token: {token}')

backend = (root / 'backend/Garmetix.Api/AppInfo/AppInfoEndpoints.cs').read_text()
for token in ['Version = "4.8.2"', 'Stage 8H Package 13 GST CA Workflow Polish', 'GARMETIX-8H-20260618-4820']:
    if token not in backend:
        raise SystemExit(f'Missing backend version token: {token}')

for rel in [
    'docs/stages/stage-8/Stage8H-Package13-GstCaWorkflow-v4.8.2-Notes.md',
    'docs/operations/GST-CA-Workflow-v4.8.2.md',
]:
    if not (root / rel).exists():
        raise SystemExit(f'Missing doc: {rel}')

print('Stage 8H Package 13 static validation passed.')
