#!/usr/bin/env python3
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
checks = {
    "backend/Garmetix.Api/Billing/BillingEndpoints.cs": [
        'group.MapGet("/sales", SearchSalesAsync)',
        'private static async Task<PagedSaleInvoicesDto> SearchSalesAsync',
        'ResolveSalesDateRange',
        'datePreset = "today"',
        '.Skip((page - 1) * pageSize)',
        '.Take(pageSize)',
        'invoice.CustomerGSTIN'
    ],
    "backend/Garmetix.Api/Billing/BillingDtos.cs": [
        'public sealed record PagedSaleInvoicesDto',
        'IReadOnlyList<RecentInvoiceDto> Items',
        'decimal BalanceAmount'
    ],
    "frontend/garmetix-web/pages/billing/index.vue": [
        "const saleDatePreset = ref('today')",
        'saleDatePresetOptions',
        'invoicePageSize',
        'billing/sales?',
        'invoicePageFrom',
        'invoiceTotalPages',
        'saleSearchTimer',
        "saleDatePreset === 'custom'",
        "saleDatePreset === 'month-year'"
    ],
    "backend/Garmetix.Api/AppInfo/AppInfoEndpoints.cs": [
        '4.11.59',
        'Stage 11D-44 Sale Invoice Filters Pagination',
        'GARMETIX-11D44-20260626-4459'
    ],
    "frontend/garmetix-web/utils/appVersion.ts": [
        "APP_VERSION = '4.11.59'",
        'Stage 11D-44 Sale Invoice Filters Pagination'
    ],
    "frontend/garmetix-web/package.json": [
        '"version": "4.11.59"'
    ],
    "backend/Garmetix.Api/Garmetix.Api.csproj": [
        '<Version>4.11.59</Version>',
        '<AssemblyVersion>4.11.59.0</AssemblyVersion>'
    ],
    "docs/stages/stage-11/Stage11D44-Sale-Invoice-Filters-Pagination-v4.11.59.md": [
        'GET /api/billing/sales',
        'Today default filter',
        'No new database table or migration'
    ]
}

missing = []
for rel, tokens in checks.items():
    path = ROOT / rel
    if not path.exists():
        missing.append(f"MISSING FILE: {rel}")
        continue
    text = path.read_text(errors='ignore')
    for token in tokens:
        if token not in text:
            missing.append(f"MISSING TOKEN in {rel}: {token}")

if missing:
    print('\n'.join(missing))
    print('Stage 11D-44 validation FAILED')
    raise SystemExit(1)

print('Stage 11D-44 validation passed')
