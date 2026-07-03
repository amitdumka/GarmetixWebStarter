#!/usr/bin/env bash
set -Eeuo pipefail
ROOT="${1:-/opt/garmetix/current}"
cd "$ROOT"
ENV_FILE=".env.production"
PG_CONTAINER="${PG_CONTAINER:-garmetix-postgres-1}"
get_env_value(){ local key="$1"; grep -E "^${key}=" "$ENV_FILE" | tail -1 | cut -d= -f2- | sed -e 's/^"//' -e 's/"$//' -e "s/^'//" -e "s/'$//"; }
PG_DB="$(get_env_value POSTGRES_DB || true)"; PG_USER="$(get_env_value POSTGRES_USER || true)"
PG_DB="${PG_DB:-garmetix}"; PG_USER="${PG_USER:-garmetix}"
BACKUP_DIR="$ROOT/backups"
REPORT_DIR="$ROOT/reports/stage11d33-backup"
mkdir -p "$BACKUP_DIR" "$REPORT_DIR"
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
  "sequence": ${seq:-1},
  "proofArchiveFileName": "$(basename "$backup_file").purchase-import-proofs.tar.gz",
  "proofArchivePresent": $(if [[ -f "$backup_file.purchase-import-proofs.tar.gz" ]]; then echo true; else echo false; fi)
}
JSON
}

create_purchase_import_proof_archive(){
  local backup_file="$1" archive_file="${backup_file}.purchase-import-proofs.tar.gz" volume_name project_name
  project_name="${COMPOSE_PROJECT_NAME:-garmetix}"
  volume_name="${project_name}_garmetix_app_data"
  if docker volume inspect "$volume_name" >/dev/null 2>&1; then
    docker run --rm -v "${volume_name}:/appdata:ro" -v "$BACKUP_DIR:/backup" alpine sh -lc 'if [ -d /appdata/purchase-imports ] && [ "$(find /appdata/purchase-imports -type f | head -1)" ]; then tar -czf "/backup/'"$(basename "$archive_file")"'" -C /appdata purchase-imports; fi'
  elif [[ -d "$ROOT/data/purchase-imports" ]]; then
    tar -czf "$archive_file" -C "$ROOT/data" purchase-imports
  fi
  if [[ -f "$archive_file" ]]; then
    sha256sum "$archive_file" > "$archive_file.sha256"
    echo "Created purchase invoice proof archive: $archive_file"
  fi
}

BACKUP_FILE="$(next_backup_file manual)"
MANIFEST="$BACKUP_FILE.manifest.json"
SHA_FILE="$BACKUP_FILE.sha256"
echo "Creating PostgreSQL backup: $BACKUP_FILE"
docker exec "$PG_CONTAINER" pg_dump -U "$PG_USER" -d "$PG_DB" -Fc > "$BACKUP_FILE"
create_purchase_import_proof_archive "$BACKUP_FILE"
write_backup_manifest "$BACKUP_FILE" manual
cp "$MANIFEST" "$REPORT_DIR/latest-backup-manifest.json"
echo "Backup completed."
echo "$BACKUP_FILE"
