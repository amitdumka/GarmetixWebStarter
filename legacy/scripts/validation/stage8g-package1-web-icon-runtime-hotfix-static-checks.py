#!/usr/bin/env python3
from pathlib import Path
import json

root = Path(__file__).resolve().parents[2]

package_json = root / 'frontend/garmetix-web/package.json'
dockerfile = root / 'frontend/garmetix-web/Dockerfile'
icon_route = root / 'frontend/garmetix-web/server/api/_nuxt_icon/lucide.json.get.ts'
notes = root / 'docs/stages/stage-8/Stage8G-Package1-WebIconRuntimeHotfix-v4.6.0-Notes.md'

errors = []

pkg = json.loads(package_json.read_text())
if '@iconify-json/lucide' not in pkg.get('dependencies', {}):
    errors.append('frontend package.json must include @iconify-json/lucide dependency')

docker_text = dockerfile.read_text()
if 'COPY --from=build /app/node_modules/@iconify-json ./node_modules/@iconify-json' not in docker_text:
    errors.append('frontend Dockerfile must copy @iconify-json from the build layer')
if 'npm install --no-save @iconify-json/lucide' in docker_text:
    errors.append('frontend Dockerfile must not perform runtime network npm install for iconify')

route_text = icon_route.read_text() if icon_route.exists() else ''
if 'createRequire' not in route_text or 'readFileSync' not in route_text or '@iconify-json/lucide/icons.json' not in route_text:
    errors.append('custom lucide icon endpoint must use fs/createRequire to avoid JSON import attributes')
if 'defineEventHandler' not in route_text or 'getQuery' not in route_text:
    errors.append('custom lucide icon endpoint must be a Nuxt/Nitro GET handler with query filtering')

if not notes.exists():
    errors.append('web icon runtime hotfix notes are missing')

if errors:
    for error in errors:
        print(f'ERROR: {error}')
    raise SystemExit(1)

print('Stage 8G Package 1 web icon runtime hotfix static validation passed.')
