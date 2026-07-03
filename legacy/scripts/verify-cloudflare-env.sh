#!/usr/bin/env bash
set -Eeuo pipefail
ENV_FILE="${1:-/opt/garmetix/current/.env.production}"
if [[ ! -f "$ENV_FILE" ]]; then
  echo "Missing env file: $ENV_FILE" >&2
  exit 1
fi
python3 - "$ENV_FILE" <<'PY'
from pathlib import Path
import sys
p=Path(sys.argv[1])
env={}
for line in p.read_text().splitlines():
    line=line.strip()
    if not line or line.startswith('#') or '=' not in line: continue
    k,v=line.split('=',1)
    env[k]=v.strip().strip('"').strip("'")
tok=env.get('CLOUDFLARE_TUNNEL_TOKEN','')
print('CLOUDFLARE_TUNNEL_ID:', env.get('CLOUDFLARE_TUNNEL_ID',''))
print('CLOUDFLARE_HOSTNAME:', env.get('CLOUDFLARE_HOSTNAME',''))
print('Token exists:', bool(tok))
print('Token length:', len(tok))
print('Token has spaces:', ' ' in tok)
print('Token is full command:', any(x in tok for x in ['cloudflared','--token','docker run']))
print('Token is literal variable:', '${CLOUDFLARE_TUNNEL_TOKEN}' in tok)
PY
