#!/usr/bin/env python3
from pathlib import Path
import sys

ROOT = Path(__file__).resolve().parents[2]
checks = []

def must_contain(path, token):
    text = (ROOT / path).read_text(errors='ignore')
    if token not in text:
        checks.append(f"MISSING TOKEN in {path}: {token}")

def must_not_contain(path, token):
    text = (ROOT / path).read_text(errors='ignore')
    if token in text:
        checks.append(f"UNEXPECTED TOKEN in {path}: {token}")

must_contain('backend/Garmetix.Api/AppInfo/AppInfoEndpoints.cs', '4.12.07')
must_contain('backend/Garmetix.Api/AppInfo/AppInfoEndpoints.cs', 'Stage 11D-92 Vyapar Sale Import Build Layout Fix')
must_contain('backend/Garmetix.Api/AppInfo/AppInfoEndpoints.cs', 'GARMETIX-11D92-20260629-4207')
must_contain('frontend/garmetix-web/utils/appVersion.ts', "APP_VERSION = '4.12.07'")
must_contain('frontend/garmetix-web/package.json', '"version": "4.12.07"')
must_contain('backend/Garmetix.Api/Garmetix.Api.csproj', '<Version>4.12.07</Version>')

billing = (ROOT / 'backend/Garmetix.Api/Billing/BillingEndpoints.cs').read_text(errors='ignore')
start = billing.find('private static RecentInvoiceDto ToRecentInvoiceDto')
end = billing.find('private static string BuildDigitalBillPublicPath', start)
section = billing[start:end]
if 'invoice.Remarks' in section:
    checks.append('ToRecentInvoiceDto still references invoice.Remarks outside invoice scope')
if 'remarks);' not in section:
    checks.append('ToRecentInvoiceDto does not pass remarks to RecentInvoiceDto')

must_contain('frontend/garmetix-web/pages/billing/vyapar-import.vue', '<AppShell title="Vyapar Sale Import" @refresh="loadOptions">')
must_contain('frontend/garmetix-web/pages/billing/vyapar-import.vue', '</AppShell>')
must_contain('frontend/garmetix-web/pages/billing/vyapar-imported.vue', '<AppShell title="Imported Vyapar Sales" @refresh="refresh">')
must_contain('frontend/garmetix-web/pages/billing/vyapar-imported.vue', '</AppShell>')
must_contain('docs/stages/stage-11/Stage11D92-Vyapar-Sale-Import-Build-Layout-Fix-v4.12.07.md', 'Stage 11D-93')

if checks:
    print('\n'.join(checks))
    print('Stage 11D-92 validation FAILED')
    sys.exit(1)
print('Stage 11D-92 validation passed')
