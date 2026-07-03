#!/usr/bin/env bash
set -Eeuo pipefail
ROOT="${1:-/opt/garmetix/current}"
cd "$ROOT"
ENV_FILE=".env.production"
PG_CONTAINER="${PG_CONTAINER:-garmetix-postgres-1}"
get_env_value(){ local key="$1"; grep -E "^${key}=" "$ENV_FILE" | tail -1 | cut -d= -f2- | sed -e 's/^"//' -e 's/"$//' -e "s/^'//" -e "s/'$//"; }
PG_DB="$(get_env_value POSTGRES_DB || true)"; PG_DB="${PG_DB:-garmetix}"
PG_USER="$(get_env_value POSTGRES_USER || true)"; PG_USER="${PG_USER:-garmetix}"
BACKUP_DIR="$ROOT/backups"
mkdir -p "$BACKUP_DIR"
get_app_version(){
  grep -E 'public const string Version = ' backend/Garmetix.Api/AppInfo/AppInfoEndpoints.cs 2>/dev/null | head -1 | sed -E 's/.*Version = "([^"]+)".*/\1/'
}
safe_slug(){
  printf '%s' "${1:-Garmetix}" | tr -cd '[:alnum:]'
}
get_company_name(){
  local name
  name="$(docker exec "$PG_CONTAINER" psql -U "$PG_USER" -d "$PG_DB" -Atc 'select coalesce(nullif("Name", '\''\''), '\''Garmetix'\'') from "Companies" order by "CreatedAt" nulls last limit 1' 2>/dev/null | head -1 || true)"
  printf '%s' "${name:-Garmetix}"
}
next_backup_file(){
  local source="$1"
  local company app_version safe_company stamp date_part seq backup_dir count
  backup_dir="${BACKUP_DIR:-$ROOT/backups}"
  company="$(get_company_name)"
  app_version="${APP_VERSION:-$(get_app_version)}"
  app_version="${app_version:-4.11.52}"
  safe_company="$(safe_slug "$company")"
  safe_company="${safe_company:-Garmetix}"
  stamp="$(TZ=Asia/Kolkata date +%Y%m%d-%H%M%S)"
  date_part="$(TZ=Asia/Kolkata date +%Y%m%d)"
  count="$(find "$backup_dir" -type f -name "${safe_company}-Garmetix-v${app_version}-${date_part}-*-*-${source}.dump" 2>/dev/null | wc -l | tr -d ' ')"
  seq=$((count + 1))
  printf '%s/%s-Garmetix-v%s-%s-B%03d-%s.dump' "$backup_dir" "$safe_company" "$app_version" "$stamp" "$seq" "$source"
}
write_backup_manifest(){
  local backup_file="$1" source="$2" company app_version seq
  company="$(get_company_name)"
  app_version="${APP_VERSION:-$(get_app_version)}"
  app_version="${app_version:-4.11.52}"
  seq="$(basename "$backup_file" | sed -nE 's/.*-B([0-9]+)-.*/\1/p')"
  sha256sum "$backup_file" > "$backup_file.sha256"
  cat > "$backup_file.manifest.json" <<JSON
{
  "fileName": "$(basename "$backup_file")",
  "sizeBytes": $(stat -c%s "$backup_file"),
  "createdAtUtc": "$(date -u +%Y-%m-%dT%H:%M:%SZ)",
  "source": "$source",
  "database": "$PG_DB",
  "host": "$PG_CONTAINER",
  "port": 5432,
  "sha256": "$(awk '{print $1}' "$backup_file.sha256")",
  "format": "PostgreSQL custom pg_dump",
  "application": "Garmetix",
  "stage": "script backup / v${app_version}",
  "companyName": "$company",
  "appVersion": "$app_version",
  "backupDateLocal": "$(TZ=Asia/Kolkata date '+%Y-%m-%d %H:%M:%S %z')",
  "sequence": ${seq:-1}
}
JSON
}

BACKUP_FILE="$(next_backup_file admin-data)"
echo "Creating full PostgreSQL backup before admin data operation:"
echo "  $BACKUP_FILE"
docker exec "$PG_CONTAINER" pg_dump -U "$PG_USER" -d "$PG_DB" -Fc > "$BACKUP_FILE"
write_backup_manifest "$BACKUP_FILE" admin-data
echo "Backup complete."
echo "$BACKUP_FILE"
