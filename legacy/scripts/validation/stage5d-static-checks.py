#!/usr/bin/env python3
from pathlib import Path
import sys

root = Path(__file__).resolve().parents[2]
stock_ops = root / 'backend' / 'Garmetix.Api' / 'Inventory' / 'StockOperationEndpoints.cs'
text = stock_ops.read_text()
errors = []

if 'private static IQueryable<StockMovementRowDto> MovementQuery(HttpContext context, GarmetixDbContext db, int take)' not in text:
    errors.append('MovementQuery should accept take and own ordering before projection.')

if '.OrderByDescending(item => item.OnDate)\n            .ThenByDescending(item => item.Id)\n            .Take(take)\n            .Select(item => new StockMovementRowDto(' not in text:
    errors.append('Stock movement query must order StockMovement entity fields before StockMovementRowDto projection.')

for forbidden in [
    'MovementQuery(context, db)\n            .OrderByDescending(item => item.OnDate)',
    '.OrderByDescending(item => new StockMovementRowDto',
    ').OnDate)\' could not be translated'
]:
    if forbidden in text:
        errors.append(f'Forbidden stock movement runtime pattern remains: {forbidden}')

# Simple brace balance for the patched file.
balance = 0
for ch in text:
    if ch == '{':
        balance += 1
    elif ch == '}':
        balance -= 1
    if balance < 0:
        errors.append('Brace balance went negative in StockOperationEndpoints.cs')
        break
if balance != 0:
    errors.append(f'Brace balance mismatch in StockOperationEndpoints.cs: {balance}')

if errors:
    print('Stage 5D static checks failed:')
    for error in errors:
        print(f'- {error}')
    sys.exit(1)

print('Stage 5D static checks passed.')
