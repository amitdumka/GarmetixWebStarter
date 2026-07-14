#!/usr/bin/env python3
from pathlib import Path
import sys

ROOT = Path(__file__).resolve().parents[2]

checks = {
    'backend/Garmetix.Api/AppInfo/AppInfoEndpoints.cs': [
        '4.11.57',
        'Stage 11D-42 Final PDF Print Acceptance Evidence',
        'GARMETIX-11D42-20260626-4257'
    ],
    'frontend/garmetix-web/utils/appVersion.ts': [
        "APP_VERSION = '4.11.57'",
        'Stage 11D-42 Final PDF Print Acceptance Evidence'
    ],
    'frontend/garmetix-web/package.json': [
        '"version": "4.11.57"'
    ],
    'backend/Garmetix.Api/Garmetix.Api.csproj': [
        '<Version>4.11.57</Version>',
        '4.11.57-stage11d42-final-pdf-print-acceptance-evidence'
    ],
    'backend/Garmetix.Api/Production/PrintAcceptanceEndpoints.cs': [
        'MapGet("/evidence", EvidenceAsync)',
        'MapPost("/evidence", SaveEvidenceAsync)',
        'PrintFinalAcceptanceEvidence',
        'AuditLogEntries.Add(new AuditLogEntry',
        'BuildScenarios(docs)',
        'saleA4',
        'saleA5',
        'purchaseA4',
        'purchaseA5',
        'largeInvoicePagination',
        'amountBox',
        'footer',
        'signature',
        'pageSummary'
    ],
    'frontend/garmetix-web/pages/print-final-acceptance/index.vue': [
        'Save Print Evidence',
        'print-acceptance/evidence',
        'Final PDF print evidence checklist',
        'Saved evidence history',
        'Mark pass',
        'Mark fail'
    ],
    'docs/stages/stage-11/Stage11D42-Final-PDF-Print-Acceptance-Evidence-v4.11.57.md': [
        'Stage 11D-42',
        'AuditLogEntries',
        'Sale invoice A4',
        'Purchase inward A5'
    ],
    'README.md': [
        'v4.11.57 Stage 11D-42',
        'GET /api/print-acceptance/evidence',
        'POST /api/print-acceptance/evidence'
    ]
}

failed = False
for relative, needles in checks.items():
    path = ROOT / relative
    if not path.exists():
        print(f'MISSING: {relative}')
        failed = True
        continue
    text = path.read_text(errors='ignore')
    for needle in needles:
        if needle not in text:
            print(f'MISSING TOKEN in {relative}: {needle}')
            failed = True

if failed:
    print('Stage 11D-42 validation FAILED')
    sys.exit(1)

print('Stage 11D-42 validation passed')
