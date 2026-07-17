#!/usr/bin/env bash
set -euo pipefail

# Deploys the Swalekha (Personal & Personal Finance) module onto an SRP host that already has
# the rest of Garmetix installed and running (via srp-whole-site-deploy.sh). This is deliberately
# NOT a whole-site redeploy: it does not require a local build of main/pos/hr/books/etc. Instead
# it copies the currently-live release forward on the remote host and layers three things on top
# of it, atomically, via the same symlink-swap release model srp-whole-site-deploy.sh uses:
#   1. a fresh build of the swalekha-web static app
#   2. a fresh publish of the shared Garmetix.Api backend (Swalekha's endpoints live in the same
#      process as every other app - there is no separate "Swalekha backend" to deploy)
#   3. the swalekha_db database + its ConnectionStrings__Swalekha entry in the API env file,
#      created/wired automatically if not already present
# It also patches the single `swalekhaUrl` runtime-config value into every already-deployed app's
# compiled HTML, in place, so the Owner-only profile-menu link lights up without rebuilding those
# apps from source.
#
# Same as every other deploy in this project: reads config from
#   $GARMETIX_SRP_DEPLOY_CONFIG, or ~/.config/garmetix/srp-deploy.env
# and secrets from
#   $SRP_SECRETS_PATH, or ~/.config/garmetix/srp-deploy.secrets.env
#
# On Windows/Git Bash, disable MSYS path conversion - see srp-whole-site-deploy.sh for why.
export MSYS_NO_PATHCONV=1

usage() {
  cat <<'USAGE'
Garmetix SRP Swalekha module deploy

Usage:
  bash frontend/modular/deploy/srp-swalekha-deploy.sh --dry-run
  bash frontend/modular/deploy/srp-swalekha-deploy.sh --build-only
  bash frontend/modular/deploy/srp-swalekha-deploy.sh --stage=SwalekhaModuleGoLive
  bash frontend/modular/deploy/srp-swalekha-deploy.sh --stage=SwalekhaModuleGoLive --install-remote

Flags:
  --stage=Name         Required for real deploys (not --dry-run/--build-only). Used in the
                        mandatory pre-deploy database backup filename/history entry.
  --skip-build          Reuse the existing local frontend/modular/apps/swalekha/.output build.
  --skip-api            Do not republish the backend API - pull the currently-live api/ forward
                        from the remote host instead. Only safe if the remote API binary was
                        already built from a commit that includes the Swalekha endpoints.
  --skip-db-setup       Do not create swalekha_db or write ConnectionStrings__Swalekha. Use this
                        if you have already provisioned the database/connection string yourself.
  --skip-db-backup      Emergency/manual override only. Skips the mandatory pre-deploy database
                        backup (covers both the main database and swalekha_db, once configured).
  --build-only          Build and stage the release locally, skip everything remote.
  --install-remote      After uploading, install/refresh the Nginx location and the API systemd
                        unit on the host and restart the API service. Without this flag the new
                        release is uploaded and made `current`, but Nginx/systemd are left as-is
                        (existing routes keep working; /swalekha/ will 404 until you install).
  --dry-run              Print the plan and exit without touching anything local or remote.

Reads config from:
  $GARMETIX_SRP_DEPLOY_CONFIG, or ~/.config/garmetix/srp-deploy.env

Optional private secrets file:
  $SRP_SECRETS_PATH, or ~/.config/garmetix/srp-deploy.secrets.env

What this script assumes is already true on the target host:
  - Garmetix is already installed and running there via srp-whole-site-deploy.sh
    (Nginx serving $SRP_REMOTE_BASE/current/web-root, garmetix-srp-api.service running the
    shared API, a Postgres server reachable with the credentials in $SRP_API_ENV_PATH's
    ConnectionStrings__Default).
  - That same Postgres server/user can also host a second, separate swalekha_db database
    (the module's designed isolation boundary - see docs/personal-finance-module-design.md).
USAGE
}

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../../.." && pwd)"
MODULAR_ROOT="$REPO_ROOT/frontend/modular"
DEFAULT_CONFIG_PATH="$HOME/.config/garmetix/srp-deploy.env"
CONFIG_PATH="${GARMETIX_SRP_DEPLOY_CONFIG:-$DEFAULT_CONFIG_PATH}"

detect_windows_config_path() {
  case "$REPO_ROOT" in
    /mnt/?/Users/*/*)
      local drive user
      drive="$(printf '%s' "$REPO_ROOT" | cut -d/ -f3)"
      user="$(printf '%s' "$REPO_ROOT" | cut -d/ -f5)"
      printf '/mnt/%s/Users/%s/.config/garmetix/srp-deploy.env' "$drive" "$user"
      ;;
    /?/Users/*/*)
      local drive user
      drive="$(printf '%s' "$REPO_ROOT" | cut -d/ -f2)"
      user="$(printf '%s' "$REPO_ROOT" | cut -d/ -f4)"
      printf '/%s/Users/%s/.config/garmetix/srp-deploy.env' "$drive" "$user"
      ;;
  esac
}

if [ -z "${GARMETIX_SRP_DEPLOY_CONFIG:-}" ] && [ ! -f "$CONFIG_PATH" ]; then
  DETECTED_CONFIG_PATH="$(detect_windows_config_path || true)"
  if [ -n "${DETECTED_CONFIG_PATH:-}" ] && [ -f "$DETECTED_CONFIG_PATH" ]; then
    CONFIG_PATH="$DETECTED_CONFIG_PATH"
  fi
fi

DRY_RUN=false
BUILD_ONLY=false
INSTALL_REMOTE=false
SKIP_BUILD=false
SKIP_API=false
SKIP_DB_SETUP=false
SKIP_DB_BACKUP=false
SRP_DEPLOY_STAGE="${SRP_DEPLOY_STAGE:-${GARMETIX_BACKUP_STAGE:-}}"

for arg in "$@"; do
  case "$arg" in
    --dry-run) DRY_RUN=true ;;
    --build-only) BUILD_ONLY=true ;;
    --install-remote) INSTALL_REMOTE=true ;;
    --skip-build) SKIP_BUILD=true ;;
    --skip-api) SKIP_API=true ;;
    --skip-db-setup) SKIP_DB_SETUP=true ;;
    --skip-db-backup) SKIP_DB_BACKUP=true ;;
    --stage=*) SRP_DEPLOY_STAGE="${arg#--stage=}" ;;
    -h|--help) usage; exit 0 ;;
    *) echo "Unknown argument: $arg" >&2; usage; exit 1 ;;
  esac
done

if [ -f "$CONFIG_PATH" ]; then
  # shellcheck disable=SC1090
  source "$CONFIG_PATH"
else
  echo "Config not found at $CONFIG_PATH; using safe defaults. Run srp-whole-site-deploy.sh --init-config to create it."
fi

SRP_SECRETS_PATH="${SRP_SECRETS_PATH:-$HOME/.config/garmetix/srp-deploy.secrets.env}"
RAW_SRP_SECRETS_PATH="$SRP_SECRETS_PATH"
if [[ "$SRP_SECRETS_PATH" == "~/"* ]]; then
  SRP_SECRETS_PATH="$HOME/${SRP_SECRETS_PATH#"~/"}"
fi
CONFIG_DIR="$(dirname "$CONFIG_PATH")"
CONFIG_SIDE_SECRETS="$CONFIG_DIR/srp-deploy.secrets.env"
if [ -f "$CONFIG_SIDE_SECRETS" ]; then
  if [[ "$RAW_SRP_SECRETS_PATH" == "~/"* ]] || [[ "$SRP_SECRETS_PATH" == "$HOME/.config/garmetix/srp-deploy.secrets.env" ]]; then
    SRP_SECRETS_PATH="$CONFIG_SIDE_SECRETS"
  fi
fi
if [ -f "$SRP_SECRETS_PATH" ]; then
  # shellcheck disable=SC1090
  source "$SRP_SECRETS_PATH"
fi

SRP_DEPLOY_TARGET="${SRP_DEPLOY_TARGET:-amitkumar@192.168.11.127}"
SRP_SSH_PORT="${SRP_SSH_PORT:-22}"
SRP_DOMAIN="${SRP_DOMAIN:-srp.aadwikafashion.in}"
SRP_REMOTE_BASE="${SRP_REMOTE_BASE:-/opt/garmetix-srp}"
SRP_NGINX_PORT="${SRP_NGINX_PORT:-8088}"
SRP_API_PORT="${SRP_API_PORT:-5080}"
SRP_KEEP_RELEASES="${SRP_KEEP_RELEASES:-5}"
SRP_API_PROJECT="${SRP_API_PROJECT:-backend/Garmetix.Api/Garmetix.Api.csproj}"
if [ ! -f "$REPO_ROOT/$SRP_API_PROJECT" ] && [[ "$SRP_API_PROJECT" == legacy/backend/* ]]; then
  MIGRATED_API_PROJECT="${SRP_API_PROJECT#legacy/}"
  if [ -f "$REPO_ROOT/$MIGRATED_API_PROJECT" ]; then
    SRP_API_PROJECT="$MIGRATED_API_PROJECT"
  fi
fi
SRP_API_PUBLISH_SELF_CONTAINED="${SRP_API_PUBLISH_SELF_CONTAINED:-true}"
SRP_API_RUNTIME="${SRP_API_RUNTIME:-linux-x64}"
SRP_API_ENV_PATH="${SRP_API_ENV_PATH:-/etc/garmetix/srp-api.env}"
SRP_BACKUP_DIR="${SRP_BACKUP_DIR:-/opt/garmetix/backup/database}"

# Swalekha-specific. SRP_SWALEKHA_BASE_PATH/SRP_SWALEKHA_URL match the exact defaults
# srp-whole-site-deploy.sh already uses for this module - keep them in sync if either changes.
SRP_SWALEKHA_BASE_PATH="${SRP_SWALEKHA_BASE_PATH:-/swalekha/}"
SRP_PUBLIC_API_BASE_URL="${SRP_PUBLIC_API_BASE_URL:-/api}"
SRP_SWALEKHA_URL="${SRP_SWALEKHA_URL:-$SRP_SWALEKHA_BASE_PATH}"
# The database name Swalekha's own database uses. Matches the local dev default in
# backend/Garmetix.Api/appsettings.json's ConnectionStrings:Swalekha.
SRP_SWALEKHA_DB_NAME="${SRP_SWALEKHA_DB_NAME:-swalekha_db}"

RELEASE_ID="$(date -u +%Y%m%d%H%M%S)"
LOCAL_OVERLAY="$REPO_ROOT/outputs/deploy/srp-swalekha/releases/$RELEASE_ID"
REMOTE_RELEASE="$SRP_REMOTE_BASE/releases/$RELEASE_ID"

print_plan() {
  cat <<PLAN
Swalekha SRP deploy plan
  Target:            $SRP_DEPLOY_TARGET
  Public site:        https://$SRP_DOMAIN$SRP_SWALEKHA_BASE_PATH
  Remote base:        $SRP_REMOTE_BASE
  Nginx port:         $SRP_NGINX_PORT
  API port:           $SRP_API_PORT
  Swalekha DB name:   $SRP_SWALEKHA_DB_NAME
  Config file:        $CONFIG_PATH
  Secrets file:        $SRP_SECRETS_PATH
  Stage:              ${SRP_DEPLOY_STAGE:-not set}
  DB backup:          $(if [ "$SKIP_DB_BACKUP" = true ]; then echo "skipped by flag"; elif [ "$BUILD_ONLY" = true ] || [ "$DRY_RUN" = true ]; then echo "not needed for this mode"; else echo "required before install"; fi)
  DB/env setup:       $(if [ "$SKIP_DB_SETUP" = true ]; then echo "skipped by flag - you must provision swalekha_db + ConnectionStrings__Swalekha yourself"; else echo "create swalekha_db and ConnectionStrings__Swalekha if missing"; fi)
  Install remote:     $INSTALL_REMOTE
  Local overlay:      $LOCAL_OVERLAY

This deploy layers the swalekha-web build + a fresh API publish on top of whatever is already
live under $SRP_REMOTE_BASE/current - it does not require a local build of any other app.
PLAN
}

need_command() {
  if ! command -v "$1" >/dev/null 2>&1; then
    echo "Missing required command: $1" >&2
    exit 1
  fi
}

resolve_command() {
  local command_name="$1"
  local fallback_name="${2:-}"
  if command -v "$command_name" >/dev/null 2>&1; then
    command -v "$command_name"
    return 0
  fi
  if [ -n "$fallback_name" ] && command -v "$fallback_name" >/dev/null 2>&1; then
    command -v "$fallback_name"
    return 0
  fi
  return 1
}

dotnet_path_arg() {
  # See srp-whole-site-deploy.sh's dotnet_path_arg for why this exists (Git Bash/MSYS argv
  # path corruption for native-exe arguments) - duplicated here rather than sourced so this
  # script stays runnable standalone.
  local path_value="$1"
  if [ -n "${WSL_DISTRO_NAME:-}${WSL_INTEROP:-}" ] && command -v wslpath >/dev/null 2>&1; then
    wslpath -w "$path_value"
  elif command -v cygpath >/dev/null 2>&1; then
    cygpath -w "$path_value"
  else
    printf '%s' "$path_value"
  fi
}

shell_quote() {
  printf "%q" "$1"
}

ssh_cmd() {
  if [ -n "${SRP_SSH_PASSWORD:-}" ]; then
    need_command sshpass
    SSHPASS="$SRP_SSH_PASSWORD" sshpass -e ssh -p "$SRP_SSH_PORT" -o StrictHostKeyChecking=accept-new "$SRP_DEPLOY_TARGET" "$@"
  else
    ssh -p "$SRP_SSH_PORT" -o StrictHostKeyChecking=accept-new "$SRP_DEPLOY_TARGET" "$@"
  fi
}

remote_sudo_env_prefix() {
  local sudo_password="${SRP_SUDO_PASSWORD:-${SRP_SSH_PASSWORD:-}}"
  if [ -n "$sudo_password" ]; then
    printf "export SRP_REMOTE_SUDO_PASSWORD=%s; " "$(shell_quote "$sudo_password")"
  fi
}

remote_sudo_function() {
  cat <<'REMOTE_SUDO'
sudo_cmd() {
  if [ -n "${SRP_REMOTE_SUDO_PASSWORD:-}" ]; then
    printf '%s\n' "$SRP_REMOTE_SUDO_PASSWORD" | sudo -S -p '' "$@"
  else
    sudo "$@"
  fi
};
REMOTE_SUDO
}

upload_dir() {
  # Uploads $1 (local dir) into $2 (remote dir, must already exist), deleting anything in the
  # remote dir that isn't in the local one - i.e. the remote dir ends up an exact mirror of the
  # local one. Falls back to a tar stream when rsync isn't available on either end (this has
  # genuinely happened on the SRP host before - see srp-whole-site-deploy.sh's upload_payload).
  local local_dir="$1"
  local remote_dir="$2"
  if command -v rsync >/dev/null 2>&1; then
    if [ -n "${SRP_SSH_PASSWORD:-}" ]; then
      need_command sshpass
      SSHPASS="$SRP_SSH_PASSWORD" sshpass -e rsync -az --delete -e "ssh -p $SRP_SSH_PORT -o StrictHostKeyChecking=accept-new" "$local_dir/" "$SRP_DEPLOY_TARGET:$remote_dir/"
    else
      rsync -az --delete -e "ssh -p $SRP_SSH_PORT -o StrictHostKeyChecking=accept-new" "$local_dir/" "$SRP_DEPLOY_TARGET:$remote_dir/"
    fi
    return
  fi
  need_command tar
  echo "rsync not found; using tar stream upload for $remote_dir."
  (cd "$local_dir" && tar -czf - .) | ssh_cmd "rm -rf '$remote_dir' && mkdir -p '$remote_dir' && tar -xzf - -C '$remote_dir'"
  # See srp-whole-site-deploy.sh's upload_payload for why the tar path needs this: Git
  # Bash/NTFS tar streams can lose the executable bit on the way over.
  ssh_cmd "chmod +x '$remote_dir/Garmetix.Api' 2>/dev/null || true"
}

build_swalekha_app() {
  local dest="$MODULAR_ROOT/apps/swalekha/.output/public"
  local index_file="$dest/index.html"

  if [ "$SKIP_BUILD" = true ]; then
    local existing_size
    existing_size=$(wc -c < "$index_file" 2>/dev/null || echo 0)
    if [ "${existing_size:-0}" -le 200 ]; then
      echo "ERROR: --skip-build was given but no valid existing build was found at $index_file." >&2
      exit 1
    fi
    echo "Skipping build (--skip-build); reusing existing local swalekha-web build output."
    mkdir -p "$LOCAL_OVERLAY/web-root/swalekha"
    cp -a "$dest/." "$LOCAL_OVERLAY/web-root/swalekha/"
    return
  fi

  echo "Building swalekha-web with base path $SRP_SWALEKHA_BASE_PATH"
  (cd "$MODULAR_ROOT" && "$NPM_COMMAND" run workspace-links -- --repair)
  rm -rf "$MODULAR_ROOT/node_modules/.cache/nuxt"

  local attempt=1
  local max_attempts=3
  local build_ok=false
  while [ "$attempt" -le "$max_attempts" ]; do
    rm -rf "$MODULAR_ROOT/apps/swalekha/.output"
    if (
      cd "$MODULAR_ROOT"
      GARMETIX_NUXT_BASE_URL="$SRP_SWALEKHA_BASE_PATH" \
      NUXT_PUBLIC_GARMETIX_API_BASE_URL="$SRP_PUBLIC_API_BASE_URL" \
      "$NPM_COMMAND" run build:swalekha
    ); then
      local index_size
      index_size=$(wc -c < "$index_file" 2>/dev/null || echo 0)
      if [ "${index_size:-0}" -gt 200 ]; then
        build_ok=true
        break
      fi
      echo "Build produced degenerate output (${index_size:-0} bytes for index.html) on attempt $attempt/$max_attempts; retrying without clearing the Nuxt build cache."
    else
      echo "Build command failed on attempt $attempt/$max_attempts; retrying."
    fi
    attempt=$((attempt + 1))
  done

  if [ "$build_ok" != true ]; then
    echo "ERROR: swalekha-web build did not produce valid static output after $max_attempts attempts. Refusing to deploy a broken release." >&2
    exit 1
  fi

  mkdir -p "$LOCAL_OVERLAY/web-root/swalekha"
  cp -a "$dest/." "$LOCAL_OVERLAY/web-root/swalekha/"
}

publish_api_overlay() {
  if [ "$SKIP_API" = true ]; then
    echo "Skipping API publish (--skip-api) - the currently-live api/ will be copied forward as part of the release-forward step."
    return
  fi
  local project_path output_path
  project_path="$(dotnet_path_arg "$REPO_ROOT/$SRP_API_PROJECT")"
  output_path="$(dotnet_path_arg "$LOCAL_OVERLAY/api")"
  echo "Publishing Garmetix.Api ($SRP_API_RUNTIME, self-contained=$SRP_API_PUBLISH_SELF_CONTAINED)"
  if [ "$SRP_API_PUBLISH_SELF_CONTAINED" = true ]; then
    "$DOTNET_COMMAND" publish "$project_path" -c Release -r "$SRP_API_RUNTIME" --self-contained true -o "$output_path"
  else
    "$DOTNET_COMMAND" publish "$project_path" -c Release -o "$output_path"
  fi
}

write_ops_templates() {
  # Same Nginx/systemd shape srp-whole-site-deploy.sh's write_templates() installs - duplicated
  # here (not sourced) so this script stays runnable standalone. Keep the route list and paths
  # in sync with that script if either changes.
  mkdir -p "$LOCAL_OVERLAY/ops"
  local api_exec_start
  if [ "$SRP_API_PUBLISH_SELF_CONTAINED" = true ]; then
    api_exec_start="$SRP_REMOTE_BASE/current/api/Garmetix.Api"
  else
    api_exec_start="/usr/bin/dotnet $SRP_REMOTE_BASE/current/api/Garmetix.Api.dll"
  fi

  cat > "$LOCAL_OVERLAY/ops/nginx-garmetix-srp.conf" <<NGINX
server {
    listen $SRP_NGINX_PORT;
    server_name $SRP_DOMAIN;
    absolute_redirect off;
    port_in_redirect off;
    server_name_in_redirect off;

    root $SRP_REMOTE_BASE/current/web-root;
    index index.html;

    client_max_body_size 100m;

    location /api/ {
        proxy_pass http://127.0.0.1:$SRP_API_PORT/api/;
        proxy_http_version 1.1;
        proxy_set_header Host \$host;
        proxy_set_header X-Real-IP \$remote_addr;
        proxy_set_header X-Forwarded-For \$proxy_add_x_forwarded_for;
        proxy_set_header X-Forwarded-Proto https;
        proxy_read_timeout 300s;
    }

    rewrite ^/(pos|hr|ai-sense|books|crm|admin|inventory|final-accounts|swalekha)/(.+)/$ /\$1/\$2 permanent;

    location /pos/ {
        try_files \$uri \$uri/index.html \$uri/ /pos/index.html;
    }

    location /hr/ {
        try_files \$uri \$uri/index.html \$uri/ /hr/index.html;
    }

    location /ai-sense/ {
        try_files \$uri \$uri/index.html \$uri/ /ai-sense/index.html;
    }

    location /books/ {
        try_files \$uri \$uri/index.html \$uri/ /books/index.html;
    }

    location /crm/ {
        try_files \$uri \$uri/index.html \$uri/ /crm/index.html;
    }

    location /admin/ {
        try_files \$uri \$uri/index.html \$uri/ /admin/index.html;
    }

    location /inventory/ {
        try_files \$uri \$uri/index.html \$uri/ /inventory/index.html;
    }

    location /final-accounts/ {
        try_files \$uri \$uri/index.html \$uri/ /final-accounts/index.html;
    }

    location /swalekha/ {
        try_files \$uri \$uri/index.html \$uri/ /swalekha/index.html;
    }

    location / {
        try_files \$uri \$uri/ /index.html;
    }
}
NGINX

  cat > "$LOCAL_OVERLAY/ops/garmetix-srp-api.service" <<SERVICE
[Unit]
Description=Garmetix SRP ASP.NET Core API
After=network.target

[Service]
WorkingDirectory=$SRP_REMOTE_BASE/current/api
ExecStart=$api_exec_start
Restart=always
RestartSec=10
KillSignal=SIGINT
SyslogIdentifier=garmetix-srp-api
User=www-data
Environment=ASPNETCORE_ENVIRONMENT=Production
Environment=ASPNETCORE_URLS=http://127.0.0.1:$SRP_API_PORT
Environment=ASPNETCORE_FORWARDEDHEADERS_ENABLED=true
EnvironmentFile=-$SRP_API_ENV_PATH
Environment=ApiDocs__Enabled=true

[Install]
WantedBy=multi-user.target
SERVICE

  cat > "$LOCAL_OVERLAY/ops/install-swalekha-on-host.sh" <<INSTALL
#!/usr/bin/env bash
set -euo pipefail

REMOTE_BASE="$SRP_REMOTE_BASE"

sudo_cmd() {
  if [ -n "\${SRP_REMOTE_SUDO_PASSWORD:-}" ]; then
    printf '%s\n' "\$SRP_REMOTE_SUDO_PASSWORD" | sudo -S -p '' "\$@"
  else
    sudo "\$@"
  fi
}

sudo_cmd cp "\$REMOTE_BASE/current/ops/nginx-garmetix-srp.conf" /etc/nginx/sites-available/garmetix-srp.conf
sudo_cmd ln -sfn /etc/nginx/sites-available/garmetix-srp.conf /etc/nginx/sites-enabled/garmetix-srp.conf
sudo_cmd nginx -t
sudo_cmd systemctl reload nginx

sudo_cmd cp "\$REMOTE_BASE/current/ops/garmetix-srp-api.service" /etc/systemd/system/garmetix-srp-api.service
sudo_cmd systemctl daemon-reload
sudo_cmd systemctl restart garmetix-srp-api.service

echo "Installed/refreshed Nginx config (incl. /swalekha/) and restarted the API service."
INSTALL
  chmod +x "$LOCAL_OVERLAY/ops/install-swalekha-on-host.sh"
}

run_pre_deploy_backup() {
  if [ "$SKIP_DB_BACKUP" = true ]; then
    echo "WARNING: skipping pre-deploy database backup because --skip-db-backup was supplied."
    return
  fi
  if [ -z "$SRP_DEPLOY_STAGE" ]; then
    echo "A deployment stage name is required for the pre-deploy database backup." >&2
    echo "Example: --stage=SwalekhaModuleGoLive" >&2
    echo "Use --skip-db-backup only for an explicitly approved emergency/manual exception." >&2
    exit 1
  fi
  echo "Creating pre-deploy database backup for stage $SRP_DEPLOY_STAGE (covers the main database, and swalekha_db once ConnectionStrings__Swalekha exists)"
  GARMETIX_BACKUP_STAGE="$SRP_DEPLOY_STAGE" \
  SRP_BACKUP_DIR="$SRP_BACKUP_DIR" \
  "$MODULAR_ROOT/deploy/srp-backup-database.sh" --stage="$SRP_DEPLOY_STAGE"
}

ensure_remote_database_and_connection_string() {
  if [ "$SKIP_DB_SETUP" = true ]; then
    echo "Skipping swalekha_db/ConnectionStrings__Swalekha setup (--skip-db-setup)."
    return
  fi

  echo "Ensuring $SRP_SWALEKHA_DB_NAME exists and ConnectionStrings__Swalekha is configured on $SRP_DEPLOY_TARGET"

  local sudo_prefix
  sudo_prefix="$(remote_sudo_env_prefix)"

  local remote_script
  remote_script="$(cat <<'REMOTE'
set -euo pipefail

sudo_cmd() {
  if [ -n "${SRP_REMOTE_SUDO_PASSWORD:-}" ]; then
    printf '%s\n' "$SRP_REMOTE_SUDO_PASSWORD" | sudo -S -p '' "$@"
  else
    sudo "$@"
  fi
}

conn_value() {
  local key="$1"
  printf '%s' "$CONNECTION_STRING" | tr ';' '\n' | awk -F= -v wanted="$key" 'tolower($1) == tolower(wanted) { print substr($0, index($0, "=") + 1); exit }'
}

if [ ! -f "$SRP_API_ENV_PATH" ]; then
  echo "API env file not found: $SRP_API_ENV_PATH - is Garmetix actually installed on this host yet?" >&2
  exit 2
fi

if ! command -v psql >/dev/null 2>&1; then
  echo "psql is not installed on this host - install the postgresql-client package first." >&2
  exit 3
fi

CONNECTION_STRING="$(sudo_cmd sed -n 's/^ConnectionStrings__Default=//p' "$SRP_API_ENV_PATH" | tail -1)"
if [ -z "$CONNECTION_STRING" ]; then
  echo "ConnectionStrings__Default was not found in $SRP_API_ENV_PATH" >&2
  exit 4
fi

DB_HOST="$(conn_value Host)"
DB_PORT="$(conn_value Port)"
DB_USER="$(conn_value Username)"
DB_PASSWORD="$(conn_value Password)"
DB_HOST="${DB_HOST:-127.0.0.1}"
DB_PORT="${DB_PORT:-5432}"

if [ -z "$DB_USER" ] || [ -z "$DB_PASSWORD" ]; then
  echo "Could not parse the database user/password out of ConnectionStrings__Default." >&2
  exit 5
fi

export PGPASSWORD="$DB_PASSWORD"

EXISTS="$(psql -h "$DB_HOST" -p "$DB_PORT" -U "$DB_USER" -d postgres -tAc "SELECT 1 FROM pg_database WHERE datname = '$SWALEKHA_DB_NAME'" 2>&1)" || {
  echo "Could not query the Postgres server to check for $SWALEKHA_DB_NAME: $EXISTS" >&2
  unset PGPASSWORD
  exit 6
}

if [ "$EXISTS" = "1" ]; then
  echo "$SWALEKHA_DB_NAME already exists - not recreating it."
else
  echo "Creating database $SWALEKHA_DB_NAME (owned by $DB_USER)"
  createdb -h "$DB_HOST" -p "$DB_PORT" -U "$DB_USER" "$SWALEKHA_DB_NAME"
fi
unset PGPASSWORD

if sudo_cmd grep -q '^ConnectionStrings__Swalekha=' "$SRP_API_ENV_PATH"; then
  echo "ConnectionStrings__Swalekha already present in $SRP_API_ENV_PATH - leaving it untouched (it may point at a deliberately separate host)."
else
  BACKUP_ENV="$SRP_API_ENV_PATH.bak.$(date +%s)"
  sudo_cmd cp "$SRP_API_ENV_PATH" "$BACKUP_ENV"
  echo "Backed up $SRP_API_ENV_PATH to $BACKUP_ENV before editing."

  SWALEKHA_CONNECTION_STRING="Host=$DB_HOST;Port=$DB_PORT;Database=$SWALEKHA_DB_NAME;Username=$DB_USER;Password=$DB_PASSWORD"
  TMP_ENV="$(mktemp)"
  sudo_cmd cat "$SRP_API_ENV_PATH" > "$TMP_ENV"
  printf 'ConnectionStrings__Swalekha=%s\n' "$SWALEKHA_CONNECTION_STRING" >> "$TMP_ENV"
  sudo_cmd install -m 600 -o root -g root "$TMP_ENV" "$SRP_API_ENV_PATH"
  rm -f "$TMP_ENV"
  echo "Added ConnectionStrings__Swalekha to $SRP_API_ENV_PATH (same host/user/password as the main database, database=$SWALEKHA_DB_NAME)."
fi
REMOTE
)"

  ssh_cmd "${sudo_prefix}$(remote_sudo_function) export SRP_API_ENV_PATH=$(shell_quote "$SRP_API_ENV_PATH"); export SWALEKHA_DB_NAME=$(shell_quote "$SRP_SWALEKHA_DB_NAME"); bash -s" <<<"$remote_script"
}

stage_and_upload_release() {
  echo "Preparing new release $RELEASE_ID on the remote host (copy-forward from current)"
  ssh_cmd "
    set -e
    mkdir -p '$REMOTE_RELEASE'
    if [ -d '$SRP_REMOTE_BASE/current' ]; then
      cp -a '$SRP_REMOTE_BASE/current/.' '$REMOTE_RELEASE/'
    fi
    mkdir -p '$REMOTE_RELEASE/web-root' '$REMOTE_RELEASE/api' '$REMOTE_RELEASE/ops'
  "

  echo "Uploading swalekha-web build"
  ssh_cmd "mkdir -p '$REMOTE_RELEASE/web-root/swalekha'"
  upload_dir "$LOCAL_OVERLAY/web-root/swalekha" "$REMOTE_RELEASE/web-root/swalekha"

  if [ "$SKIP_API" = false ]; then
    echo "Uploading freshly published API"
    upload_dir "$LOCAL_OVERLAY/api" "$REMOTE_RELEASE/api"
  else
    echo "Leaving the copied-forward api/ as-is (--skip-api)."
  fi

  echo "Uploading ops templates"
  upload_dir "$LOCAL_OVERLAY/ops" "$REMOTE_RELEASE/ops"

  echo "Patching the swalekhaUrl profile-menu link into every already-deployed app's compiled HTML"
  ssh_cmd "
    set -e
    find '$REMOTE_RELEASE/web-root' -type f -name '*.html' -print0 | xargs -0 -r perl -0pi -e 's#swalekhaUrl:\"[^\"]*\"#swalekhaUrl:\"$SRP_SWALEKHA_URL\"#g'
  "

  echo "Switching current -> $RELEASE_ID"
  ssh_cmd "
    set -e
    ln -sfn '$REMOTE_RELEASE' '$SRP_REMOTE_BASE/current'
    find '$SRP_REMOTE_BASE/releases' -mindepth 1 -maxdepth 1 -type d | sort -r | tail -n +$((SRP_KEEP_RELEASES + 1)) | xargs -r rm -rf
  "
}

install_remote_config() {
  if [ "$INSTALL_REMOTE" != true ]; then
    cat <<NEXT
Remote install was not applied (no --install-remote).
/swalekha/ will 404 and the API restart (needed to load Swalekha's endpoints) has not happened
until you run:
  ssh -p $SRP_SSH_PORT $SRP_DEPLOY_TARGET "bash '$SRP_REMOTE_BASE/current/ops/install-swalekha-on-host.sh'"
NEXT
    return
  fi
  local sudo_prefix
  sudo_prefix="$(remote_sudo_env_prefix)"
  ssh_cmd "${sudo_prefix}bash '$REMOTE_RELEASE/ops/install-swalekha-on-host.sh'"
}

verify_deployment() {
  if [ "$INSTALL_REMOTE" != true ]; then
    return
  fi
  echo "Verifying the deployed release on the host..."
  ssh_cmd "
    set -e
    swalekha_status=\$(curl -s -o /dev/null -w '%{http_code}' 'http://127.0.0.1:$SRP_NGINX_PORT/swalekha/')
    health_status=\$(curl -s -o /dev/null -w '%{http_code}' 'http://127.0.0.1:$SRP_NGINX_PORT/api/swalekha/health')
    echo \"  /swalekha/ -> \$swalekha_status (expect 200)\"
    echo \"  /api/swalekha/health -> \$health_status (expect 200/401, not 404/502)\"
    if [ \"\$swalekha_status\" != '200' ]; then
      echo 'WARNING: /swalekha/ did not return 200.' >&2
    fi
    if [ \"\$health_status\" = '404' ] || [ \"\$health_status\" = '502' ] || [ \"\$health_status\" = '503' ]; then
      echo 'WARNING: /api/swalekha/health looks unreachable - check the API service (journalctl -u garmetix-srp-api).' >&2
    fi
  "
  echo "Spot-check the public domain yourself: https://$SRP_DOMAIN$SRP_SWALEKHA_BASE_PATH"
}

print_plan
if [ "$DRY_RUN" = true ]; then
  exit 0
fi

NPM_COMMAND="$(resolve_command npm npm.cmd || true)"
if [ -z "$NPM_COMMAND" ]; then
  echo "Missing required command: npm" >&2
  exit 1
fi
if [ "$SKIP_API" = false ]; then
  DOTNET_COMMAND="$(resolve_command dotnet dotnet.exe || true)"
  if [ -z "$DOTNET_COMMAND" ]; then
    echo "Missing required command: dotnet or dotnet.exe" >&2
    exit 1
  fi
fi

rm -rf "$LOCAL_OVERLAY"
mkdir -p "$LOCAL_OVERLAY"

build_swalekha_app
publish_api_overlay
write_ops_templates

echo "Local overlay staged at $LOCAL_OVERLAY"

if [ "$BUILD_ONLY" = true ]; then
  echo "Build-only mode complete. Nothing uploaded."
  exit 0
fi

need_command ssh
run_pre_deploy_backup
ensure_remote_database_and_connection_string
stage_and_upload_release
echo "Uploaded Swalekha release to $SRP_DEPLOY_TARGET:$REMOTE_RELEASE (now current)"

install_remote_config
verify_deployment

cat <<DONE

Swalekha deploy finished.
  Release:  $RELEASE_ID
  Frontend: https://$SRP_DOMAIN$SRP_SWALEKHA_BASE_PATH (Owner login only)
  API:      $SRP_PUBLIC_API_BASE_URL/swalekha/*

If this is the first time Swalekha has ever been deployed here, also run once:
  npm --prefix frontend/modular run deploy:srp:backup -- --stage=$SRP_DEPLOY_STAGE
to confirm the swalekha_db line now appears in Backupfilehistory.md.
DONE
