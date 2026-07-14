#!/usr/bin/env bash
set -Eeuo pipefail

REMOTE_ROOT="${REMOTE_ROOT:-/opt/garmetix}"
RELEASE="${RELEASE:-$REMOTE_ROOT/current}"
COMPOSE_PROJECT_NAME="${COMPOSE_PROJECT_NAME:-garmetix}"
RESET_DB="${RESET_DB:-false}"
cd "$RELEASE"

log(){ printf '\033[1;32m==>\033[0m %s\n' "$*"; }
warn(){ printf '\033[1;33mWARN:\033[0m %s\n' "$*" >&2; }
run_sudo(){
  if [[ $EUID -eq 0 ]]; then
    "$@"
  elif [[ -n "${GARMETIX_SUDO_PASSWORD:-}" ]]; then
    printf '%s\n' "$GARMETIX_SUDO_PASSWORD" | sudo -S -p '' "$@"
  else
    sudo "$@"
  fi
}

if [[ ! -f .env.production ]]; then
  ln -sfn "$REMOTE_ROOT/shared/env/.env.production" .env.production
fi

# Prefer project compose if present, otherwise deploy-kit compose.
COMPOSE_FILES=()
if [[ -f docker-compose.prod.yml ]]; then
  COMPOSE_FILES+=( -f docker-compose.prod.yml )
elif [[ -f deploy/docker-compose.prod.yml ]]; then
  COMPOSE_FILES+=( -f deploy/docker-compose.prod.yml )
elif [[ -f docker-compose.yml ]]; then
  COMPOSE_FILES+=( -f docker-compose.yml )
fi
if [[ -f deploy/docker-compose.cloudflare.yml ]]; then
  COMPOSE_FILES+=( -f deploy/docker-compose.cloudflare.yml )
fi

if [[ ${#COMPOSE_FILES[@]} -eq 0 ]]; then
  echo "No Docker Compose file found in $RELEASE" >&2
  exit 1
fi

compose(){ COMPOSE_PROJECT_NAME="$COMPOSE_PROJECT_NAME" docker compose --env-file .env.production "${COMPOSE_FILES[@]}" "$@"; }

if [[ "$RESET_DB" == "true" ]] || grep -q '^RESET_DATABASE_ON_DEPLOY=true' .env.production 2>/dev/null; then
  warn "RESET_DATABASE_ON_DEPLOY=true. Removing old Garmetix containers and Postgres volumes."
  compose down --remove-orphans || true
  docker rm -f garmetix-postgres-1 garmetix-api-1 garmetix-web-1 garmetix-cloudflared-1 current-postgres-1 current-api-1 current-web-1 current-cloudflared-1 2>/dev/null || true
  for V in \
    garmetix_postgres_data \
    garmetix_garmetix_pg \
    garmetix_garmetix_postgres_data \
    garmetix_pg \
    current_postgres_data \
    current_garmetix_pg \
    postgres_data; do
    docker volume rm "$V" 2>/dev/null || true
  done
fi

log "Validating Compose services"
SERVICES="$(compose config --services)"
printf '%s\n' "$SERVICES"

has_service(){ printf '%s\n' "$SERVICES" | grep -qx "$1"; }

# Stop app services first so API does not keep crash-looping with an old DB password
# while PostgreSQL is being aligned to the current .env.production.
for SVC in api web cloudflared; do
  if has_service "$SVC"; then
    compose stop "$SVC" >/dev/null 2>&1 || true
  fi
done

log "Starting PostgreSQL first"
compose up -d postgres

wait_postgres_container(){
  local attempts="${1:-60}" i
  for i in $(seq 1 "$attempts"); do
    if docker ps --format '{{.Names}} {{.Status}}' | grep -q '^garmetix-postgres-1 .*healthy'; then
      return 0
    fi
    sleep 2
  done
  warn "Postgres container did not report healthy in time; continuing to password sync attempt."
  return 0
}

get_env_value(){
  local key="$1"
  grep -E "^${key}=" .env.production | tail -1 | cut -d= -f2- | sed -e 's/^"//' -e 's/"$//' -e "s/^'//" -e "s/'$//"
}

sync_postgres_password(){
  local pg_container="garmetix-postgres-1"
  local pg_db pg_user pg_pass sql_host sql_container
  pg_db="$(get_env_value POSTGRES_DB)"; pg_db="${pg_db:-garmetix}"
  pg_user="$(get_env_value POSTGRES_USER)"; pg_user="${pg_user:-garmetix}"
  pg_pass="$(get_env_value POSTGRES_PASSWORD)"
  if [[ -z "$pg_pass" ]]; then
    warn "POSTGRES_PASSWORD is empty in .env.production; skipping password sync."
    return 0
  fi
  if ! docker ps --format '{{.Names}}' | grep -qx "$pg_container"; then
    warn "Postgres container $pg_container not found; skipping password sync."
    return 0
  fi

  log "Aligning PostgreSQL role password with .env.production for user '$pg_user'"
  sql_host="$(mktemp)"
  python3 - "$pg_user" "$pg_pass" "$pg_db" > "$sql_host" <<'PYSYNCDB'
import sys
user, password, db = sys.argv[1], sys.argv[2], sys.argv[3]
def lit(s):
    return "'" + s.replace("'", "''") + "'"
def ident(s):
    return '"' + s.replace('"', '""') + '"'
print("DO $$")
print("BEGIN")
print(f"  IF NOT EXISTS (SELECT 1 FROM pg_roles WHERE rolname = {lit(user)}) THEN")
print(f"    CREATE ROLE {ident(user)} LOGIN PASSWORD {lit(password)};")
print("  ELSE")
print(f"    ALTER ROLE {ident(user)} WITH LOGIN PASSWORD {lit(password)};")
print("  END IF;")
print("END")
print("$$;")
print(f"SELECT format('CREATE DATABASE %I OWNER %I', {lit(db)}, {lit(user)}) WHERE NOT EXISTS (SELECT 1 FROM pg_database WHERE datname = {lit(db)})\\gexec")
print(f"ALTER DATABASE {ident(db)} OWNER TO {ident(user)};")
PYSYNCDB
  sql_container="/tmp/garmetix-sync-db-password.sql"
  docker cp "$sql_host" "$pg_container:$sql_container"
  rm -f "$sql_host"

  # Official postgres Docker image may not create a database role named "postgres"
  # when POSTGRES_USER is set to "garmetix". Connect using the configured
  # superuser first; fall back to postgres only for older/custom volumes.
  if ! docker exec "$pg_container" psql -v ON_ERROR_STOP=1 -U "$pg_user" -d postgres -f "$sql_container"; then
    warn "psql as '$pg_user' failed; trying fallback role 'postgres'."
    docker exec "$pg_container" psql -v ON_ERROR_STOP=1 -U postgres -d postgres -f "$sql_container"
  fi

  # Make sure the app user can use and create objects in public schema.
  sql_host="$(mktemp)"
  python3 - "$pg_user" > "$sql_host" <<'PYGRANTDB'
import sys
user = sys.argv[1]
def ident(s):
    return '"' + s.replace('"', '""') + '"'
print(f"GRANT ALL ON SCHEMA public TO {ident(user)};")
print(f"ALTER SCHEMA public OWNER TO {ident(user)};")
PYGRANTDB
  docker cp "$sql_host" "$pg_container:/tmp/garmetix-grant-public.sql"
  rm -f "$sql_host"
  docker exec "$pg_container" psql -v ON_ERROR_STOP=1 -U "$pg_user" -d "$pg_db" -f /tmp/garmetix-grant-public.sql || \
    docker exec "$pg_container" psql -v ON_ERROR_STOP=1 -U postgres -d "$pg_db" -f /tmp/garmetix-grant-public.sql || true
}

wait_postgres_container 60
sync_postgres_password

log "Building and starting Garmetix app services"
compose up -d --build --force-recreate

wait_url(){
  local name="$1" url="$2" attempts="${3:-120}" i code
  log "Waiting for $name: $url"
  for i in $(seq 1 "$attempts"); do
    code="$(curl -k -sS -o /dev/null -w '%{http_code}' "$url" 2>/tmp/garmetix-curl.err || true)"
    if [[ "$code" =~ ^(200|204|301|302|401|403)$ ]]; then
      log "$name is responding with HTTP=$code"
      return 0
    fi
    if (( i == 1 || i % 10 == 0 )); then
      warn "attempt $i/$attempts: $name not ready, HTTP=$code $(cat /tmp/garmetix-curl.err 2>/dev/null || true)"
    fi
    sleep 2
  done
  warn "$name did not respond in time"
  return 1
}

API_PORT="$(grep -E '^API_PORT=' .env.production | tail -1 | cut -d= -f2- || true)"; API_PORT="${API_PORT:-5080}"
WEB_PORT="$(grep -E '^WEB_PORT=' .env.production | tail -1 | cut -d= -f2- || true)"; WEB_PORT="${WEB_PORT:-3000}"

wait_url "API direct health endpoint" "http://127.0.0.1:${API_PORT}/api/health" 120 || true
wait_url "Nuxt web proxy health endpoint" "http://127.0.0.1:${WEB_PORT}/api/health" 120 || true

compose ps

if docker ps --format '{{.Names}} {{.Status}}' | grep -q '^garmetix-cloudflared-1 .*Restarting'; then
  warn "cloudflared is restarting. Last logs:"
  docker logs --tail=80 garmetix-cloudflared-1 || true
fi

DOTMATRIX_BRIDGE_AUTO_INSTALL="$(grep -E '^DOTMATRIX_BRIDGE_AUTO_INSTALL=' .env.production | tail -1 | cut -d= -f2- || true)"
DOTMATRIX_BRIDGE_AUTO_INSTALL="${DOTMATRIX_BRIDGE_AUTO_INSTALL:-false}"
DOTMATRIX_PRINTER_NAME="$(grep -E '^DOTMATRIX_PRINTER_NAME=' .env.production | tail -1 | cut -d= -f2- || true)"
DOTMATRIX_PRINTER_NAME="${DOTMATRIX_PRINTER_NAME:-EPSON_LX810}"
if [[ "${DOTMATRIX_BRIDGE_AUTO_INSTALL,,}" == "true" || "${DOTMATRIX_BRIDGE_AUTO_INSTALL}" == "1" || "${DOTMATRIX_BRIDGE_AUTO_INSTALL,,}" == "yes" ]]; then
  log "Installing/updating Garmetix DotMatrix Bridge service for printer ${DOTMATRIX_PRINTER_NAME}"
  run_sudo env PROJECT_DIR="$RELEASE" GARMETIX_DOTMATRIX_PRINTER="$DOTMATRIX_PRINTER_NAME" "$RELEASE/deploy/install-dotmatrix-bridge-ubuntu.sh" || warn "DotMatrix Bridge install failed. Run it manually: sudo PROJECT_DIR=$RELEASE GARMETIX_DOTMATRIX_PRINTER=$DOTMATRIX_PRINTER_NAME $RELEASE/deploy/install-dotmatrix-bridge-ubuntu.sh"
else
  log "DotMatrix Bridge auto-install is off. To install manually, run: sudo PROJECT_DIR=$RELEASE GARMETIX_DOTMATRIX_PRINTER=$DOTMATRIX_PRINTER_NAME $RELEASE/deploy/install-dotmatrix-bridge-ubuntu.sh"
fi
