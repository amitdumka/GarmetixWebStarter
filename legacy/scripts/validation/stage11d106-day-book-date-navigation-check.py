#!/usr/bin/env python3
from pathlib import Path
import sys

checks = [
    ('backend/Garmetix.Api/AppInfo/AppInfoEndpoints.cs', ['4.12.21', 'Stage 11D-106 Day Book Date Navigation', 'GARMETIX-11D106-20260630-4221']),
    ('frontend/garmetix-web/utils/appVersion.ts', ["APP_VERSION = '4.12.21'", 'Stage 11D-106 Day Book Date Navigation']),
    ('backend/Garmetix.Api/Garmetix.Api.csproj', ['<Version>4.12.21</Version>', '4.12.21-stage11d106-day-book-date-navigation']),
    ('backend/Garmetix.Api/DayBook/DayBookEndpoints.cs', ['DateTime? date = null', 'datePreset', '"date" or "day" or "single-day"']),
    ('frontend/garmetix-web/pages/day-book/index.vue', ["const datePreset = ref('date')", 'selectedDate', 'moveDay(-1)', 'moveDay(1)', 'Book date', 'Use − / + to move previous or next date']),
    ('docs/stages/stage-11/Stage11D106-Day-Book-Date-Navigation-v4.12.21.md', ['Stage 11D-106', 'Stage 11D-107']),
]

failed = False
for file, tokens in checks:
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
if failed:
    print('Stage 11D-106 validation FAILED')
    sys.exit(1)
print('Stage 11D-106 validation passed')
