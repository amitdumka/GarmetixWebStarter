from pathlib import Path

root = Path(__file__).resolve().parents[2]

def read(path: str) -> str:
    return (root / path).read_text()

def require(path: str, needle: str) -> None:
    text = read(path)
    if needle not in text:
        raise SystemExit(f"Missing expected text in {path}: {needle}")

require('backend/Garmetix.Domain/Generated/Models/Accounting/AccountingEntries.cs', 'ReconciliationReference')
require('backend/Garmetix.Domain/Generated/Models/Accounting/Banks.cs', 'public bool Reconciled')
require('backend/Garmetix.Domain/Generated/Models/Accounting/Banks.cs', 'public DateTime? ClearedAt')
require('backend/Garmetix.Api/Accounting/AccountingDtos.cs', 'BankReconciliationSummary')
require('backend/Garmetix.Api/Accounting/AccountingDtos.cs', 'ChequeLifecycleRequest')
require('backend/Garmetix.Api/Accounting/AccountingEndpoints.cs', '/bank-reconciliation/{bankAccountId:guid}')
require('backend/Garmetix.Api/Accounting/AccountingEndpoints.cs', '/bank-statement-lines/{id:guid}/reconcile')
require('backend/Garmetix.Api/Accounting/AccountingEndpoints.cs', '/cheque-logs/{id:guid}/lifecycle')
require('backend/Garmetix.Api/Accounting/AccountingPostingService.cs', 'ReconcileBankStatementLineAsync')
require('backend/Garmetix.Api/Accounting/AccountingPostingService.cs', 'UpdateChequeLifecycleAsync')
require('backend/Garmetix.Api/Database/DatabaseSchemaRepairService.cs', 'IX_BankStatementLines_CompanyId_BankAccountId_Reconciled')
require('frontend/garmetix-web/pages/accounting/index.vue', "key: 'reconciliation'")
require('frontend/garmetix-web/pages/accounting/index.vue', 'markStatementReconciled')
require('frontend/garmetix-web/pages/accounting/index.vue', 'updateChequeStatus')
require('docs/planning/CURRENT-ROADMAP.md', 'Complete bank reconciliation and cheque issue/deposit/clear/bounce lifecycle auditing in v4.4.0')
print('Stage 8E Package 3 static validation passed.')
