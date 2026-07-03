#!/usr/bin/env python3
from pathlib import Path
import sys

checks = {
    'backend/Garmetix.Api/AppInfo/AppInfoEndpoints.cs': ['4.12.27', 'Stage 11D-112 Day Book Return Link + Live Navigation QA', 'GARMETIX-11D112-20260630-4227', 'Back to Day Book return navigation'],
    'frontend/garmetix-web/utils/appVersion.ts': ["APP_VERSION = '4.12.27'", 'Stage 11D-112 Day Book Return Link + Live Navigation QA', 'GARMETIX-11D112-20260630-4227', 'Back to Day Book return navigation'],
    'frontend/garmetix-web/package.json': ['"version": "4.12.27"'],
    'backend/Garmetix.Api/Garmetix.Api.csproj': ['<Version>4.12.27</Version>', '4.12.27-stage11d112-day-book-return-link-live-navigation-qa'],
    'frontend/garmetix-web/pages/day-book/index.vue': ['DAY_BOOK_STATE_KEY', 'persistDayBookState', 'sourceWithDayBookReturnHint', "params.set('fromDayBook', '1')", 'navigateTo(sourceWithDayBookReturnHint(source))'],
    'frontend/garmetix-web/components/UiDayBookReturnButton.vue': ['Back to Day Book', 'DAY_BOOK_STATE_KEY', 'fromDayBook', '/day-book?restore=1', 'sessionStorage.getItem'],
    'docs/stages/stage-11/Stage11D112-Day-Book-Return-Link-Live-Navigation-QA-v4.12.27.md': ['Stage 11D-112', 'v4.12.27', 'Back to Day Book', 'fromDayBook=1', 'sessionStorage'],
}

source_pages = [
    'frontend/garmetix-web/pages/billing/index.vue',
    'frontend/garmetix-web/pages/purchase/index.vue',
    'frontend/garmetix-web/pages/billing/new.vue',
    'frontend/garmetix-web/pages/purchase/new.vue',
    'frontend/garmetix-web/pages/vouchers/index.vue',
    'frontend/garmetix-web/pages/cash-vouchers/index.vue',
    'frontend/garmetix-web/pages/vendor-payments/index.vue',
    'frontend/garmetix-web/pages/accounting/index.vue',
]

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

for file in source_pages:
    path = Path(file)
    text = path.read_text(errors='ignore') if path.exists() else ''
    if '<UiDayBookReturnButton />' not in text:
        print(f'MISSING Day Book return button in source page: {file}')
        failed = True

page = Path('frontend/garmetix-web/pages/day-book/index.vue').read_text(errors='ignore')
for raw_link in [
    "navigateTo('/billing/new')",
    "navigateTo('/purchase/new')",
    "navigateTo('/vendor-payments?new=invoice')",
    "navigateTo('/vouchers?new=1')",
    "if (source) navigateTo(source)"
]:
    if raw_link in page:
        print(f'Day Book still navigates away without return hint: {raw_link}')
        failed = True

if failed:
    print('Stage 11D-112 validation FAILED')
    sys.exit(1)
print('Stage 11D-112 validation passed')
