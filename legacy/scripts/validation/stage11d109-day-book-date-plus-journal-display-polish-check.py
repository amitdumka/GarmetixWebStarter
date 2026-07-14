#!/usr/bin/env python3
from pathlib import Path
import sys

checks = {
    'backend/Garmetix.Api/AppInfo/AppInfoEndpoints.cs': ['4.12.24', 'Stage 11D-109 Day Book Date Plus + Journal Display Polish', 'GARMETIX-11D109-20260630-4224'],
    'frontend/garmetix-web/utils/appVersion.ts': ["APP_VERSION = '4.12.24'", 'Stage 11D-109 Day Book Date Plus + Journal Display Polish', 'GARMETIX-11D109-20260630-4224'],
    'frontend/garmetix-web/package.json': ['"version": "4.12.24"'],
    'backend/Garmetix.Api/Garmetix.Api.csproj': ['<Version>4.12.24</Version>', '4.12.24-stage11d109-day-book-date-plus-journal-display-polish'],
    'backend/Garmetix.Api/DayBook/DayBookEndpoints.cs': ['bool includeJournal = false', 'showJournalEntries', 'includeJournal || string.Equals(type, "journal"', 'if (showJournalEntries)'],
    'frontend/garmetix-web/pages/day-book/index.vue': ['includeJournalEntries', "params.set('includeJournal', 'true')", 'Show journal entries', 'Journal entries are hidden by default', '− Previous', 'Next +', 'detailRows', 'relatedLineColumns', 'Transaction details', 'tableValue'],
    'docs/stages/stage-11/Stage11D109-Day-Book-Date-Plus-Journal-Display-Polish-v4.12.24.md': ['Day Book Date Plus', 'v4.12.24', 'Show journal entries', 'key/value table'],
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

page = Path('frontend/garmetix-web/pages/day-book/index.vue').read_text(errors='ignore')
for forbidden in ['jsonPreview', 'Raw detail snapshot']:
    if forbidden in page:
        print(f'FORBIDDEN USER-FACING JSON TOKEN remains in Day Book page: {forbidden}')
        failed = True

if failed:
    print('Stage 11D-109 validation FAILED')
    sys.exit(1)
print('Stage 11D-109 validation passed')
