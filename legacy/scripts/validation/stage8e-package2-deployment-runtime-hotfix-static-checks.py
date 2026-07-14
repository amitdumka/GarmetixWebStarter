#!/usr/bin/env python3
from pathlib import Path

root = Path(__file__).resolve().parents[2]
errors: list[str] = []

migration = root / 'backend/Garmetix.Infrastructure/Data/Migrations/20260614094500_SeparateNonGstGoodsFromBooks.cs'
text = migration.read_text()
required = [
    "IF to_regclass('\"NonGstGoodsDocuments\"') IS NOT NULL THEN",
    "IF to_regclass('\"JournalLines\"') IS NOT NULL",
    "AND to_regclass('\"JournalEntries\"') IS NOT NULL THEN",
    'UPDATE "NonGstGoodsDocuments"',
]
for item in required:
    if item not in text:
        errors.append(f'Missing migration hardening marker: {item}')

old_unconditional = '''UPDATE "NonGstGoodsDocuments"
            SET'''
if old_unconditional in text:
    errors.append('Migration still appears to have the old unconditional non-GST document update block.')

nuxt = (root / 'frontend/garmetix-web/nuxt.config.ts').read_text()
if "serverBundle" not in nuxt or "collections: ['lucide']" not in nuxt:
    errors.append('Nuxt lucide icon server bundle config is missing.')

notes = root / 'docs/stages/stage-8/Stage8E-Package2-DeploymentRuntimeHotfix-v4.3.8-Notes.md'
if not notes.exists():
    errors.append('Deployment runtime hotfix stage notes are missing.')

if errors:
    print('Stage 8E Package 2 deployment runtime hotfix validation failed:')
    for error in errors:
        print(f'- {error}')
    raise SystemExit(1)

print('Stage 8E Package 2 deployment runtime hotfix static validation passed.')
