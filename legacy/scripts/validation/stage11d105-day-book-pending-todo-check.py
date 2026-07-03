#!/usr/bin/env python3
from pathlib import Path
import sys

root = Path(__file__).resolve().parents[2]
checks = [
    ('backend/Garmetix.Api/AppInfo/AppInfoEndpoints.cs', ['4.12.20', 'Stage 11D-105 Day Book Pending TODO Register', 'GARMETIX-11D105-20260629-4220']),
    ('frontend/garmetix-web/utils/appVersion.ts', ["APP_VERSION = '4.12.20'", 'Stage 11D-105 Day Book Pending TODO Register']),
    ('backend/Garmetix.Api/Garmetix.Api.csproj', ['<Version>4.12.20</Version>', '4.12.20-stage11d105-day-book-pending-todo-register']),
    ('backend/Garmetix.Api/DayBook/DayBookEndpoints.cs', ['MapDayBookEndpoints', 'GET /api/day-book', 'SaleInvoice', 'PurchaseInward', 'VendorPayment', 'JournalEntry']),
    ('backend/Garmetix.Api/DayBook/DayBookDtos.cs', ['DayBookRowDto', 'DayBookResultDto', 'DayBookDetailDto']),
    ('backend/Garmetix.Api/Program.cs', ['using Garmetix.Api.DayBook;', 'app.MapDayBookEndpoints();']),
    ('frontend/garmetix-web/pages/day-book/index.vue', ['AppShell', 'Day Book', 'openDetail', 'detailApiPath', 'sourcePath']),
    ('frontend/garmetix-web/components/AppShell.vue', ["to: '/day-book'", "label: 'Day Book'"]),
    ('frontend/garmetix-web/components/AppShellLegacy.vue', ["to: '/day-book'", "label: 'Day Book'"]),
    ('frontend/garmetix-web/composables/useAccessControl.ts', ["path: '/day-book'", "label: 'Day Book'"]),
    ('docs/todo/GARMETIX-PENDING-TODO-v4.12.20.md', ['Vyapar Sale Import', 'Purchase Inward Date', 'Day Book', 'Payroll']),
    ('docs/stages/stage-11/Stage11D105-Day-Book-Pending-TODO-v4.12.20.md', ['Stage 11D-105', 'Stage 11D-106']),
]
failed = False
for rel, tokens in checks:
    path = root / rel
    if not path.exists():
        print(f'MISSING FILE: {rel}')
        failed = True
        continue
    text = path.read_text(errors='ignore')
    for token in tokens:
        if token not in text:
            print(f'MISSING TOKEN in {rel}: {token}')
            failed = True
if failed:
    print('Stage 11D-105 validation FAILED')
    sys.exit(1)
print('Stage 11D-105 validation passed')
