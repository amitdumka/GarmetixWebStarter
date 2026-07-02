#!/usr/bin/env bash
set -euo pipefail

usage() {
  cat <<'USAGE'
Garmetix SRP whole-site deploy

Usage:
  bash modular/deploy/srp-whole-site-deploy.sh --init-config
  bash modular/deploy/srp-whole-site-deploy.sh --dry-run
  bash modular/deploy/srp-whole-site-deploy.sh --build-only
  bash modular/deploy/srp-whole-site-deploy.sh
  bash modular/deploy/srp-whole-site-deploy.sh --install-remote

Reads config from:
  $GARMETIX_SRP_DEPLOY_CONFIG, or ~/.config/garmetix/srp-deploy.env

Optional private secrets file:
  $SRP_SECRETS_PATH, or ~/.config/garmetix/srp-deploy.secrets.env

The default SRP shape is one public hostname:
  /          Main Back Office
  /pos/      POS
  /hr/       HR
  /ai-sense/ AI Sense
  /books/    Accounting/Books
  /admin/    Admin/SaaS
  /api/      ASP.NET Core API reverse proxy
USAGE
}

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
MODULAR_ROOT="$REPO_ROOT/modular"
CONFIG_EXAMPLE="$MODULAR_ROOT/deploy/srp-deploy.config.example.env"
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
    CONFIG_WAS_AUTO_DETECTED=true
  fi
fi

INIT_CONFIG=false
DRY_RUN=false
BUILD_ONLY=false
INSTALL_REMOTE=false
SKIP_BUILD=false
SKIP_API=false

for arg in "$@"; do
  case "$arg" in
    --init-config) INIT_CONFIG=true ;;
    --dry-run) DRY_RUN=true ;;
    --build-only) BUILD_ONLY=true ;;
    --install-remote) INSTALL_REMOTE=true ;;
    --skip-build) SKIP_BUILD=true ;;
    --skip-api) SKIP_API=true ;;
    -h|--help) usage; exit 0 ;;
    *) echo "Unknown argument: $arg" >&2; usage; exit 1 ;;
  esac
done

if [ "$INIT_CONFIG" = true ]; then
  mkdir -p "$(dirname "$CONFIG_PATH")"
  if [ -f "$CONFIG_PATH" ]; then
    echo "Config already exists: $CONFIG_PATH"
    exit 0
  fi
  cp "$CONFIG_EXAMPLE" "$CONFIG_PATH"
  echo "Created $CONFIG_PATH"
  echo "Edit it before deploying. Do not add secrets to git."
  exit 0
fi

if [ -f "$CONFIG_PATH" ]; then
  # shellcheck disable=SC1090
  source "$CONFIG_PATH"
else
  echo "Config not found at $CONFIG_PATH; using safe defaults. Run --init-config to create it."
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
SRP_PATH_BASED_URLS="${SRP_PATH_BASED_URLS:-true}"
SRP_MAIN_BASE_PATH="${SRP_MAIN_BASE_PATH:-/}"
SRP_POS_BASE_PATH="${SRP_POS_BASE_PATH:-/pos/}"
SRP_HR_BASE_PATH="${SRP_HR_BASE_PATH:-/hr/}"
SRP_AI_SENSE_BASE_PATH="${SRP_AI_SENSE_BASE_PATH:-/ai-sense/}"
SRP_BOOKS_BASE_PATH="${SRP_BOOKS_BASE_PATH:-/books/}"
SRP_ADMIN_BASE_PATH="${SRP_ADMIN_BASE_PATH:-/admin/}"
if [ "$SRP_PATH_BASED_URLS" = true ]; then
  SRP_PUBLIC_API_BASE_URL="/api"
  SRP_MAIN_URL="$SRP_MAIN_BASE_PATH"
  SRP_POS_URL="$SRP_POS_BASE_PATH"
  SRP_HR_URL="$SRP_HR_BASE_PATH"
  SRP_AI_SENSE_URL="$SRP_AI_SENSE_BASE_PATH"
  SRP_BOOKS_URL="$SRP_BOOKS_BASE_PATH"
  SRP_ADMIN_URL="$SRP_ADMIN_BASE_PATH"
else
  SRP_PUBLIC_API_BASE_URL="${SRP_PUBLIC_API_BASE_URL:-https://$SRP_DOMAIN/api}"
  SRP_MAIN_URL="${SRP_MAIN_URL:-https://$SRP_DOMAIN}"
  SRP_POS_URL="${SRP_POS_URL:-https://$SRP_DOMAIN/pos}"
  SRP_HR_URL="${SRP_HR_URL:-https://$SRP_DOMAIN/hr}"
  SRP_AI_SENSE_URL="${SRP_AI_SENSE_URL:-https://$SRP_DOMAIN/ai-sense}"
  SRP_BOOKS_URL="${SRP_BOOKS_URL:-https://$SRP_DOMAIN/books}"
  SRP_ADMIN_URL="${SRP_ADMIN_URL:-https://$SRP_DOMAIN/admin}"
fi
SRP_API_PROJECT="${SRP_API_PROJECT:-legacy/backend/Garmetix.Api/Garmetix.Api.csproj}"
SRP_SKIP_API_PUBLISH="${SRP_SKIP_API_PUBLISH:-false}"
SRP_API_PUBLISH_SELF_CONTAINED="${SRP_API_PUBLISH_SELF_CONTAINED:-true}"
SRP_API_RUNTIME="${SRP_API_RUNTIME:-linux-x64}"
SRP_API_ENV_PATH="${SRP_API_ENV_PATH:-/etc/garmetix/srp-api.env}"
SRP_CLOUDFLARE_TUNNEL_NAME="${SRP_CLOUDFLARE_TUNNEL_NAME:-garmetix-srp}"
SRP_CLOUDFLARE_CREDENTIALS_FILE="${SRP_CLOUDFLARE_CREDENTIALS_FILE:-/etc/cloudflared/garmetix-srp.json}"
SRP_CLOUDFLARE_CONFIG_PATH="${SRP_CLOUDFLARE_CONFIG_PATH:-/etc/cloudflared/garmetix-srp.yml}"

RELEASE_ID="$(date -u +%Y%m%d%H%M%S)"
LOCAL_RELEASE="$REPO_ROOT/outputs/deploy/srp/releases/$RELEASE_ID"
WEB_ROOT="$LOCAL_RELEASE/web-root"
OPS_ROOT="$LOCAL_RELEASE/ops"
REMOTE_RELEASE="$SRP_REMOTE_BASE/releases/$RELEASE_ID"

print_plan() {
  cat <<PLAN
SRP deployment plan
  Target:        $SRP_DEPLOY_TARGET
  Public site:   https://$SRP_DOMAIN
  Remote base:   $SRP_REMOTE_BASE
  Nginx port:    $SRP_NGINX_PORT
  API port:      $SRP_API_PORT
  Config file:   $CONFIG_PATH
  Secrets file:  $SRP_SECRETS_PATH
  Auth mode:     $(if [ -n "${SRP_SSH_PASSWORD:-}" ]; then echo "password via sshpass"; else echo "SSH key or interactive"; fi)
  Local release: $LOCAL_RELEASE

Routes:
  $SRP_MAIN_URL -> /
  $SRP_POS_URL -> /pos/
  $SRP_HR_URL -> /hr/
  $SRP_AI_SENSE_URL -> /ai-sense/
  $SRP_BOOKS_URL -> /books/
  $SRP_ADMIN_URL -> /admin/
  $SRP_PUBLIC_API_BASE_URL -> /api/
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
  local path_value="$1"
  if [[ "$DOTNET_COMMAND" == *dotnet.exe ]] && command -v wslpath >/dev/null 2>&1; then
    wslpath -w "$path_value"
  else
    printf '%s' "$path_value"
  fi
}

shell_quote() {
  printf "%q" "$1"
}

ssh_base_args() {
  printf "%s\n" "-p" "$SRP_SSH_PORT" "-o" "StrictHostKeyChecking=accept-new"
}

ssh_cmd() {
  if [ -n "${SRP_SSH_PASSWORD:-}" ]; then
    need_command sshpass
    SSHPASS="$SRP_SSH_PASSWORD" sshpass -e ssh -p "$SRP_SSH_PORT" -o StrictHostKeyChecking=accept-new "$SRP_DEPLOY_TARGET" "$@"
  else
    ssh -p "$SRP_SSH_PORT" -o StrictHostKeyChecking=accept-new "$SRP_DEPLOY_TARGET" "$@"
  fi
}

rsync_cmd() {
  if [ -n "${SRP_SSH_PASSWORD:-}" ]; then
    need_command sshpass
    SSHPASS="$SRP_SSH_PASSWORD" sshpass -e rsync -az --delete -e "ssh -p $SRP_SSH_PORT -o StrictHostKeyChecking=accept-new" "$LOCAL_RELEASE/" "$SRP_DEPLOY_TARGET:$REMOTE_RELEASE/"
  else
    rsync -az --delete -e "ssh -p $SRP_SSH_PORT -o StrictHostKeyChecking=accept-new" "$LOCAL_RELEASE/" "$SRP_DEPLOY_TARGET:$REMOTE_RELEASE/"
  fi
}

upload_payload() {
  if command -v rsync >/dev/null 2>&1; then
    rsync_cmd
    return
  fi

  need_command tar
  echo "rsync not found; using tar stream upload."
  (
    cd "$LOCAL_RELEASE"
    tar -czf - .
  ) | ssh_cmd "rm -rf '$REMOTE_RELEASE' && mkdir -p '$REMOTE_RELEASE' && tar -xzf - -C '$REMOTE_RELEASE'"
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

copy_public_output() {
  local app_name="$1"
  local dest="$2"
  local public_dir="$MODULAR_ROOT/apps/$app_name/.output/public"
  if [ ! -d "$public_dir" ]; then
    echo "Nuxt static output not found for $app_name: $public_dir" >&2
    exit 1
  fi
  mkdir -p "$dest"
  cp -a "$public_dir/." "$dest/"
}

patch_static_runtime_config() {
  local dest="$1"
  find "$dest" -type f -name '*.html' -print0 | while IFS= read -r -d '' file; do
    perl -0pi -e "s#apiBaseUrl:\"[^\"]*\"#apiBaseUrl:\"$SRP_PUBLIC_API_BASE_URL\"#g" "$file"
    perl -0pi -e "s#NUXT_PUBLIC_GARMETIX_MAIN_URL:\"[^\"]*\"#NUXT_PUBLIC_GARMETIX_MAIN_URL:\"$SRP_MAIN_URL\"#g" "$file"
    perl -0pi -e "s#NUXT_PUBLIC_GARMETIX_POS_URL:\"[^\"]*\"#NUXT_PUBLIC_GARMETIX_POS_URL:\"$SRP_POS_URL\"#g" "$file"
    perl -0pi -e "s#NUXT_PUBLIC_GARMETIX_HR_URL:\"[^\"]*\"#NUXT_PUBLIC_GARMETIX_HR_URL:\"$SRP_HR_URL\"#g" "$file"
    perl -0pi -e "s#NUXT_PUBLIC_GARMETIX_AI_SENSE_URL:\"[^\"]*\"#NUXT_PUBLIC_GARMETIX_AI_SENSE_URL:\"$SRP_AI_SENSE_URL\"#g" "$file"
    perl -0pi -e "s#NUXT_PUBLIC_GARMETIX_BOOKS_URL:\"[^\"]*\"#NUXT_PUBLIC_GARMETIX_BOOKS_URL:\"$SRP_BOOKS_URL\"#g" "$file"
    perl -0pi -e "s#NUXT_PUBLIC_GARMETIX_ADMIN_URL:\"[^\"]*\"#NUXT_PUBLIC_GARMETIX_ADMIN_URL:\"$SRP_ADMIN_URL\"#g" "$file"
  done
}

build_app() {
  local app_name="$1"
  local base_path="$2"
  local dest="$3"
  echo "Building $app_name with base path $base_path"
  if [ "$SKIP_BUILD" = false ]; then
    rm -rf "$MODULAR_ROOT/apps/$app_name/.output"
    (
      cd "$MODULAR_ROOT"
      NUXT_APP_BASE_URL="$base_path" \
      NUXT_PUBLIC_GARMETIX_API_BASE_URL="$SRP_PUBLIC_API_BASE_URL" \
      NUXT_PUBLIC_GARMETIX_MAIN_URL="$SRP_MAIN_URL" \
      NUXT_PUBLIC_GARMETIX_POS_URL="$SRP_POS_URL" \
      NUXT_PUBLIC_GARMETIX_HR_URL="$SRP_HR_URL" \
      NUXT_PUBLIC_GARMETIX_AI_SENSE_URL="$SRP_AI_SENSE_URL" \
      NUXT_PUBLIC_GARMETIX_BOOKS_URL="$SRP_BOOKS_URL" \
      NUXT_PUBLIC_GARMETIX_ADMIN_URL="$SRP_ADMIN_URL" \
      "$NPM_COMMAND" run "build:$app_name"
    )
  fi
  copy_public_output "$app_name" "$dest"
  patch_static_runtime_config "$dest"
}

write_templates() {
  mkdir -p "$OPS_ROOT"
  local api_exec_start
  if [ "$SRP_API_PUBLISH_SELF_CONTAINED" = true ]; then
    api_exec_start="$SRP_REMOTE_BASE/current/api/Garmetix.Api"
  else
    api_exec_start="/usr/bin/dotnet $SRP_REMOTE_BASE/current/api/Garmetix.Api.dll"
  fi
  cat > "$OPS_ROOT/nginx-garmetix-srp.conf" <<NGINX
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

    location /pos/ {
        try_files \$uri \$uri/ /pos/index.html;
    }

    location /hr/ {
        try_files \$uri \$uri/ /hr/index.html;
    }

    location /ai-sense/ {
        try_files \$uri \$uri/ /ai-sense/index.html;
    }

    location /books/ {
        try_files \$uri \$uri/ /books/index.html;
    }

    location /admin/ {
        try_files \$uri \$uri/ /admin/index.html;
    }

    location / {
        try_files \$uri \$uri/ /index.html;
    }
}
NGINX

  cat > "$OPS_ROOT/garmetix-srp-api.service" <<SERVICE
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

[Install]
WantedBy=multi-user.target
SERVICE

  cat > "$OPS_ROOT/srp-api.env.template" <<ENV
# Copy to $SRP_API_ENV_PATH on the server and edit values there.
# Keep this file out of git after editing.
Database__AutoMigrate=true
Database__SchemaBootstrapMode=FreshBaseline
ConnectionStrings__Default=Host=127.0.0.1;Port=5432;Database=garmetix_srp;Username=garmetix;Password=CHANGE_ME
Cors__AllowedOriginsCsv=https://$SRP_DOMAIN
Jwt__Issuer=Garmetix
Jwt__Audience=Garmetix
Jwt__SigningKey=CHANGE_ME_MINIMUM_32_CHARS
PasswordReset__FrontendBaseUrl=https://$SRP_DOMAIN
License__EnforcementEnabled=false
ENV

  cat > "$OPS_ROOT/cloudflared-garmetix-srp.yml" <<CLOUDFLARED
tunnel: $SRP_CLOUDFLARE_TUNNEL_NAME
credentials-file: $SRP_CLOUDFLARE_CREDENTIALS_FILE

ingress:
  - hostname: $SRP_DOMAIN
    service: http://localhost:$SRP_NGINX_PORT
  - service: http_status:404
CLOUDFLARED

  cat > "$OPS_ROOT/install-srp-on-host.sh" <<INSTALL
#!/usr/bin/env bash
set -euo pipefail

REMOTE_BASE="$SRP_REMOTE_BASE"
API_ENV_PATH="$SRP_API_ENV_PATH"
CLOUDFLARE_CONFIG_PATH="$SRP_CLOUDFLARE_CONFIG_PATH"

sudo_cmd() {
  if [ -n "\${SRP_REMOTE_SUDO_PASSWORD:-}" ]; then
    printf '%s\n' "\$SRP_REMOTE_SUDO_PASSWORD" | sudo -S -p '' "\$@"
  else
    sudo "\$@"
  fi
}

sudo_cmd mkdir -p "\$(dirname "\$API_ENV_PATH")" "\$(dirname "\$CLOUDFLARE_CONFIG_PATH")"

if [ ! -f "\$API_ENV_PATH" ]; then
  sudo_cmd cp "\$REMOTE_BASE/current/ops/srp-api.env.template" "\$API_ENV_PATH"
  echo "Created \$API_ENV_PATH. Edit database/JWT values before starting the API."
fi

sudo_cmd cp "\$REMOTE_BASE/current/ops/nginx-garmetix-srp.conf" /etc/nginx/sites-available/garmetix-srp.conf
sudo_cmd ln -sfn /etc/nginx/sites-available/garmetix-srp.conf /etc/nginx/sites-enabled/garmetix-srp.conf
sudo_cmd nginx -t
sudo_cmd systemctl reload nginx

sudo_cmd cp "\$REMOTE_BASE/current/ops/garmetix-srp-api.service" /etc/systemd/system/garmetix-srp-api.service
sudo_cmd systemctl daemon-reload
sudo_cmd systemctl enable garmetix-srp-api.service
if sudo_cmd grep -q 'CHANGE_ME' "\$API_ENV_PATH"; then
  echo "\$API_ENV_PATH still contains CHANGE_ME placeholders. API service was installed but not started."
else
  sudo_cmd systemctl restart garmetix-srp-api.service
fi

if [ ! -f "\$CLOUDFLARE_CONFIG_PATH" ]; then
  sudo_cmd cp "\$REMOTE_BASE/current/ops/cloudflared-garmetix-srp.yml" "\$CLOUDFLARE_CONFIG_PATH"
  echo "Created \$CLOUDFLARE_CONFIG_PATH. Add the real Cloudflare tunnel credentials before enabling cloudflared."
fi

echo "Installed SRP Nginx and API service templates."
INSTALL
  chmod +x "$OPS_ROOT/install-srp-on-host.sh"
}

publish_api() {
  if [ "$SKIP_API" = true ] || [ "$SRP_SKIP_API_PUBLISH" = true ]; then
    echo "Skipping API publish."
    mkdir -p "$LOCAL_RELEASE/api"
    return
  fi
  local project_path output_path
  project_path="$(dotnet_path_arg "$REPO_ROOT/$SRP_API_PROJECT")"
  output_path="$(dotnet_path_arg "$LOCAL_RELEASE/api")"
  if [ "$SRP_API_PUBLISH_SELF_CONTAINED" = true ]; then
    "$DOTNET_COMMAND" publish "$project_path" -c Release -r "$SRP_API_RUNTIME" --self-contained true -o "$output_path"
  else
    "$DOTNET_COMMAND" publish "$project_path" -c Release -o "$output_path"
  fi
}

upload_release() {
  need_command ssh
  local sudo_prefix
  sudo_prefix="$(remote_sudo_env_prefix)"
  ssh_cmd "${sudo_prefix}$(remote_sudo_function) sudo_cmd mkdir -p '$SRP_REMOTE_BASE/releases'; sudo_cmd chown -R \"\$(id -u):\$(id -g)\" '$SRP_REMOTE_BASE'"
  upload_payload
  ssh_cmd "ln -sfn '$REMOTE_RELEASE' '$SRP_REMOTE_BASE/current' && find '$SRP_REMOTE_BASE/releases' -mindepth 1 -maxdepth 1 -type d | sort -r | tail -n +$((SRP_KEEP_RELEASES + 1)) | xargs -r rm -rf"
}

install_remote() {
  local sudo_prefix
  sudo_prefix="$(remote_sudo_env_prefix)"
  ssh_cmd "${sudo_prefix}bash '$SRP_REMOTE_BASE/current/ops/install-srp-on-host.sh'"
}

print_plan
if [ "$DRY_RUN" = true ]; then
  exit 0
fi

NPM_COMMAND="$(resolve_command npm npm.cmd || true)"
DOTNET_COMMAND="$(resolve_command dotnet dotnet.exe || true)"
if [ -z "$NPM_COMMAND" ]; then
  echo "Missing required command: npm" >&2
  exit 1
fi
if [ -z "$DOTNET_COMMAND" ] && [ "$SKIP_API" = false ] && [ "$SRP_SKIP_API_PUBLISH" != true ]; then
  echo "Missing required command: dotnet or dotnet.exe" >&2
  exit 1
fi
rm -rf "$LOCAL_RELEASE"
mkdir -p "$WEB_ROOT"

build_app main "$SRP_MAIN_BASE_PATH" "$WEB_ROOT"
build_app pos "$SRP_POS_BASE_PATH" "$WEB_ROOT/pos"
build_app hr "$SRP_HR_BASE_PATH" "$WEB_ROOT/hr"
build_app ai-sense "$SRP_AI_SENSE_BASE_PATH" "$WEB_ROOT/ai-sense"
build_app books "$SRP_BOOKS_BASE_PATH" "$WEB_ROOT/books"
build_app admin "$SRP_ADMIN_BASE_PATH" "$WEB_ROOT/admin"
publish_api
write_templates

echo "Local SRP release staged at $LOCAL_RELEASE"

if [ "$BUILD_ONLY" = true ]; then
  echo "Build-only mode complete. Nothing uploaded."
  exit 0
fi

upload_release
echo "Uploaded SRP release to $SRP_DEPLOY_TARGET:$REMOTE_RELEASE"

if [ "$INSTALL_REMOTE" = true ]; then
  install_remote
else
  cat <<NEXT
Remote install was not applied.
To apply Nginx/API templates after reviewing them:
  ssh -p $SRP_SSH_PORT $SRP_DEPLOY_TARGET "bash '$SRP_REMOTE_BASE/current/ops/install-srp-on-host.sh'"

Cloudflare tunnel setup on the server:
  cloudflared tunnel create $SRP_CLOUDFLARE_TUNNEL_NAME
  cloudflared tunnel route dns $SRP_CLOUDFLARE_TUNNEL_NAME $SRP_DOMAIN
  sudo cp $SRP_REMOTE_BASE/current/ops/cloudflared-garmetix-srp.yml $SRP_CLOUDFLARE_CONFIG_PATH
  sudo systemctl restart cloudflared
NEXT
fi
