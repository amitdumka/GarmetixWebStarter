#!/usr/bin/env python3
from pathlib import Path

root = Path(__file__).resolve().parents[2]
checks = []

def add(name, ok):
    checks.append((name, bool(ok)))

app_info = (root / 'backend/Garmetix.Api/AppInfo/AppInfoEndpoints.cs').read_text(errors='ignore')
csproj = (root / 'backend/Garmetix.Api/Garmetix.Api.csproj').read_text(errors='ignore')
dtos = (root / 'backend/Garmetix.Api/PurchaseImport/PurchaseInvoiceImportDtos.cs').read_text(errors='ignore')
endpoints = (root / 'backend/Garmetix.Api/PurchaseImport/PurchaseInvoiceImportEndpoints.cs').read_text(errors='ignore')
service = (root / 'backend/Garmetix.Api/PurchaseImport/PurchaseInvoiceImportService.cs').read_text(errors='ignore')
page = (root / 'frontend/garmetix-web/pages/purchase/import-acceptance.vue').read_text(errors='ignore')
version = (root / 'frontend/garmetix-web/utils/appVersion.ts').read_text(errors='ignore')
backup = (root / 'scripts/linux/create-database-backup-now.sh').read_text(errors='ignore')
doc = (root / 'docs/modules/purchase-inventory/purchase-import-final-closure-v4.12.39.md').read_text(errors='ignore')

add('backend version v4.12.39', 'Version = "4.12.39"' in app_info and 'GARMETIX-11D124-20260701-4239' in app_info)
add('csproj version v4.12.39', '<Version>4.12.39</Version>' in csproj and '<AssemblyVersion>4.12.39.0</AssemblyVersion>' in csproj)
add('frontend version v4.12.39', "APP_VERSION = '4.12.39'" in version and 'GARMETIX-11D124-20260701-4239' in version)
add('final closure DTO exists', 'PurchaseInvoiceImportFinalClosureStatusDto' in dtos)
add('final closure endpoint exists', '/final-closure-status' in endpoints)
add('final closure service exists', 'GetFinalClosureStatusAsync' in service and 'FinalClosureKnownLimitations' in service)
add('import acceptance renders closure card', 'Purchase Import final closure' in page and 'knownLimitations' in page and 'nextModuleCandidates' in page)
add('duplicate recent table row close fixed', '              </tr>\n              </tr>' not in page)
add('backup manifest stage updated', 'Stage 11D-124 Purchase Import Final Closure' in backup)
add('stage doc exists', 'Purchase Import Final Closure - v4.12.39' in doc)

failed = [name for name, ok in checks if not ok]
for name, ok in checks:
    print(f"{'PASS' if ok else 'FAIL'} - {name}")
if failed:
    raise SystemExit(f"Purchase Import final closure validation failed: {failed}")
print('Purchase Import final closure static validation passed.')
