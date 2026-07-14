#!/usr/bin/env bash
set -Eeuo pipefail

# Generic Garmetix Ubuntu deployment script.
# Usage examples:
#   ./deploy/deploy-to-ubuntu.sh -s amit@192.168.11.126 -t T1
#   ./deploy/deploy-to-ubuntu.sh -s amit@192.168.11.126 -tun ffff... -tok 'TOKEN'
# Config files:
#   ~/.garmetix/ubuntu.env
#   ~/.garmetix/.env.production

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"
CONFIG_DIR="${GARMETIX_CONFIG_DIR:-$HOME/.garmetix}"
UBUNTU_ENV="$CONFIG_DIR/ubuntu.env"
OLD_MACMINI_ENV="$CONFIG_DIR/macmini.env"
OLD_MACMINI_EVN="$CONFIG_DIR/macmini.evn"
PROD_ENV="$CONFIG_DIR/.env.production"
REMOTE_SPEC=""
REMOTE_ROOT_DEFAULT="/opt/garmetix"
REMOTE_ROOT=""
TUNNEL_ALIAS=""
TUNNEL_ID_OVERRIDE=""
TUNNEL_TOKEN_OVERRIDE=""
UPDATE_ENV=false
RESET_DB=false
SKIP_INSTALL=false
DRY_RUN=false
SAVE_PASSWORDS=true
PROMPT_PASSWORDS=true
SSH_PASSWORD_OVERRIDE=""
SUDO_PASSWORD_OVERRIDE=""

log(){ printf '\033[1;32m==>\033[0m %s\n' "$*"; }
warn(){ printf '\033[1;33mWARN:\033[0m %s\n' "$*" >&2; }
err(){ printf '\033[1;31mERROR:\033[0m %s\n' "$*" >&2; }
usage(){
  cat <<USAGE
Usage:
  $0 -s user@host [-t T1|T2] [-tun tunnel-id] [-tok tunnel-token] [options]

Required:
  -s, --server user@host       SSH target, e.g. amit@192.168.11.126

Tunnel selection:
  -t, --tunnel T1|T2           Use Cloudflare tunnel alias from ~/.garmetix/ubuntu.env
  -tun, --tunnel-id ID         Override tunnel ID
  -tok, --token TOKEN          Override tunnel token. Token only, not full docker command.

Options:
  -e, --env FILE               Ubuntu config file. Default: ~/.garmetix/ubuntu.env
  -p, --project-root DIR       Project root. Default: parent of deploy/ folder
  -r, --remote-root DIR        Remote root. Default: /opt/garmetix or REMOTE_ROOT in ubuntu.env
      --update-env             Replace remote /opt/garmetix/shared/env/.env.production
      --reset-db               Remove Garmetix Postgres volumes on remote before start
      --no-install             Skip Ubuntu Docker/rsync/sleep setup
      --ssh-password PASS       SSH login password. Saved to ubuntu.env unless --no-save-passwords
      --sudo-password PASS      Remote sudo password. Defaults to SSH password when blank
      --save-passwords         Save entered/overridden passwords to ~/.garmetix/ubuntu.env. Default
      --no-save-passwords      Do not write passwords to config file
      --no-password-prompt     Do not prompt; fail/fallback if password is missing
      --dry-run                Print actions only where possible
  -h, --help                   Show this help

Config files:
  ~/.garmetix/ubuntu.env       Generic deployment values and tunnel aliases
  ~/.garmetix/.env.production  Runtime app secrets copied to remote if missing or --update-env

Examples:
  $0 -s amit@192.168.11.126 -t T1
  $0 -s amit@192.168.11.126 -tun ffffabaf-a3b6-4682-b335-ceb5e8dc4c5e -tok 'PASTE_TOKEN'
  $0 -s amit@192.168.11.126 -t T1 --ssh-password 'LOGIN_PASS' --sudo-password 'SUDO_PASS'
USAGE
}

while [[ $# -gt 0 ]]; do
  case "$1" in
    -s|--server) REMOTE_SPEC="${2:-}"; shift 2 ;;
    -t|--tunnel) TUNNEL_ALIAS="${2:-}"; shift 2 ;;
    -tun|--tunnel-id) TUNNEL_ID_OVERRIDE="${2:-}"; shift 2 ;;
    -tok|--token) TUNNEL_TOKEN_OVERRIDE="${2:-}"; shift 2 ;;
    -e|--env) UBUNTU_ENV="${2:-}"; shift 2 ;;
    -p|--project-root) PROJECT_ROOT="${2:-}"; shift 2 ;;
    -r|--remote-root) REMOTE_ROOT="${2:-}"; shift 2 ;;
    --update-env) UPDATE_ENV=true; shift ;;
    --reset-db) RESET_DB=true; shift ;;
    --no-install) SKIP_INSTALL=true; shift ;;
    --ssh-password) SSH_PASSWORD_OVERRIDE="${2:-}"; shift 2 ;;
    --sudo-password) SUDO_PASSWORD_OVERRIDE="${2:-}"; shift 2 ;;
    --save-passwords) SAVE_PASSWORDS=true; shift ;;
    --no-save-passwords) SAVE_PASSWORDS=false; shift ;;
    --no-password-prompt) PROMPT_PASSWORDS=false; shift ;;
    --dry-run) DRY_RUN=true; shift ;;
    -h|--help) usage; exit 0 ;;
    *) err "Unknown argument: $1"; usage; exit 1 ;;
  esac
done

if [[ -z "$REMOTE_SPEC" ]]; then
  err "Missing -s user@host"
  usage
  exit 1
fi

mkdir -p "$CONFIG_DIR"
if [[ ! -f "$UBUNTU_ENV" && -f "$OLD_MACMINI_ENV" ]]; then
  warn "ubuntu.env not found. Copying old macmini.env to ubuntu.env."
  cp "$OLD_MACMINI_ENV" "$UBUNTU_ENV"
elif [[ ! -f "$UBUNTU_ENV" && -f "$OLD_MACMINI_EVN" ]]; then
  warn "ubuntu.env not found. Copying old macmini.evn to ubuntu.env."
  cp "$OLD_MACMINI_EVN" "$UBUNTU_ENV"
fi

if [[ ! -f "$UBUNTU_ENV" ]]; then
  err "Missing config: $UBUNTU_ENV"
  echo "Create it from: deploy/ubuntu.env.example"
  exit 1
fi
if [[ ! -f "$PROD_ENV" ]]; then
  err "Missing runtime env: $PROD_ENV"
  echo "Create it from: deploy/env.production.example"
  exit 1
fi

# shellcheck disable=SC1090
set -a
source "$UBUNTU_ENV"
set +a

# Keep local config private because it can store SSH/sudo passwords.
chmod 700 "$CONFIG_DIR" 2>/dev/null || true
chmod 600 "$UBUNTU_ENV" 2>/dev/null || true
chmod 600 "$PROD_ENV" 2>/dev/null || true

REMOTE_ROOT="${REMOTE_ROOT:-${REMOTE_ROOT_DEFAULT}}"
COMPOSE_PROJECT_NAME="${COMPOSE_PROJECT_NAME:-garmetix}"
SSH_OPTS="${SSH_OPTS:-}"
RSYNC_DELETE="${RSYNC_DELETE:-true}"
PUBLIC_DOMAIN="${PUBLIC_DOMAIN:-${CLOUDFLARE_HOSTNAME:-garmetix.aadwikafashion.in}}"
CLOUDFLARE_HOSTNAME="${CLOUDFLARE_HOSTNAME:-$PUBLIC_DOMAIN}"
CLOUDFLARE_SERVICE="${CLOUDFLARE_SERVICE:-http://web:3000}"
CLOUDFLARE_PROTOCOL="${CLOUDFLARE_PROTOCOL:-http2}"

get_var_first(){
  local name
  for name in "$@"; do
    if [[ -n "${!name-}" ]]; then
      printf '%s' "${!name}"
      return 0
    fi
  done
  return 1
}

config_upsert(){
  local file="$1" key="$2" value="$3"
  python3 - "$file" "$key" "$value" <<'PYCFGUPSERT'
from pathlib import Path
import sys
path=Path(sys.argv[1]); key=sys.argv[2]; val=sys.argv[3]
lines=path.read_text().splitlines() if path.exists() else []
out=[]; done=False
for line in lines:
    if line.startswith(key+'='):
        out.append(f'{key}={val}')
        done=True
    else:
        out.append(line)
if not done:
    out.append(f'{key}={val}')
path.write_text('\n'.join(out)+'\n')
PYCFGUPSERT
  chmod 600 "$file" 2>/dev/null || true
}

alias_number(){
  local a="${1^^}"
  case "$a" in
    T1|1|TUNNEL1|CLOUDFLARETUNNEL_1|CLOUDFLAIRTUNNEL_1|CLOUDFLAIRTIURNNEL_1) printf '1' ;;
    T2|2|TUNNEL2|CLOUDFLARETUNNEL_2|CLOUDFLAIRTUNNEL_2|CLOUDFLAIRTIURNNEL_2) printf '2' ;;
    *) printf '%s' "$a" ;;
  esac
}

TUNNEL_ID="${TUNNEL_ID_OVERRIDE:-}"
TUNNEL_TOKEN="${TUNNEL_TOKEN_OVERRIDE:-}"
if [[ -z "$TUNNEL_ID" || -z "$TUNNEL_TOKEN" ]]; then
  if [[ -n "$TUNNEL_ALIAS" ]]; then
    N="$(alias_number "$TUNNEL_ALIAS")"
    if [[ -z "$TUNNEL_ID" ]]; then
      TUNNEL_ID="$(get_var_first \
        "CLOUDFLARE_TUNNEL_${N}_ID" "CLOUDFLARE_TUNNEL_T${N}_ID" "CLOUDFLARE_T${N}_ID" \
        "CloudFlairTunnel_${N}_ID" "CloudFlairTiurnnel_${N}_ID" "CloudflareTunnel_${N}_ID" || true)"
    fi
    if [[ -z "$TUNNEL_TOKEN" ]]; then
      TUNNEL_TOKEN="$(get_var_first \
        "CLOUDFLARE_TUNNEL_${N}_TOKEN" "CLOUDFLARE_TUNNEL_T${N}_TOKEN" "CLOUDFLARE_T${N}_TOKEN" \
        "CloudFlairTunnel_${N}_TOKEN" "CloudFlairTiurnnel_${N}_TOKEN" "CloudflareTunnel_${N}_TOKEN" \
        "CloudFlairTunnel_${N}" "CloudFlairTiurnnel_${N}" "CloudflareTunnel_${N}" || true)"
    fi
  fi
fi

# Fallback to default variables from ubuntu.env
TUNNEL_ID="${TUNNEL_ID:-${CLOUDFLARE_TUNNEL_ID:-}}"
TUNNEL_TOKEN="${TUNNEL_TOKEN:-${CLOUDFLARE_TUNNEL_TOKEN:-}}"

fetch_tunnel_token_if_possible(){
  if [[ -n "$TUNNEL_TOKEN" || -z "$TUNNEL_ID" ]]; then return 0; fi
  if [[ -z "${CLOUDFLARE_API_TOKEN:-}" || -z "${CLOUDFLARE_ACCOUNT_ID:-}" ]]; then return 0; fi
  log "Fetching Cloudflare connector token for tunnel $TUNNEL_ID from Cloudflare API"
  local tmp response success token
  tmp="$(mktemp)"
  if ! curl -fsS -H "Authorization: Bearer ${CLOUDFLARE_API_TOKEN}" \
      "https://api.cloudflare.com/client/v4/accounts/${CLOUDFLARE_ACCOUNT_ID}/cfd_tunnel/${TUNNEL_ID}/token" -o "$tmp"; then
    warn "Could not fetch Cloudflare tunnel token from API. Provide -tok or set CLOUDFLARE_TUNNEL_TOKEN."
    rm -f "$tmp"
    return 0
  fi
  token="$(python3 - "$tmp" <<'PY'
import json, sys
p=sys.argv[1]
data=json.load(open(p))
res=data.get('result')
if isinstance(res, dict):
    print(res.get('token') or res.get('secret') or '')
elif isinstance(res, str):
    print(res)
else:
    print('')
PY
)"
  rm -f "$tmp"
  if [[ -n "$token" ]]; then
    TUNNEL_TOKEN="$token"
  fi
}
fetch_tunnel_token_if_possible

if [[ "${CLOUDFLARE_ENABLED:-true}" == "true" ]]; then
  if [[ -z "$TUNNEL_ID" ]]; then
    err "Cloudflare enabled but tunnel ID is empty. Use -tun or set CLOUDFLARE_TUNNEL_ID / CLOUDFLARE_TUNNEL_1_ID."
    exit 1
  fi
  if [[ -z "$TUNNEL_TOKEN" ]]; then
    err "Cloudflare enabled but tunnel token is empty. Use -tok or set CLOUDFLARE_TUNNEL_TOKEN / CLOUDFLARE_TUNNEL_1_TOKEN."
    exit 1
  fi
  if [[ "$TUNNEL_TOKEN" == *"cloudflared"* || "$TUNNEL_TOKEN" == *"--token"* || "$TUNNEL_TOKEN" == *"docker run"* ]]; then
    err "Tunnel token looks like a full command. Paste only the value after --token."
    exit 1
  fi
  if [[ "$TUNNEL_TOKEN" == *" "* ]]; then
    err "Tunnel token contains spaces. Paste only the token, no quotes/full command."
    exit 1
  fi
fi

upsert_env(){
  local file="$1" key="$2" value="$3"
  python3 - "$file" "$key" "$value" <<'PY'
from pathlib import Path
import sys
path=Path(sys.argv[1]); key=sys.argv[2]; val=sys.argv[3]
lines=path.read_text().splitlines() if path.exists() else []
out=[]; done=False
for line in lines:
    if line.startswith(key+'='):
        out.append(f'{key}={val}')
        done=True
    else:
        out.append(line)
if not done:
    out.append(f'{key}={val}')
path.write_text('\n'.join(out)+'\n')
PY
}

WORKDIR="$(mktemp -d)"
trap 'rm -rf "$WORKDIR"' EXIT
STAGED_ENV="$WORKDIR/.env.production"
STAGED_UBUNTU_ENV="$WORKDIR/ubuntu.env"
cp "$PROD_ENV" "$STAGED_ENV"
cp "$UBUNTU_ENV" "$STAGED_UBUNTU_ENV"

# Merge deployment values into runtime env file.
upsert_env "$STAGED_ENV" PUBLIC_DOMAIN "$PUBLIC_DOMAIN"
upsert_env "$STAGED_ENV" CLOUDFLARE_ENABLED "${CLOUDFLARE_ENABLED:-true}"
upsert_env "$STAGED_ENV" CLOUDFLARE_HOSTNAME "$CLOUDFLARE_HOSTNAME"
upsert_env "$STAGED_ENV" CLOUDFLARE_SERVICE "$CLOUDFLARE_SERVICE"
upsert_env "$STAGED_ENV" CLOUDFLARE_PROTOCOL "$CLOUDFLARE_PROTOCOL"
upsert_env "$STAGED_ENV" CLOUDFLARE_TUNNEL_ID "$TUNNEL_ID"
upsert_env "$STAGED_ENV" CLOUDFLARE_TUNNEL_TOKEN "$TUNNEL_TOKEN"
upsert_env "$STAGED_ENV" CLOUDFLARE_ACCOUNT_ID "${CLOUDFLARE_ACCOUNT_ID:-}"
upsert_env "$STAGED_ENV" CLOUDFLARE_ZONE_ID "${CLOUDFLARE_ZONE_ID:-}"
upsert_env "$STAGED_ENV" CLOUDFLARE_API_TOKEN "${CLOUDFLARE_API_TOKEN:-}"
if [[ "$RESET_DB" == "true" ]]; then
  upsert_env "$STAGED_ENV" RESET_DATABASE_ON_DEPLOY "true"
fi

# Keep the EF/Npgsql connection string aligned with the same Postgres password.
# PostgreSQL Docker volumes keep the first initialized password, so the remote runner
# also syncs the actual database role password before API starts.
env_value(){
  local key="$1"
  grep -E "^${key}=" "$STAGED_ENV" | tail -1 | cut -d= -f2- | sed -e 's/^"//' -e 's/"$//' -e "s/^'//" -e "s/'$//"
}
PG_DB_STAGED="$(env_value POSTGRES_DB)"; PG_DB_STAGED="${PG_DB_STAGED:-garmetix}"
PG_USER_STAGED="$(env_value POSTGRES_USER)"; PG_USER_STAGED="${PG_USER_STAGED:-garmetix}"
PG_PASS_STAGED="$(env_value POSTGRES_PASSWORD)"
PG_PORT_STAGED="$(env_value POSTGRES_PORT)"; PG_PORT_STAGED="${PG_PORT_STAGED:-5432}"
if [[ -n "$PG_PASS_STAGED" ]]; then
  if [[ "$PG_PASS_STAGED" == *";"* ]]; then
    warn "POSTGRES_PASSWORD contains semicolon; not auto-updating ConnectionStrings__Default. Avoid semicolon in this password."
  else
    upsert_env "$STAGED_ENV" ConnectionStrings__Default "Host=postgres;Port=${PG_PORT_STAGED};Database=${PG_DB_STAGED};Username=${PG_USER_STAGED};Password=${PG_PASS_STAGED}"
  fi
fi

# SSH/sudo password support. This is optional; SSH keys remain preferred.
# If password is entered or passed, it is saved to ubuntu.env by default so next deploy is non-interactive.
SSH_PASSWORD="${SSH_PASSWORD_OVERRIDE:-${SSH_PASSWORD:-${UBUNTU_SSH_PASSWORD:-}}}"
SUDO_PASSWORD="${SUDO_PASSWORD_OVERRIDE:-${SUDO_PASSWORD:-${UBUNTU_SUDO_PASSWORD:-}}}"
if [[ -z "$SUDO_PASSWORD" && -n "$SSH_PASSWORD" ]]; then
  SUDO_PASSWORD="$SSH_PASSWORD"
fi

need_sshpass=false
if [[ -n "$SSH_PASSWORD" ]]; then
  need_sshpass=true
elif [[ "$PROMPT_PASSWORDS" == "true" ]]; then
  if [[ ! -f "$HOME/.ssh/id_rsa" && ! -f "$HOME/.ssh/id_ed25519" ]]; then
    # Key may still exist under another name or agent; prompt only after a quick BatchMode test fails.
    if ! ssh $SSH_OPTS -o BatchMode=yes -o ConnectTimeout=5 "$REMOTE_SPEC" "true" >/dev/null 2>&1; then
      read -r -s -p "SSH password for $REMOTE_SPEC: " SSH_PASSWORD; echo
      if [[ -z "$SUDO_PASSWORD" ]]; then
        read -r -s -p "Remote sudo password for $REMOTE_SPEC [press Enter to use SSH password]: " SUDO_PASSWORD; echo
        SUDO_PASSWORD="${SUDO_PASSWORD:-$SSH_PASSWORD}"
      fi
      need_sshpass=true
    fi
  else
    if ! ssh $SSH_OPTS -o BatchMode=yes -o ConnectTimeout=5 "$REMOTE_SPEC" "true" >/dev/null 2>&1; then
      read -r -s -p "SSH password for $REMOTE_SPEC: " SSH_PASSWORD; echo
      if [[ -z "$SUDO_PASSWORD" ]]; then
        read -r -s -p "Remote sudo password for $REMOTE_SPEC [press Enter to use SSH password]: " SUDO_PASSWORD; echo
        SUDO_PASSWORD="${SUDO_PASSWORD:-$SSH_PASSWORD}"
      fi
      need_sshpass=true
    fi
  fi
fi

if [[ "$SAVE_PASSWORDS" == "true" ]]; then
  if [[ -n "$SSH_PASSWORD" ]]; then
    config_upsert "$UBUNTU_ENV" SSH_PASSWORD "$SSH_PASSWORD"
    config_upsert "$UBUNTU_ENV" UBUNTU_SSH_PASSWORD "$SSH_PASSWORD"
  fi
  if [[ -n "$SUDO_PASSWORD" ]]; then
    config_upsert "$UBUNTU_ENV" SUDO_PASSWORD "$SUDO_PASSWORD"
    config_upsert "$UBUNTU_ENV" UBUNTU_SUDO_PASSWORD "$SUDO_PASSWORD"
  fi
fi

if [[ -n "$SSH_PASSWORD" ]]; then
  if ! command -v sshpass >/dev/null 2>&1; then
    warn "SSH_PASSWORD is set, but sshpass is not installed on this client."
    warn "Install once on this WSL/client machine with: sudo apt-get install -y sshpass"
    warn "Until sshpass is installed, SSH login may still ask for the password."
  fi
fi

log "Deploy target: $REMOTE_SPEC"
log "Remote root: $REMOTE_ROOT"
log "Project root: $PROJECT_ROOT"
if [[ "${CLOUDFLARE_ENABLED:-true}" == "true" ]]; then
  log "Cloudflare hostname: $CLOUDFLARE_HOSTNAME"
  log "Cloudflare tunnel ID: $TUNNEL_ID"
fi

if [[ "$DRY_RUN" == "true" ]]; then
  warn "Dry run enabled; stopping before SSH/rsync."
  exit 0
fi

ssh_base(){
  if [[ -n "$SSH_PASSWORD" && -x "$(command -v sshpass 2>/dev/null || true)" ]]; then
    SSHPASS="$SSH_PASSWORD" sshpass -e ssh -o StrictHostKeyChecking=accept-new $SSH_OPTS "$REMOTE_SPEC" "$@"
  else
    ssh $SSH_OPTS "$REMOTE_SPEC" "$@"
  fi
}

ssh_plain(){ ssh_base "$@"; }

ssh_tty(){
  # Force a pseudo-terminal for commands that may need interactive fallback.
  if [[ -n "$SSH_PASSWORD" && -x "$(command -v sshpass 2>/dev/null || true)" ]]; then
    SSHPASS="$SSH_PASSWORD" sshpass -e ssh -tt -o StrictHostKeyChecking=accept-new $SSH_OPTS "$REMOTE_SPEC" "$@"
  else
    ssh $SSH_OPTS -tt "$REMOTE_SPEC" "$@"
  fi
}

rsync_remote(){
  local src="$1" dst="$2"; shift 2
  if [[ -n "$SSH_PASSWORD" && -x "$(command -v sshpass 2>/dev/null || true)" ]]; then
    SSHPASS="$SSH_PASSWORD" sshpass -e rsync "$@" -e "ssh -o StrictHostKeyChecking=accept-new $SSH_OPTS" "$src" "$dst"
  else
    rsync "$@" -e "ssh $SSH_OPTS" "$src" "$dst"
  fi
}

sudo_prefix(){
  if [[ -n "$SUDO_PASSWORD" ]]; then
    python3 - "$SUDO_PASSWORD" <<'PYSUDOPREFIX'
import shlex, sys
pw=sys.argv[1]
print("export GARMETIX_SUDO_PASSWORD=" + shlex.quote(pw) + "; ")
PYSUDOPREFIX
  fi
}

copy_and_run_remote_script_with_tty(){
  local local_script="$1"
  local remote_script="/tmp/garmetix-$(basename "$local_script")-$$"
  ssh_plain "cat > '$remote_script'" < "$local_script"
  local prefix
  prefix="$(sudo_prefix)"
  ssh_tty "${prefix}chmod +x '$remote_script' && bash '$remote_script'; rc=\$?; rm -f '$remote_script'; exit \$rc"
}

if [[ "$SKIP_INSTALL" != "true" ]]; then
  log "Preparing Ubuntu host: Docker, Compose plugin, rsync, sleep/network settings"
  copy_and_run_remote_script_with_tty "$SCRIPT_DIR/../scripts/install-ubuntu-host.sh"
  if [[ "${DISABLE_SERVER_SLEEP:-true}" == "true" ]]; then
    copy_and_run_remote_script_with_tty "$SCRIPT_DIR/../scripts/disable-server-sleep.sh" || true
  fi
fi

RELEASE_ID="$(date +%Y%m%d%H%M%S)"
REMOTE_RELEASE="$REMOTE_ROOT/releases/$RELEASE_ID"
REMOTE_SHARED_ENV="$REMOTE_ROOT/shared/env"

log "Creating remote release directories"
PREFIX="$(sudo_prefix)"
ssh_tty "${PREFIX}if [ -n \"\${GARMETIX_SUDO_PASSWORD:-}\" ]; then printf '%s\n' \"\$GARMETIX_SUDO_PASSWORD\" | sudo -S -p '' mkdir -p '$REMOTE_ROOT/releases' '$REMOTE_SHARED_ENV' '$REMOTE_ROOT/share/env' && printf '%s\n' \"\$GARMETIX_SUDO_PASSWORD\" | sudo -S -p '' chown -R \$USER: '$REMOTE_ROOT'; else sudo mkdir -p '$REMOTE_ROOT/releases' '$REMOTE_SHARED_ENV' '$REMOTE_ROOT/share/env' && sudo chown -R \$USER: '$REMOTE_ROOT'; fi && ln -sfn '$REMOTE_ROOT/shared/env' '$REMOTE_ROOT/share/env'"

log "Uploading project source to $REMOTE_RELEASE"
ssh_plain "mkdir -p '$REMOTE_RELEASE'"
RSYNC_DELETE_FLAG=""
if [[ "$RSYNC_DELETE" == "true" ]]; then RSYNC_DELETE_FLAG="--delete"; fi
rsync_remote "$PROJECT_ROOT/" "$REMOTE_SPEC:$REMOTE_RELEASE/" -az $RSYNC_DELETE_FLAG \
  --exclude '.git/' \
  --exclude '.vs/' \
  --exclude '.idea/' \
  --exclude 'node_modules/' \
  --exclude '**/node_modules/' \
  --exclude '**/bin/' \
  --exclude '**/obj/' \
  --exclude '**/.nuxt/' \
  --exclude '**/.output/' \
  --exclude '.env' \
  --exclude '.env.*' \
  --exclude 'deploy/macmini.env' \
  --exclude 'deploy/ubuntu.env' 

# Ensure compose templates from this kit exist in release, even if project did not contain them.
ssh_plain "mkdir -p '$REMOTE_RELEASE/deploy' '$REMOTE_RELEASE/scripts'"
rsync_remote "$SCRIPT_DIR/docker-compose.prod.yml" "$REMOTE_SPEC:$REMOTE_RELEASE/deploy/docker-compose.prod.yml" -az
rsync_remote "$SCRIPT_DIR/docker-compose.cloudflare.yml" "$REMOTE_SPEC:$REMOTE_RELEASE/deploy/docker-compose.cloudflare.yml" -az
rsync_remote "$SCRIPT_DIR/../scripts/" "$REMOTE_SPEC:$REMOTE_RELEASE/scripts/" -az

log "Uploading/copying environment files"
REMOTE_ENV_EXISTS="$(ssh_plain "test -f '$REMOTE_SHARED_ENV/.env.production' && echo yes || echo no")"
if [[ "$REMOTE_ENV_EXISTS" != "yes" || "$UPDATE_ENV" == "true" ]]; then
  rsync_remote "$STAGED_ENV" "$REMOTE_SPEC:$REMOTE_SHARED_ENV/.env.production" -az
  log "Uploaded .env.production to $REMOTE_SHARED_ENV/.env.production"
else
  log "Remote .env.production exists; not overwritten. Use --update-env to replace it."
fi
rsync_remote "$STAGED_UBUNTU_ENV" "$REMOTE_SPEC:$REMOTE_SHARED_ENV/ubuntu.env" -az
ssh_plain "ln -sfn '$REMOTE_SHARED_ENV/.env.production' '$REMOTE_RELEASE/.env.production' && ln -sfn '$REMOTE_RELEASE' '$REMOTE_ROOT/current'"

log "Configuring Cloudflare DNS/tunnel route if API details are present"
if [[ "${CLOUDFLARE_ENABLED:-true}" == "true" && -n "${CLOUDFLARE_API_TOKEN:-}" && -n "${CLOUDFLARE_ZONE_ID:-}" ]]; then
  python3 "$SCRIPT_DIR/lib/cloudflare-configure.py" \
    --api-token "${CLOUDFLARE_API_TOKEN}" \
    --zone-id "${CLOUDFLARE_ZONE_ID}" \
    --account-id "${CLOUDFLARE_ACCOUNT_ID:-}" \
    --hostname "$CLOUDFLARE_HOSTNAME" \
    --tunnel-id "$TUNNEL_ID" \
    --service "$CLOUDFLARE_SERVICE" || warn "Cloudflare API configuration failed. Check manually in Cloudflare website."
else
  warn "Skipping Cloudflare API configuration; API token or zone ID not present. Configure DNS/Public Hostname in Cloudflare website."
fi

log "Starting application on remote host"
PREFIX="$(sudo_prefix)"
ssh_plain "${PREFIX}REMOTE_ROOT='$REMOTE_ROOT' RELEASE='$REMOTE_RELEASE' COMPOSE_PROJECT_NAME='$COMPOSE_PROJECT_NAME' RESET_DB='$RESET_DB' bash '$REMOTE_RELEASE/scripts/remote-run-garmetix.sh'"

log "Deployment complete"
echo "Local web URL on server: http://127.0.0.1:${WEB_PORT:-3000}"
echo "Local API URL on server: http://127.0.0.1:${API_PORT:-5080}/api/health"
echo "Public URL: https://${CLOUDFLARE_HOSTNAME}"
