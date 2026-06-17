from pathlib import Path
root = Path(__file__).resolve().parents[2]
checks = []

def require(name, condition):
    checks.append((name, bool(condition)))

tailoring_page = (root / 'frontend/garmetix-web/pages/tailoring/index.vue').read_text()
endpoints = (root / 'backend/Garmetix.Api/Tailoring/TailoringEndpoints.cs').read_text()
dtos = (root / 'backend/Garmetix.Api/Tailoring/TailoringDtos.cs').read_text()
version = (root / 'frontend/garmetix-web/utils/appVersion.ts').read_text()
appinfo = (root / 'backend/Garmetix.Api/AppInfo/AppInfoEndpoints.cs').read_text()

require('separate stitching tab exists', "activeTab === 'stitching'" in tailoring_page and 'Tailoring / Stitching' in tailoring_page)
require('separate alteration tab exists', "activeTab === 'alteration'" in tailoring_page and 'New Alteration Order' in tailoring_page)
require('customer mobile lookup wired', 'customers/by-mobile' in tailoring_page and 'CustomerByMobileAsync' in endpoints)
require('source sale invoice item lookup wired', 'alteration/source-invoices' in tailoring_page and 'AlterationSourceInvoiceItemsAsync' in endpoints)
require('delivery board wired', 'deliveries/overview' in tailoring_page and 'DeliveryOverviewAsync' in endpoints)
require('status update endpoint wired', '/status' in endpoints and 'UpdateStatusAsync' in endpoints)
require('5 percent GST service invoice wired', 'ServiceGstRate = 5m' in endpoints and 'TaxPercentage = ServiceGstRate' in endpoints and 'GST 5%' in tailoring_page)
require('two copy print payload wired', 'PrintOrderAsync' in endpoints and 'PrintInvoiceAsync' in endpoints and 'Customer Copy' in endpoints and 'Store Copy' in endpoints)
require('in-house alteration cost impact wired', 'ApplyInHouseAlterationCostImpactAsync' in endpoints and 'CostPrice' in endpoints)
require('safe select values used', "value: ''" not in tailoring_page and "'__none'" in tailoring_page)
require('DTOs added', 'TailoringPrintDto' in dtos and 'TailoringDeliveryOverviewDto' in dtos)
require('version updated', "APP_VERSION = '4.7.3'" in version and 'Stage 8H Package 4 Tailoring and Alteration Pro' in version and 'GARMETIX-8H-20260617-4730' in version)
require('backend appinfo updated', 'Version = "4.7.3"' in appinfo and 'Stage 8H Package 4 Tailoring and Alteration Pro' in appinfo)

failed = [name for name, ok in checks if not ok]
for name, ok in checks:
    print(f"[{'PASS' if ok else 'FAIL'}] {name}")
if failed:
    raise SystemExit(f'{len(failed)} Stage 8H Package 4 check(s) failed: {failed}')
print('Stage 8H Package 4 Tailoring/Alteration Pro static validation passed.')
