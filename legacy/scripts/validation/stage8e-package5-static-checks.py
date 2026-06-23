from pathlib import Path

root = Path(__file__).resolve().parents[2]


def require(path: str, needle: str) -> None:
    text = (root / path).read_text()
    if needle not in text:
        raise SystemExit(f"Missing expected text in {path}: {needle}")

require('backend/Garmetix.Api/Accounting/AccountingDtos.cs', 'LedgerSyncSummary')
require('backend/Garmetix.Api/Accounting/AccountingDtos.cs', 'LedgerSyncIssue')
require('backend/Garmetix.Api/Accounting/AccountingEndpoints.cs', '/ledger-sync/status')
require('backend/Garmetix.Api/Accounting/AccountingEndpoints.cs', '/ledger-sync/repair')
require('backend/Garmetix.Api/Accounting/AccountingPostingService.cs', 'ValidateLedgerSynchronizationAsync')
require('backend/Garmetix.Api/Accounting/AccountingPostingService.cs', 'PartyLedgerIsLinkedElsewhereAsync')
require('backend/Garmetix.Api/Accounting/AccountingPostingService.cs', 'BankLedgerIsLinkedElsewhereAsync')
require('frontend/garmetix-web/pages/accounting/index.vue', "key: 'ledgerSync'")
require('frontend/garmetix-web/pages/accounting/index.vue', 'repairLedgerSync')
require('frontend/garmetix-web/pages/accounting/index.vue', 'Repair ledger links')
require('docs/planning/CURRENT-ROADMAP.md', 'Stage 8E Package 5 Ledger Synchronization Hardening / v4.4.2')
require('docs/planning/CURRENT-ROADMAP.md', 'Harden automatic Party and Bank Account ledger synchronization in v4.4.2')
print('Stage 8E Package 5 static validation passed.')
