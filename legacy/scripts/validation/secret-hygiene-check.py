#!/usr/bin/env python3
from __future__ import annotations

import re
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
errors: list[str] = []

FORBIDDEN_FILES = [
    'deploy/macmini.env',
    '.env.production',
    '.env.local',
]

SKIP_DIRS = {
    '.git', 'node_modules', '.nuxt', '.output', 'bin', 'obj', 'backups'
}

SECRET_PATTERNS = [
    ('Cloudflare API token', re.compile(r'\bcf(?:ut|pat|api)_[A-Za-z0-9_-]{20,}\b')),
    ('JWT/private key material', re.compile(r'-----BEGIN (?:RSA |EC |OPENSSH |)PRIVATE KEY-----')),
    ('non-placeholder Cloudflare token assignment', re.compile(r'^\s*CLOUDFLARE_API_TOKEN\s*=\s*(?!CHANGE_ME|your_|$).+', re.IGNORECASE | re.MULTILINE)),
    ('non-placeholder SSH password assignment', re.compile(r'^\s*SSH_PASSWORD\s*=\s*(?!CHANGE_ME|your_|$).+', re.IGNORECASE | re.MULTILINE)),
    ('non-placeholder sudo password assignment', re.compile(r'^\s*SUDO_PASSWORD\s*=\s*(?!CHANGE_ME|your_|$).+', re.IGNORECASE | re.MULTILINE)),
]

ALLOW_FILES = {
    'deploy/macmini.env.example',
}

for rel in FORBIDDEN_FILES:
    if (ROOT / rel).exists():
        errors.append(f'forbidden private file present: {rel}')

for path in ROOT.rglob('*'):
    if not path.is_file():
        continue
    parts = set(path.relative_to(ROOT).parts)
    if parts & SKIP_DIRS:
        continue
    rel = path.relative_to(ROOT).as_posix()
    if rel.endswith(('.png', '.jpg', '.jpeg', '.webp', '.ico', '.docx', '.zip', '.gz', '.dll', '.pdb')):
        continue
    try:
        text = path.read_text(encoding='utf-8')
    except UnicodeDecodeError:
        continue

    for label, pattern in SECRET_PATTERNS:
        if rel in ALLOW_FILES and label.endswith('assignment'):
            continue
        match = pattern.search(text)
        if match:
            errors.append(f'{label} detected in {rel}')

if errors:
    print('Secret hygiene check failed:', file=sys.stderr)
    for error in errors:
        print(f' - {error}', file=sys.stderr)
    sys.exit(1)

print('Secret hygiene check passed: no local deploy env or obvious private token patterns found.')
