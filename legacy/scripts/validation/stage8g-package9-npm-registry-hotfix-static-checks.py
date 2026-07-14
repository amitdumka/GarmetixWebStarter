from pathlib import Path
root = Path(__file__).resolve().parents[2]
lock = (root / 'frontend/garmetix-web/package-lock.json').read_text()
dockerfile = (root / 'frontend/garmetix-web/Dockerfile').read_text()
npmrc = (root / 'frontend/garmetix-web/.npmrc').read_text()
if 'packages.applied-caas' in lock or 'internal.api.openai.org' in lock:
    raise SystemExit('package-lock still contains internal registry URL')
if 'https://registry.npmjs.org/@iconify-json/lucide/-/lucide-1.2.113.tgz' not in lock:
    raise SystemExit('package-lock missing public npm lucide tarball URL')
if 'registry=https://registry.npmjs.org/' not in npmrc:
    raise SystemExit('frontend .npmrc missing public npm registry')
for token in ['npm config set registry https://registry.npmjs.org/', 'fetch-retries 5', 'npm ci --prefer-online --no-audit --no-fund']:
    if token not in dockerfile:
        raise SystemExit(f'Dockerfile missing npm hardening token: {token}')
print('Stage 8G Package 9 npm registry hotfix static validation passed.')
