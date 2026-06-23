from pathlib import Path
import re
root = Path(__file__).resolve().parents[2]

service = (root / 'backend/Garmetix.Api/Seeds/AfssDefaultSeederService.cs').read_text()
for token in [
    '"Smart Menswear profile merges into Aadwika Fashion company under Aadwika Fashion MBO store group.",',
    'Seeder2CsOnly:',
    'Smart Menswear',
]:
    if token not in service:
        raise SystemExit(f'Missing service token: {token}')

lines = service.splitlines()
for index in range(len(lines) - 1):
    current = lines[index].strip()
    nxt = lines[index + 1].strip()
    if re.match(r'^".*"$', current) and nxt.startswith('"'):
        raise SystemExit(f'Possible missing comma between string literals at line {index + 1}')

checks = {
    'backend/Garmetix.Api/Seeds/SeederVerificationEndpoints.cs': [
        'MapSeederVerificationEndpoints',
        'SeederVerificationStatusDto',
        'Aadwika Fashion MBO',
        'Smart Menswear store under Aadwika Fashion MBO',
        'Shalini company/profile is separate',
        'Protected default ledger groups',
    ],
    'backend/Garmetix.Api/Program.cs': [
        'app.MapSeederVerificationEndpoints();',
    ],
    'frontend/garmetix-web/pages/af-ss/index.vue': [
        'Seeder/Merge Verification',
        'seeder-verification/status',
        'Verify seeder',
        'refreshSeederVerification',
    ],
    'docs/planning/TODO.md': [
        'Completed in v4.9.5',
        'Fixed AF/SS seeder comparison syntax error',
    ],
}
for rel, tokens in checks.items():
    text = (root / rel).read_text()
    for token in tokens:
        if token not in text:
            raise SystemExit(f'Missing {token} in {rel}')

version = (root / 'frontend/garmetix-web/utils/appVersion.ts').read_text()
for token in ["APP_VERSION = '4.9.5'", 'Stage 8I Package 6 Seeder Verification', 'GARMETIX-8I-20260618-4950']:
    if token not in version:
        raise SystemExit(f'Missing frontend version token: {token}')

backend = (root / 'backend/Garmetix.Api/AppInfo/AppInfoEndpoints.cs').read_text()
for token in ['Version = "4.9.5"', 'Stage 8I Package 6 Seeder Verification', 'GARMETIX-8I-20260618-4950']:
    if token not in backend:
        raise SystemExit(f'Missing backend version token: {token}')

for rel in [
    'docs/stages/stage-8/Stage8I-Package6-SeederVerification-v4.9.5-Notes.md',
    'docs/operations/Seeder-Verification-v4.9.5.md',
]:
    if not (root / rel).exists():
        raise SystemExit(f'Missing doc: {rel}')

print('Stage 8I Package 6 static validation passed.')
