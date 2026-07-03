#!/usr/bin/env python3
from pathlib import Path
import sys

checks = {
    'backend/Garmetix.Api/AppInfo/AppInfoEndpoints.cs': ['4.12.26', 'Stage 11D-111 Day Book State Memory + Detail Drawer Polish', 'GARMETIX-11D111-20260630-4226', 'widens the detail drawer'],
    'frontend/garmetix-web/utils/appVersion.ts': ["APP_VERSION = '4.12.26'", 'Stage 11D-111 Day Book State Memory + Detail Drawer Polish', 'GARMETIX-11D111-20260630-4226', 'remembers Day Book date/type/search/page'],
    'frontend/garmetix-web/package.json': ['"version": "4.12.26"'],
    'backend/Garmetix.Api/Garmetix.Api.csproj': ['<Version>4.12.26</Version>', '4.12.26-stage11d111-day-book-state-memory-detail-drawer-polish'],
    'backend/Garmetix.Api/DayBook/DayBookEndpoints.cs': ['LineType = "Item"', 'LineType = "Payment"', 'var detail = new', 'PaymentDetails', 'CustomerMobileNumber', 'InwardNumber', 'DebitAmount = lines.Sum'],
    'frontend/garmetix-web/pages/day-book/index.vue': ['DAY_BOOK_STATE_KEY', 'sessionStorage.setItem', 'restoreDayBookState', 'persistDayBookState', 'garmetix.dayBook.listState.v1', 'sm:max-w-4xl lg:max-w-6xl xl:max-w-7xl', 'selectedDetail.row.id', 'hiddenDetailKeys', 'relevantKeyPattern', 'Transaction details'],
    'docs/stages/stage-11/Stage11D111-Day-Book-State-Memory-Detail-Drawer-Polish-v4.12.26.md': ['Day Book State Memory', 'v4.12.26', 'sessionStorage', 'detail drawer should be wide enough', 'transaction ID'],
}

failed = False
for file, tokens in checks.items():
    path = Path(file)
    if not path.exists():
        print(f'MISSING FILE: {file}')
        failed = True
        continue
    text = path.read_text(errors='ignore')
    for token in tokens:
        if token not in text:
            print(f'MISSING TOKEN in {file}: {token}')
            failed = True

page_path = Path('frontend/garmetix-web/pages/day-book/index.vue')
page = page_path.read_text(errors='ignore')
for forbidden in [
    'Journal entries are hidden by default',
    'Use Previous − / Next + to move date-wise like Tally Day Book',
    'jsonPreview',
    'Raw detail snapshot'
]:
    if forbidden in page:
        print(f'FORBIDDEN USER-FACING TOKEN remains in Day Book page: {forbidden}')
        failed = True

if 'function openSource' in page:
    open_source = page[page.find('function openSource'):page.find('function resetPageAndRefresh')]
    if 'persistDayBookState()' not in open_source:
        print('openSource must persist Day Book state before navigating away')
        failed = True
else:
    print('openSource function missing')
    failed = True

backend = Path('backend/Garmetix.Api/DayBook/DayBookEndpoints.cs').read_text(errors='ignore')
for raw_detail in [
    'new DayBookDetailDto(row, invoice,',
    'new DayBookDetailDto(row, voucher,',
    'new DayBookDetailDto(row, payment,',
    'new DayBookDetailDto(row, journal,'
]:
    if raw_detail in backend:
        print(f'Raw entity detail is still returned to Day Book drawer: {raw_detail}')
        failed = True

if failed:
    print('Stage 11D-111 validation FAILED')
    sys.exit(1)
print('Stage 11D-111 validation passed')
