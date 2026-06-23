#!/usr/bin/env python3
from __future__ import annotations

import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
errors: list[str] = []

def read(rel: str) -> str:
    return (ROOT / rel).read_text(encoding='utf-8')

def exists(rel: str) -> bool:
    return (ROOT / rel).exists()

def add(name: str, ok: bool):
    if not ok:
        errors.append(name)

endpoint = read('backend/Garmetix.Api/Production/EmailDeliveryDiagnosticsEndpoints.cs')
sender = read('backend/Garmetix.Api/Auth/SmtpEmailSender.cs')
options = read('backend/Garmetix.Api/Auth/EmailOptions.cs')
page = read('frontend/garmetix-web/pages/email-delivery/index.vue') if exists('frontend/garmetix-web/pages/email-delivery/index.vue') else ''
access = read('frontend/garmetix-web/composables/useAccessControl.ts')
shell = read('frontend/garmetix-web/components/AppShell.vue')
catalog = read('backend/Garmetix.Api/Testing/TestAutomationCatalog.cs')
runtime = read('backend/Garmetix.Api/Testing/TestAutomationEndpoints.cs')
linux_smoke = read('scripts/linux/smoke-test.sh')
windows_smoke = read('scripts/windows/smoke-test.ps1')
docker_compose = read('docker-compose.prod.yml')
create_env = read('deploy/create-production-env.sh')
appsettings = read('backend/Garmetix.Api/appsettings.json')

add('email delivery page exists', bool(page))
add('email delivery route protected', "path: '/email-delivery', label: 'Email Delivery'" in access and "roles: ['admin', 'owner']" in access)
add('email delivery shell menu', "to: '/email-delivery'" in shell and "label: 'Email Delivery'" in shell)
add('email page diagnostics calls', "email-diagnostics/status" in page and "email-diagnostics/send-test" in page and "Brevo SMTP" in page and "PASSWORD_RESET_FRONTEND_BASE_URL" in page)
add('email page does not ask to store secret in browser', 'Secrets remain in .env.production' in page or 'Secrets are not exposed' in page)
add('status provider contract', 'ProviderName' in endpoint and 'DetectProvider' in endpoint and 'RecommendedEnvironmentKeys' in endpoint)
add('status required env keys', all(key in endpoint for key in ['EMAIL_ENABLED', 'EMAIL_HOST', 'EMAIL_PASSWORD', 'EMAIL_TIMEOUT_SECONDS', 'PASSWORD_RESET_FRONTEND_BASE_URL']))
add('status masks sensitive values', 'MaskAddress(settings.UserName)' in endpoint and 'MaskAddress(settings.FromEmail)' in endpoint and 'Password,' not in endpoint and 'settings.Password,' not in endpoint)
add('smtp sender hardened', 'Email:Password is required when Email:UserName is configured.' in sender and 'Email:Port must be between 1 and 65535.' in sender and 'Timeout = Math.Max(settings.TimeoutSeconds, 5) * 1000' in sender)
add('smtp options timeout', 'public int TimeoutSeconds' in options)
add('docker timeout env', 'Email__TimeoutSeconds' in docker_compose and 'EMAIL_TIMEOUT_SECONDS=30' in create_env and '"TimeoutSeconds": 30' in appsettings)
add('smtp acceptance script', exists('scripts/linux/smtp-acceptance-drill.sh') and 'email-diagnostics/status' in read('scripts/linux/smtp-acceptance-drill.sh') and 'GARMETIX_SMTP_SEND_TEST' in read('scripts/linux/smtp-acceptance-drill.sh'))
add('manifest smtp acceptance', 'SMTP_DELIVERY_ACCEPTANCE' in catalog and 'smtp-acceptance-drill.sh' in catalog)
add('runtime smtp acceptance required', '"SMTP_DELIVERY_ACCEPTANCE"' in runtime and '"PRINT_PDF_ACCEPTANCE"' in runtime)
add('smoke smtp expected version', 'GARMETIX_EXPECTED_VERSION:-4.9.24' in linux_smoke and 'SMTP_DELIVERY_ACCEPTANCE' in linux_smoke and 'ExpectedVersion = "4.9.24"' in windows_smoke and 'SMTP_DELIVERY_ACCEPTANCE' in windows_smoke)
add('docs package20', exists('docs/stages/stage-8/Stage8I-Package20-SmtpEmailDeliveryAcceptance-v4.9.19-Notes.md') and exists('docs/operations/SMTP-Email-Delivery-Acceptance-v4.9.19.md'))
add('roadmap package20', 'Stage 8I Package 20 SMTP Email Integration and Delivery Acceptance / v4.9.19' in read('docs/planning/CURRENT-ROADMAP.md'))
add('issues package20', 'SMTP delivery acceptance was split' in read('docs/planning/ISSUES.md'))

if errors:
    print('SMTP delivery acceptance check failed:', file=sys.stderr)
    for error in errors:
        print(f' - {error}', file=sys.stderr)
    sys.exit(1)

print('SMTP delivery acceptance check passed.')
