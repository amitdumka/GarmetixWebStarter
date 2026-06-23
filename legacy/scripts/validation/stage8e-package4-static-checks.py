from pathlib import Path

root = Path(__file__).resolve().parents[2]

def require(path: str, needle: str) -> None:
    text = (root / path).read_text()
    if needle not in text:
        raise SystemExit(f"Missing expected text in {path}: {needle}")

require('backend/Garmetix.Domain/Generated/Models/Accounting/AccountingEntries.cs', 'public class FinancialYearLock')
require('backend/Garmetix.Infrastructure/Data/GarmetixDbContext.cs', 'public DbSet<FinancialYearLock> FinancialYearLocks')
require('backend/Garmetix.Infrastructure/Data/GarmetixDbContext.cs', 'ValidateFinancialYearLocksAsync')
require('backend/Garmetix.Infrastructure/Data/GarmetixDbContext.cs', 'ValidateChangedJournalLines')
require('backend/Garmetix.Api/Accounting/AccountingDtos.cs', 'FinancialYearLockSaveRequest')
require('backend/Garmetix.Api/Accounting/AccountingDtos.cs', 'JournalValidationSummary')
require('backend/Garmetix.Api/Accounting/AccountingEndpoints.cs', '/financial-year-locks')
require('backend/Garmetix.Api/Accounting/AccountingEndpoints.cs', '/journal/validation')
require('backend/Garmetix.Api/Database/DatabaseSchemaRepairService.cs', 'CREATE TABLE IF NOT EXISTS "FinancialYearLocks"')
require('frontend/garmetix-web/pages/financial-year-locks/index.vue', 'Financial Year Locks')
require('frontend/garmetix-web/components/AppShell.vue', '/financial-year-locks')
require('deploy/macmini.env', 'RESET_DATABASE_ON_DEPLOY=false')
require('deploy/macmini.env.example', 'RESET_DATABASE_ON_DEPLOY=false')
require('docs/planning/CURRENT-ROADMAP.md', 'Stage 8E Package 4 Financial Year Locking and Journal Validation / v4.4.1')
print('Stage 8E Package 4 static validation passed.')
