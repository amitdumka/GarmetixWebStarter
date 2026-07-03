#!/usr/bin/env python3
from pathlib import Path
import sys

checks = {
    'backend/Garmetix.Api/AppInfo/AppInfoEndpoints.cs': ['4.12.32', 'Stage 11D-117 Meta WhatsApp Four Variable Template Hotfix', 'GARMETIX-11D117-20260630-4232', '4-variable Aadwika approved template'],
    'frontend/garmetix-web/utils/appVersion.ts': ["APP_VERSION = '4.12.32'", 'Stage 11D-117 Meta WhatsApp Four Variable Template Hotfix', 'GARMETIX-11D117-20260630-4232'],
    'frontend/garmetix-web/package.json': ['"version": "4.12.32"'],
    'backend/Garmetix.Api/Garmetix.Api.csproj': ['<Version>4.12.32</Version>', '4.12.32-stage11d117-meta-whatsapp-four-var-template-hotfix'],
    'backend/Garmetix.Api/Marketing/DigitalBillWhatsAppService.cs': ['Aadwika/Garmetix invoice template uses 4 Meta body variables', 'MetaText(customerName, "Customer")', 'MetaText(digitalInvoice?.InvoiceNumber, "TEST")', 'MetaText(digitalInvoice?.Amount.ToString("0.##"), "0")', 'MetaText(fullUrl, ResolveFallbackDigitalBillUrl())'],
    'frontend/garmetix-web/pages/marketing/whatsapp-settings.vue': ['Invoice Meta template now sends 4 variables', 'For Meta invoice template use 4 body variables only', 'garmetix_invoice_link'],
    'docker-compose.prod.yml': ['DigitalBills__PublicBaseUrl', 'WhatsApp__MetaWebhookVerifyToken', 'WhatsApp__MetaCloudApiBaseUrl'],
    'deploy/docker-compose.prod.yml': ['DigitalBills__PublicBaseUrl', 'WhatsApp__MetaWebhookVerifyToken', 'WhatsApp__MetaCloudApiBaseUrl'],
    'docs/stages/stage-11/Stage11D117-Meta-WhatsApp-Four-Variable-Template-Hotfix-v4.12.32.md': ['Stage 11D-117', 'v4.12.32', '4-variable fixed-store template']
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

service_text = Path('backend/Garmetix.Api/Marketing/DigitalBillWhatsAppService.cs').read_text(errors='ignore')
invoice_block = service_text.split('private async Task<WhatsAppSendResultDto> SendMetaCloudAsync', 1)[1].split('private async Task<WhatsAppSendResultDto> SendMetaCampaignTemplateAsync', 1)[0]
if invoice_block.count('new { type = "text", text = MetaText(') != 4:
    print('Invoice Meta template send block must contain exactly 4 body text parameters')
    failed = True

if failed:
    print('Stage 11D-117 validation FAILED')
    sys.exit(1)
print('Stage 11D-117 validation passed')
