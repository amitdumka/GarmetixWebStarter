#!/usr/bin/env python3
from pathlib import Path
import sys

checks = {
    'backend/Garmetix.Api/AppInfo/AppInfoEndpoints.cs': ['4.12.25', 'Stage 11D-110 Day Book Date Next Placement Polish', 'GARMETIX-11D110-20260630-4225', 'places + Next immediately after Today'],
    'frontend/garmetix-web/utils/appVersion.ts': ["APP_VERSION = '4.12.25'", 'Stage 11D-110 Day Book Date Next Placement Polish', 'GARMETIX-11D110-20260630-4225', 'places + Next immediately after Today'],
    'frontend/garmetix-web/package.json': ['"version": "4.12.25"'],
    'backend/Garmetix.Api/Garmetix.Api.csproj': ['<Version>4.12.25</Version>', '4.12.25-stage11d110-day-book-date-next-placement-polish'],
    'backend/Garmetix.Api/DayBook/DayBookEndpoints.cs': ['bool includeJournal = false', 'showJournalEntries', 'includeJournal || string.Equals(type, "journal"', 'if (showJournalEntries)'],
    'frontend/garmetix-web/pages/day-book/index.vue': ['includeJournalEntries', "params.set('includeJournal', 'true')", 'Show journal entries', '− Previous', 'label="Today"', 'label="+ Next"', 'detailRows', 'relatedLineColumns', 'Transaction details', 'tableValue'],
    'docs/stages/stage-11/Stage11D110-Day-Book-Date-Next-Placement-Polish-v4.12.25.md': ['Day Book Date Next Placement', 'v4.12.25', '+ Next immediately after Today', 'explanatory badges removed'],
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

book_date_start = page.find('<UFormField label="Book date">')
book_date_end = page.find('</UFormField>', book_date_start)
if book_date_start < 0 or book_date_end < 0:
    print('Could not locate Book date form field')
    failed = True
else:
    book_date_block = page[book_date_start:book_date_end]
    if 'label="+ Next"' in book_date_block or 'label="Next +"' in book_date_block:
        print('+ Next must not be inside Book date field; it should be after Today')
        failed = True

today_index = page.find('label="Today"')
next_index = page.find('label="+ Next"')
if today_index < 0 or next_index < 0 or next_index < today_index:
    print('+ Next button is not immediately placed after the Today action in Day Book controls')
    failed = True
else:
    between = page[today_index:next_index]
    if 'UBadge' in between or '</div>' in between:
        print('+ Next is not directly after Today in the same action row')
        failed = True

if failed:
    print('Stage 11D-110 validation FAILED')
    sys.exit(1)
print('Stage 11D-110 validation passed')
