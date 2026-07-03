from pathlib import Path
import sys

root = Path(__file__).resolve().parents[2]
checks = []

def add(name, ok):
    checks.append((name, bool(ok)))

files = {
    'dtos': root / 'backend/Garmetix.Api/SaleImport/VyaparSaleImportDtos.cs',
    'service': root / 'backend/Garmetix.Api/SaleImport/VyaparSaleImportService.cs',
    'endpoints': root / 'backend/Garmetix.Api/SaleImport/VyaparSaleImportEndpoints.cs',
    'batches': root / 'frontend/garmetix-web/pages/billing/vyapar-import-batches.vue',
    'version': root / 'frontend/garmetix-web/utils/appVersion.ts',
    'app_info': root / 'backend/Garmetix.Api/AppInfo/AppInfoEndpoints.cs',
    'csproj': root / 'backend/Garmetix.Api/Garmetix.Api.csproj',
    'backup': root / 'scripts/linux/create-database-backup-now.sh',
    'doc': root / 'docs/stages/stage-11/Stage11D125-Vyapar-Sale-Import-Final-Summary-v4.12.40.md',
}
text = {key: path.read_text(errors='ignore') for key, path in files.items()}

add('new final summary dto present', 'VyaparSaleImportFinalSummaryDto' in text['dtos'] and 'VyaparSaleImportGstSummaryDto' in text['dtos'])
add('new final summary service present', 'GetFinalSummaryAsync' in text['service'] and 'billItemMismatchCount' in text['service'])
add('new final summary endpoint mapped', 'MapGet("/final-summary"' in text['endpoints'])
add('UI final summary card present', 'Vyapar Sale Import final summary + reconciliation' in text['batches'])
add('UI export CSV present', 'exportFinalSummaryCsv' in text['batches'] and 'vyapar-sale-import-final-summary.csv' in text['batches'])
add('frontend version v4.12.40', "APP_VERSION = '4.12.40'" in text['version'] and 'GARMETIX-11D125-20260701-4240' in text['version'])
add('backend app info v4.12.40', 'Version = "4.12.40"' in text['app_info'] and 'GARMETIX-11D125-20260701-4240' in text['app_info'])
add('csproj version v4.12.40', '<Version>4.12.40</Version>' in text['csproj'] and '<AssemblyVersion>4.12.40.0</AssemblyVersion>' in text['csproj'])
add('backup manifest stage updated', 'Stage 11D-125 Vyapar Sale Import Final Summary' in text['backup'])
add('stage documentation present', 'Vyapar Sale Import Final Summary + Reconciliation Closure - v4.12.40' in text['doc'])

failed = [name for name, ok in checks if not ok]
for name, ok in checks:
    print(f"{'PASS' if ok else 'FAIL'} - {name}")
if failed:
    print('\nFailed checks:')
    for name in failed:
        print(f' - {name}')
    sys.exit(1)
print('\nStage 11D-125 Vyapar sale import final summary static checks passed.')
