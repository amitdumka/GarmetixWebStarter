#!/usr/bin/env bash
set -Eeuo pipefail
ROOT="${1:-/opt/garmetix/current}"
cd "$ROOT"
ENV_FILE=".env.production"
if [[ ! -f "$ENV_FILE" ]]; then
  echo "Missing $ROOT/.env.production" >&2
  exit 1
fi
get_env_value(){
  local key="$1"
  grep -E "^${key}=" "$ENV_FILE" | tail -1 | cut -d= -f2- | sed -e 's/^"//' -e 's/"$//' -e "s/^'//" -e "s/'$//"
}
PG_CONTAINER="${PG_CONTAINER:-garmetix-postgres-1}"
PG_DB="$(get_env_value POSTGRES_DB)"; PG_DB="${PG_DB:-garmetix}"
PG_USER="$(get_env_value POSTGRES_USER)"; PG_USER="${PG_USER:-garmetix}"
PG_PASS="$(get_env_value POSTGRES_PASSWORD)"
if [[ -z "$PG_PASS" ]]; then
  echo "POSTGRES_PASSWORD is empty in $ENV_FILE" >&2
  exit 1
fi
if ! docker ps --format '{{.Names}}' | grep -qx "$PG_CONTAINER"; then
  echo "Postgres container $PG_CONTAINER is not running" >&2
  exit 1
fi
SQL_HOST="$(mktemp)"
python3 - "$PG_USER" "$PG_PASS" "$PG_DB" > "$SQL_HOST" <<'PY'
import sys
user, password, db = sys.argv[1], sys.argv[2], sys.argv[3]
def lit(s): return "'" + s.replace("'", "''") + "'"
def ident(s): return '"' + s.replace('"', '""') + '"'
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
PY
docker cp "$SQL_HOST" "$PG_CONTAINER:/tmp/garmetix-fix-db-password.sql"
rm -f "$SQL_HOST"
if ! docker exec "$PG_CONTAINER" psql -v ON_ERROR_STOP=1 -U "$PG_USER" -d postgres -f /tmp/garmetix-fix-db-password.sql; then
  echo "psql as '$PG_USER' failed; trying fallback role 'postgres'." >&2
  docker exec "$PG_CONTAINER" psql -v ON_ERROR_STOP=1 -U postgres -d postgres -f /tmp/garmetix-fix-db-password.sql
fi
GRANT_HOST="$(mktemp)"
python3 - "$PG_USER" > "$GRANT_HOST" <<'PY'
import sys
user = sys.argv[1]
def ident(s): return '"' + s.replace('"', '""') + '"'
print(f"GRANT ALL ON SCHEMA public TO {ident(user)};")
print(f"ALTER SCHEMA public OWNER TO {ident(user)};")
PY
docker cp "$GRANT_HOST" "$PG_CONTAINER:/tmp/garmetix-grant-public.sql"
rm -f "$GRANT_HOST"
docker exec "$PG_CONTAINER" psql -v ON_ERROR_STOP=1 -U "$PG_USER" -d "$PG_DB" -f /tmp/garmetix-grant-public.sql || \
  docker exec "$PG_CONTAINER" psql -v ON_ERROR_STOP=1 -U postgres -d "$PG_DB" -f /tmp/garmetix-grant-public.sql || true
# Restart API after password is fixed.
COMPOSE_PROJECT_NAME="${COMPOSE_PROJECT_NAME:-garmetix}" docker compose --env-file .env.production -f deploy/docker-compose.prod.yml -f deploy/docker-compose.cloudflare.yml restart api || true
echo "Database user password aligned for '$PG_USER'. Check: docker logs --tail=80 garmetix-api-1"
