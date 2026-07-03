#!/usr/bin/env python3
from pathlib import Path
import sys

checks = {
    'backend/Garmetix.Api/AppInfo/AppInfoEndpoints.cs': ['4.12.31', 'Stage 11D-116 Meta WhatsApp Config Hotfix', 'GARMETIX-11D116-20260630-4231', 'Meta WhatsApp Cloud API config'],
    'frontend/garmetix-web/utils/appVersion.ts': ["APP_VERSION = '4.12.31'", 'Stage 11D-116 Meta WhatsApp Config Hotfix', 'GARMETIX-11D116-20260630-4231'],
    'frontend/garmetix-web/package.json': ['"version": "4.12.31"'],
    'backend/Garmetix.Api/Garmetix.Api.csproj': ['<Version>4.12.31</Version>', '4.12.31-stage11d116-meta-whatsapp-config-hotfix'],
    'backend/Garmetix.Api/Marketing/DigitalBillWhatsAppService.cs': ['ResolveMetaCloudApiBaseUrl', 'MetaText(', 'ResolveFallbackDigitalBillUrl', 'graph.facebook.com', 'Ignoring invalid Meta Cloud API base URL'],
    'docker-compose.prod.yml': ['DigitalBills__PublicBaseUrl', 'WhatsApp__MetaWebhookVerifyToken', 'WhatsApp__MetaCloudApiBaseUrl'],
    'deploy/docker-compose.prod.yml': ['DigitalBills__PublicBaseUrl', 'WhatsApp__MetaWebhookVerifyToken', 'WhatsApp__MetaCloudApiBaseUrl'],
    '.env.production.example': ['DigitalBills__PublicBaseUrl=https://garmetix.aadwikafashion.in', 'WhatsApp__MetaWebhookVerifyToken=REPLACE_WITH_RANDOM_WEBHOOK_VERIFY_TOKEN', 'WhatsApp__MetaCloudApiBaseUrl=https://graph.facebook.com/v20.0'],
    'deploy/env.production.example': ['DigitalBills__PublicBaseUrl=https://garmetix.aadwikafashion.in', 'WhatsApp__MetaCloudApiBaseUrl=https://graph.facebook.com/v20.0'],
    'scripts/runtime/configure-meta-whatsapp-env.sh': ['openssl rand -hex 32', 'WhatsApp__MetaWebhookVerifyToken', 'api/public/digital-bill-whatsapp/webhook/meta'],
    'docs/stages/stage-11/Stage11D116-Meta-WhatsApp-Config-Hotfix-v4.12.31.md': ['Stage 11D-116', 'v4.12.31', 'Meta Cloud API', 'Docker compose env']
}

failed = False
for file, tokens in checks.items():
    path = Path(file)
    if not path.exists():
        print(f'MISSING FILE: {file}')
        failed = True
        continue
    text = path.read_text(errors='ignore')
    for token in tokens:
        if token not in text:
            print(f'MISSING TOKEN in {file}: {token}')
            failed = True

root_env = Path('.env.production.example').read_text(errors='ignore')
if 'DigitalBills__PublicBaseUrl=\n' in root_env:
    print('Root env example still contains blank DigitalBills__PublicBaseUrl override')
    failed = True

if failed:
    print('Stage 11D-116 validation FAILED')
    sys.exit(1)
print('Stage 11D-116 validation passed')
