#!/usr/bin/env bash
# Creates ~/garmetix-link-env.sh for persistent Garmetix deploy env linking.
set -Eeuo pipefail

HELPER="${HOME}/garmetix-link-env.sh"

if [[ -f "$HELPER" ]]; then
  echo "$HELPER already exists."
  exit 0
fi

cat > "$HELPER" <<'EOS'
#!/usr/bin/env bash
set -Eeuo pipefail

APP_DIR="${1:-$HOME/GarmetixWebStarter}"
LOCAL_ENV="${HOME}/.garmetix/macmini.env"
REMOTE_USER="${GARMETIX_SERVER_USER:-amit}"
REMOTE_HOST="${GARMETIX_SERVER_HOST:-192.168.11.126}"
REMOTE_APP_DIR="${GARMETIX_REMOTE_APP_DIR:-/opt/garmetix}"

mkdir -p "$APP_DIR/deploy"

if [[ ! -f "$LOCAL_ENV" ]]; then
  echo "Missing: $LOCAL_ENV" >&2
  echo "Create it once:" >&2
  echo "  mkdir -p ~/.garmetix" >&2
  echo "  cp ${APP_DIR}/deploy/macmini.env ~/.garmetix/macmini.env" >&2
  echo "  chmod 600 ~/.garmetix/macmini.env" >&2
  exit 1
fi

ln -sfn "$LOCAL_ENV" "$APP_DIR/deploy/macmini.env"
echo "Linked $APP_DIR/deploy/macmini.env -> $LOCAL_ENV"

ssh "${REMOTE_USER}@${REMOTE_HOST}" "mkdir -p '${REMOTE_APP_DIR}/shared/env' && chmod 750 '${REMOTE_APP_DIR}/shared/env'"

ssh "${REMOTE_USER}@${REMOTE_HOST}" "
if [[ -f '${REMOTE_APP_DIR}/shared/env/.env.production' ]]; then
  if [[ -d '${REMOTE_APP_DIR}/current' ]]; then
    ln -sfn '${REMOTE_APP_DIR}/shared/env/.env.production' '${REMOTE_APP_DIR}/current/.env.production'
  fi
  chmod 600 '${REMOTE_APP_DIR}/shared/env/.env.production' 2>/dev/null || true
  echo 'Persistent production env is ready.'
else
  echo 'Missing ${REMOTE_APP_DIR}/shared/env/.env.production' >&2
  echo 'Copy your working .env.production there once, then deploy again.' >&2
  exit 1
fi
"

echo "Garmetix env links are ready."
EOS

chmod +x "$HELPER"
echo "Created $HELPER"
