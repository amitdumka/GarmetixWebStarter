#!/usr/bin/env python3
from __future__ import annotations

import json
import subprocess
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
errors: list[str] = []

def read(rel: str) -> str:
    return (ROOT / rel).read_text(encoding='utf-8')

def add(name: str, ok: bool):
    if not ok:
        errors.append(name)

pkg = json.loads(read('frontend/garmetix-web/package.json'))
lock = json.loads(read('frontend/garmetix-web/package-lock.json'))
readme = read('README.md')
docs_readme = read('docs/README.md')

add('frontend package version 4.9.20', pkg.get('version') == '4.9.20')
add('frontend lock version 4.9.20', lock.get('version') == '4.9.20' and lock.get('packages', {}).get('', {}).get('version') == '4.9.20')
add('readme current package21', 'Stage 8I Package 21 Client License and SaaS Activation v4.9.20' in readme and 'GARMETIX-8I-20260619-49200' in readme)
add('docs readme current package21', 'Stage 8I Package 21 Client License and SaaS Activation v4.9.20' in docs_readme and 'GARMETIX-8I-20260619-49200' in docs_readme)

subprocess.run([sys.executable, str(ROOT / 'scripts/validation/license-saas-activation-check.py')], cwd=ROOT, check=True)

if errors:
    print('Stage 8I Package 21 static checks failed:', file=sys.stderr)
    for error in errors:
        print(f' - {error}', file=sys.stderr)
    sys.exit(1)

print('Stage 8I Package 21 static checks passed.')
