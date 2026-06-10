#!/usr/bin/env python3
from pathlib import Path
import sys

root = Path(__file__).resolve().parents[2]
checks = [
    root / 'backend/Garmetix.Api/Seeds/AfssSeederDtos.cs',
    root / 'backend/Garmetix.Api/Seeds/AfssDefaultSeederService.cs',
    root / 'backend/Garmetix.Api/Seeds/AfssSeederEndpoints.cs',
    root / 'frontend/garmetix-web/pages/af-ss/index.vue',
    root / 'frontend/garmetix-web/components/AppShell.vue',
    root / 'backend/Garmetix.Api/Program.cs',
]

missing = [str(path.relative_to(root)) for path in checks if not path.exists()]
if missing:
    print('Missing expected files:', ', '.join(missing))
    sys.exit(1)

service = (root / 'backend/Garmetix.Api/Seeds/AfssDefaultSeederService.cs').read_text()
required_service_tokens = [
    'ProductGroup.Shirting',
    'ProductType.Readymade',
    'StockType.Opening',
    'StockMovement',
    'PasswordHasher.Hash',
    'public static AfssSeederComparisonDto Comparison',
    'Samrat Menswear',
    'Seeder.cs + seeder2.cs',
]
for token in required_service_tokens:
    if token not in service:
        print(f'Missing service token: {token}')
        sys.exit(1)

program = (root / 'backend/Garmetix.Api/Program.cs').read_text()
if 'using Garmetix.Api.Seeds;' not in program or 'app.MapAfssSeederEndpoints();' not in program:
    print('Program.cs is not wired for AF/SS seeder endpoints')
    sys.exit(1)

page = (root / 'frontend/garmetix-web/pages/af-ss/index.vue').read_text()
for token in ['afss-seeder/options', 'afss-seeder/seed', 'Seed selected company', 'Seeder comparison']:
    if token not in page:
        print(f'Missing page token: {token}')
        sys.exit(1)

shell = (root / 'frontend/garmetix-web/components/AppShell.vue').read_text()
if "to: '/af-ss'" not in shell or "label: 'AF/SS'" not in shell:
    print('Admin AF/SS menu item not found')
    sys.exit(1)

for path in checks[:3]:
    text = path.read_text()
    balance = 0
    in_string = False
    escape = False
    for char in text:
        if in_string:
            if escape:
                escape = False
            elif char == '\\':
                escape = True
            elif char == '"':
                in_string = False
        else:
            if char == '"':
                in_string = True
            elif char == '{':
                balance += 1
            elif char == '}':
                balance -= 1
                if balance < 0:
                    print(f'Brace imbalance in {path.relative_to(root)}')
                    sys.exit(1)
    if balance != 0:
        print(f'Brace imbalance in {path.relative_to(root)}: {balance}')
        sys.exit(1)

print('Stage 5F static checks passed.')
