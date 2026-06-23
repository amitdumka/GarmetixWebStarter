#!/usr/bin/env python3
from pathlib import Path

root = Path(__file__).resolve().parents[2]
checks = [
    (root / 'backend/Garmetix.Api/Validation/DataRepairDtos.cs', ['DataRepairActionDto', 'DataRepairRequest', 'DataRepairResultDto']),
    (root / 'backend/Garmetix.Api/Validation/DataConsistencyRepairEndpoints.cs', [
        'MapDataConsistencyRepairEndpoints',
        'BACKFILL_SALE_ITEM_SNAPSHOTS',
        'RECALCULATE_SALE_GST_HEADERS',
        'MERGE_DUPLICATE_DOCUMENT_SEQUENCES',
        'REBUILD_STOCK_QTY_FROM_LEDGER'
    ]),
    (root / 'backend/Garmetix.Api/Program.cs', ['app.MapDataConsistencyRepairEndpoints();']),
    (root / 'frontend/garmetix-web/pages/data-consistency/index.vue', [
        'Controlled Repair Tools',
        'data-consistency/repairs/actions',
        'data-consistency/repairs/preview',
        'data-consistency/repairs/apply'
    ]),
]

errors = []
for path, needles in checks:
    if not path.exists():
        errors.append(f'Missing file: {path.relative_to(root)}')
        continue
    text = path.read_text(encoding='utf-8')
    for needle in needles:
        if needle not in text:
            errors.append(f'Missing marker {needle!r} in {path.relative_to(root)}')

# basic C# brace balance, ignoring comments and strings sufficiently for generated endpoint files
for path in [root / 'backend/Garmetix.Api/Validation/DataRepairDtos.cs', root / 'backend/Garmetix.Api/Validation/DataConsistencyRepairEndpoints.cs']:
    text = path.read_text(encoding='utf-8')
    balance = 0
    in_string = in_char = line_comment = block_comment = escape = False
    for i, ch in enumerate(text):
        nxt = text[i+1] if i+1 < len(text) else ''
        if line_comment:
            if ch == '\n': line_comment = False
            continue
        if block_comment:
            if ch == '*' and nxt == '/': block_comment = False
            continue
        if in_string:
            if escape: escape = False
            elif ch == '\\': escape = True
            elif ch == '"': in_string = False
            continue
        if in_char:
            if escape: escape = False
            elif ch == '\\': escape = True
            elif ch == "'": in_char = False
            continue
        if ch == '/' and nxt == '/': line_comment = True; continue
        if ch == '/' and nxt == '*': block_comment = True; continue
        if ch == '"': in_string = True; continue
        if ch == "'": in_char = True; continue
        if ch == '{': balance += 1
        elif ch == '}': balance -= 1
        if balance < 0:
            errors.append(f'Negative brace balance in {path.relative_to(root)} at index {i}')
            break
    if balance != 0:
        errors.append(f'Brace balance {balance} in {path.relative_to(root)}')

if errors:
    print('Stage 4E static checks failed:')
    for error in errors:
        print('-', error)
    raise SystemExit(1)

print('Stage 4E static checks passed.')
