from pathlib import Path
root = Path(__file__).resolve().parents[2]
required = [
    root / 'backend/Garmetix.Api/Production/EmailDeliveryDiagnosticsEndpoints.cs',
    root / 'docs/stages/stage-8/Stage8G-Package3-SmtpEmailDeliveryValidation-v4.6.2-Notes.md',
    root / 'docs/operations/SMTP-Email-Delivery-Validation-v4.6.2.md',
]
missing = [str(p.relative_to(root)) for p in required if not p.exists()]
if missing:
    raise SystemExit(f'Missing Stage 8G Package 3 files: {missing}')
endpoint = (root / 'backend/Garmetix.Api/Production/EmailDeliveryDiagnosticsEndpoints.cs').read_text()
for token in ['MapEmailDeliveryDiagnosticsEndpoints', '/api/email-diagnostics', '/send-test', 'MaskAddress', 'RequireAuthorization']:
    if token not in endpoint:
        raise SystemExit(f'Email diagnostics endpoint token missing: {token}')
program = (root / 'backend/Garmetix.Api/Program.cs').read_text()
if 'app.MapEmailDeliveryDiagnosticsEndpoints();' not in program:
    raise SystemExit('Program.cs does not map email diagnostics endpoints')
page = (root / 'frontend/garmetix-web/pages/production-readiness/index.vue').read_text()
for token in ['email-diagnostics/status', 'email-diagnostics/send-test', 'SMTP email delivery test', 'Send test email']:
    if token not in page:
        raise SystemExit(f'Production readiness SMTP UI token missing: {token}')
version = (root / 'frontend/garmetix-web/utils/appVersion.ts').read_text()
if "APP_VERSION = '4." not in version:
    raise SystemExit('Frontend app version not compatible with current Stage 8G release')
backend = (root / 'backend/Garmetix.Api/AppInfo/AppInfoEndpoints.cs').read_text()
if 'Version = "4.' not in backend:
    raise SystemExit('Backend app info token missing for current Stage 8G release')
roadmap = (root / 'docs/planning/CURRENT-ROADMAP.md').read_text()
if 'Stage 8G Package 3 SMTP Email Delivery Validation / v4.6.2' not in roadmap:
    raise SystemExit('Current roadmap missing Stage 8G Package 3 entry')
print('Stage 8G Package 3 static validation passed.')
