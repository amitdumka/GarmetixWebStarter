#!/usr/bin/env python3
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
checks = {
    'backend/Garmetix.Api/InvoiceReplacement/InvoiceReplacementEndpoints.cs': [
        'MapInvoiceReplacementEndpoints',
        '/api/invoice-replacements',
        '/pending',
        '/audit',
        'ApproveSalesReplacementAsync',
        'ApprovePurchaseReplacementAsync',
        'CancelSaleForReplacementApprovalAsync',
        'CancelPurchaseForReplacementApprovalAsync',
    ],
    'backend/Garmetix.Api/InvoiceReplacement/InvoiceReplacementAudit.cs': [
        'Invoice Replacement',
        'AuditLogEntry',
        'InvoiceReplacementApproval',
    ],
    'backend/Garmetix.Api/Billing/BillingDtos.cs': [
        'OriginalInvoiceId',
        'ReplacementApprovalRequested',
        'ReplacementReason',
    ],
    'backend/Garmetix.Api/Purchase/PurchaseDtos.cs': [
        'OriginalInvoiceId',
        'ReplacementApprovalRequested',
        'ReplacementReason',
    ],
    'frontend/garmetix-web/pages/invoice-replacements/index.vue': [
        'Invoice Replacement Approvals',
        'invoice-replacements/pending',
        'invoice-replacements/audit',
        'Approve & Reverse Old Invoice',
    ],
    'frontend/garmetix-web/pages/billing/new.vue': [
        'replacementApprovalRequested',
        'originalInvoiceId: shouldReplaceOriginal ? sourceInvoiceId : null',
        'submit replacement for owner/admin approval',
    ],
    'frontend/garmetix-web/pages/purchase/new.vue': [
        'replacementApprovalRequested',
        'originalInvoiceId: shouldReplaceOriginal ? sourceInvoiceId : null',
        'submit replacement for owner/admin approval',
    ],
    'frontend/garmetix-web/components/AppShell.vue': ['Invoice Replacements'],
    'backend/Garmetix.Api/AppInfo/AppInfoEndpoints.cs': ['4.11.56', 'Stage 11D-41'],
    'frontend/garmetix-web/utils/appVersion.ts': ['4.11.56', 'Stage 11D-41'],
}

missing = []
for rel, needles in checks.items():
    path = ROOT / rel
    if not path.exists():
        missing.append(f'MISSING FILE: {rel}')
        continue
    text = path.read_text(encoding='utf-8')
    for needle in needles:
        if needle not in text:
            missing.append(f'MISSING TEXT in {rel}: {needle}')

# Lightweight syntax balance checks for edited C# files.
for rel in [
    'backend/Garmetix.Api/InvoiceReplacement/InvoiceReplacementEndpoints.cs',
    'backend/Garmetix.Api/InvoiceReplacement/InvoiceReplacementAudit.cs',
    'backend/Garmetix.Api/Billing/BillingEndpoints.cs',
    'backend/Garmetix.Api/Purchase/PurchaseEndpoints.cs',
]:
    text = (ROOT / rel).read_text(encoding='utf-8')
    if text.count('{') != text.count('}'):
        missing.append(f'BRACE IMBALANCE: {rel}')
    if text.count('(') != text.count(')'):
        missing.append(f'PAREN IMBALANCE: {rel}')

if missing:
    print('Stage 11D-41 validation FAILED')
    for item in missing:
        print('-', item)
    raise SystemExit(1)

print('Stage 11D-41 validation passed')
