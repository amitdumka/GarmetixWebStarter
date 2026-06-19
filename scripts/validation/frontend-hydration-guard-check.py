#!/usr/bin/env python3
from __future__ import annotations

import re
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
errors: list[str] = []
app_vue = (ROOT / 'frontend/garmetix-web/app.vue').read_text(encoding='utf-8')
main_css = (ROOT / 'frontend/garmetix-web/assets/css/main.css').read_text(encoding='utf-8')
auth = (ROOT / 'frontend/garmetix-web/composables/useAuth.ts').read_text(encoding='utf-8')
workspace = (ROOT / 'frontend/garmetix-web/composables/useWorkspace.ts').read_text(encoding='utf-8')

checks = {
    'app.vue uses ClientOnly': '<ClientOnly>' in app_vue and '<NuxtPage v-if="hydrated"' in app_vue,
    'app.vue restores auth on mounted': 'onMounted' in app_vue and 'useAuth().restore()' in app_vue,
    'fallback boot shell exists': 'garmetix-client-boot' in app_vue and 'role="status"' in app_vue,
    'boot shell css exists': '.garmetix-client-boot' in main_css,
    'auth restore remains client-only': 'if (!import.meta.client)' in auth and 'localStorage.getItem' in auth,
    'workspace storage remains client-only': workspace.count('if (!import.meta.client') >= 3 and 'localStorage' in workspace,
}

for label, ok in checks.items():
    if not ok:
        errors.append(label)

# Lightweight guard: storage access must only live in the auth/workspace composables where
# explicit import.meta.client branches above protect SSR execution.
for rel in ['frontend/garmetix-web/composables/useAuth.ts', 'frontend/garmetix-web/composables/useWorkspace.ts']:
    text = (ROOT / rel).read_text(encoding='utf-8')
    if 'localStorage' in text and 'import.meta.client' not in text:
        errors.append(f'missing import.meta.client guard in {rel}')

if errors:
    print('Frontend hydration guard check failed:', file=sys.stderr)
    for error in errors:
        print(f' - {error}', file=sys.stderr)
    sys.exit(1)

print('Frontend hydration guard check passed.')
