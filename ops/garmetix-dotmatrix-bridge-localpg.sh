#!/usr/bin/env bash
set -Eeuo pipefail

ENV_FILE="${GARMETIX_DOTMATRIX_ENV:-/etc/garmetix-dotmatrix.env}"

load_dotenv_safely() {
  local file="$1" key value
  [[ -f "$file" ]] || return 0
  while IFS='=' read -r key value || [[ -n "${key:-}" ]]; do
    key="${key%%[[:space:]]*}"
    [[ -z "${key:-}" || "${key:0:1}" == "#" ]] && continue
    [[ "$key" =~ ^[A-Za-z_][A-Za-z0-9_]*$ ]] || continue
    value="${value:-}"
    value="${value%$'\r'}"
    if [[ "${value:0:1}" == '"' && "${value: -1}" == '"' ]]; then
      value="${value:1:${#value}-2}"
    elif [[ "${value:0:1}" == "'" && "${value: -1}" == "'" ]]; then
      value="${value:1:${#value}-2}"
    fi
    export "$key=$value"
  done < "$file"
}

load_dotenv_safely "$ENV_FILE"

DB_HOST="${GARMETIX_DOTMATRIX_DB_HOST:-127.0.0.1}"
DB_PORT="${GARMETIX_DOTMATRIX_DB_PORT:-5432}"
DB_NAME="${GARMETIX_DOTMATRIX_DB_NAME:-garmetix_srp}"
DB_USER="${GARMETIX_DOTMATRIX_DB_USER:-garmetix}"
DB_PASSWORD="${GARMETIX_DOTMATRIX_DB_PASSWORD:-}"
PRINTER_NAME="${GARMETIX_DOTMATRIX_PRINTER:-EPSON_LX310}"
LP_OPTIONS="${GARMETIX_DOTMATRIX_LP_OPTIONS:--o raw}"
DEFAULT_ENABLED="${GARMETIX_DOTMATRIX_DEFAULT_ENABLED:-false}"
DEFAULT_OUTPUT_MODE="${GARMETIX_DOTMATRIX_DEFAULT_OUTPUT_MODE:-BridgeService}"
POLL_SECONDS="${GARMETIX_DOTMATRIX_POLL_SECONDS:-2}"
BATCH_SIZE="${GARMETIX_DOTMATRIX_BATCH_SIZE:-20}"
RETRY_LIMIT="${GARMETIX_DOTMATRIX_RETRY_LIMIT:-10}"
TMP_DIR="${GARMETIX_DOTMATRIX_TMP_DIR:-/tmp/garmetix-dotmatrix}"
NORMALIZE_CRLF="${GARMETIX_DOTMATRIX_NORMALIZE_CRLF:-true}"
RESET_BEFORE_JOB="${GARMETIX_DOTMATRIX_RESET_BEFORE_JOB:-true}"
LOCK_FILE="${GARMETIX_DOTMATRIX_LOCK_FILE:-/run/garmetix-dotmatrix-bridge.lock}"

mkdir -p "$TMP_DIR"

if [[ -z "$DB_PASSWORD" ]]; then
  echo "GARMETIX_DOTMATRIX_DB_PASSWORD is empty." >&2
  exit 2
fi

if [[ -z "$PRINTER_NAME" ]]; then
  echo "GARMETIX_DOTMATRIX_PRINTER is empty. Default should be EPSON_LX310." >&2
  exit 2
fi

psql_db() {
  PGPASSWORD="$DB_PASSWORD" psql -v ON_ERROR_STOP=1 -h "$DB_HOST" -p "$DB_PORT" -U "$DB_USER" -d "$DB_NAME" "$@"
}

sql_escape() {
  printf "%s" "${1:-}" | sed "s/'/''/g"
}

update_failed() {
  local id="$1" message="$2" safe
  safe="$(sql_escape "${message:0:480}")"
  psql_db -qAt -c "UPDATE \"DotMatrixPrintQueueEntries\" SET \"Status\"='Failed', \"RetryCount\"=\"RetryCount\"+1, \"UpdatedAt\"=now(), \"ErrorMessage\"='${safe}' WHERE \"Id\"='${id}'::uuid;" >/dev/null
}

update_printed() {
  local id="$1"
  psql_db -qAt -c "UPDATE \"DotMatrixPrintQueueEntries\" SET \"Status\"='Printed', \"PrintedAtUtc\"=now(), \"UpdatedAt\"=now(), \"ErrorMessage\"=NULL WHERE \"Id\"='${id}'::uuid;" >/dev/null
}

claim_one() {
  local default_enabled_sql="false"
  case "${DEFAULT_ENABLED,,}" in
    true|1|yes|y) default_enabled_sql="true" ;;
  esac
  local retry_limit_int batch_int default_printer default_output
  retry_limit_int="$(( RETRY_LIMIT + 0 ))"
  batch_int=1
  default_printer="$(sql_escape "$PRINTER_NAME")"
  default_output="$(sql_escape "$DEFAULT_OUTPUT_MODE")"
  psql_db -qAt -F $'\t' -c "
WITH candidate AS (
    SELECT
        q.\"Id\",
        COALESCE(NULLIF(q.\"PrinterName\", ''), NULLIF(s.\"PrinterName\", ''), '${default_printer}') AS printer_name
    FROM \"DotMatrixPrintQueueEntries\" q
    LEFT JOIN \"DotMatrixPrintSettings\" s
        ON s.\"StoreId\" = q.\"StoreId\" AND s.\"Deleted\" = false
    WHERE q.\"Deleted\" = false
      AND (q.\"Status\" = 'Pending' OR (q.\"Status\" = 'Failed' AND q.\"RetryCount\" < COALESCE(s.\"RetryLimit\", ${retry_limit_int})))
      AND COALESCE(s.\"Enabled\", ${default_enabled_sql}) = true
      AND COALESCE(NULLIF(s.\"OutputMode\", ''), '${default_output}') <> 'Disabled'
      AND CASE q.\"EventType\"
            WHEN 'Transaction' THEN COALESCE(s.\"PrintTransactions\", true)
            WHEN 'AuditMutation' THEN COALESCE(s.\"PrintEditsAndDeletes\", true)
            WHEN 'DayOpening' THEN COALESCE(s.\"PrintDayOpeningClosing\", true)
            WHEN 'DayClosing' THEN COALESCE(s.\"PrintDayOpeningClosing\", true)
            ELSE true
          END
    ORDER BY q.\"BusinessDate\", q.\"SequenceNo\", q.\"CreatedAt\"
    FOR UPDATE OF q SKIP LOCKED
    LIMIT ${batch_int}
)
UPDATE \"DotMatrixPrintQueueEntries\" q
SET \"Status\"='Printing', \"UpdatedAt\"=now(), \"ErrorMessage\"=NULL
FROM candidate
WHERE q.\"Id\" = candidate.\"Id\"
RETURNING q.\"Id\"::text, candidate.printer_name, encode(convert_to(q.\"PrintableText\", 'UTF8'), 'base64');"
}

recover_stale_printing() {
  psql_db -qAt -c "UPDATE \"DotMatrixPrintQueueEntries\" SET \"Status\"='Failed', \"RetryCount\"=\"RetryCount\"+1, \"UpdatedAt\"=now(), \"ErrorMessage\"='DotMatrix Bridge restarted while entry was Printing.' WHERE \"Status\"='Printing' AND COALESCE(\"UpdatedAt\", \"CreatedAt\") < now() - interval '5 minutes';" >/dev/null || true
}

normalize_print_file() {
  local src="$1" dst="$2"
  if [[ "${RESET_BEFORE_JOB,,}" == "true" || "${RESET_BEFORE_JOB}" == "1" || "${RESET_BEFORE_JOB,,}" == "yes" ]]; then
    printf '\033@' > "$dst"
  else
    : > "$dst"
  fi

  if [[ "${NORMALIZE_CRLF,,}" == "true" || "${NORMALIZE_CRLF}" == "1" || "${NORMALIZE_CRLF,,}" == "yes" ]]; then
    awk '{ sub(/\r$/, ""); printf "%s\r\n", $0 }' "$src" >> "$dst"
  else
    cat "$src" >> "$dst"
    printf '\r\n' >> "$dst"
  fi
}

print_entry() {
  local id="$1" printer="$2" text_b64="$3"
  local raw_tmp="$TMP_DIR/${id}.raw.txt"
  local tmp="$TMP_DIR/${id}.print.txt"
  printf '%s' "$text_b64" | base64 -d > "$raw_tmp"
  normalize_print_file "$raw_tmp" "$tmp"

  local lp_args=()
  if [[ -n "${LP_OPTIONS:-}" ]]; then
    read -r -a lp_args <<< "$LP_OPTIONS"
  fi

  if lp -d "$printer" "${lp_args[@]}" "$tmp" >/tmp/garmetix-dotmatrix-lp.out 2>/tmp/garmetix-dotmatrix-lp.err; then
    update_printed "$id"
    echo "Printed dot-matrix queue $id to $printer"
    rm -f "$raw_tmp" "$tmp"
  else
    local err
    err="$(cat /tmp/garmetix-dotmatrix-lp.err 2>/dev/null || true)"
    update_failed "$id" "lp failed for printer $printer: ${err:-unknown error}"
    echo "Failed dot-matrix queue $id to $printer: ${err:-unknown error}" >&2
    rm -f "$raw_tmp" "$tmp"
  fi
}

main_loop() {
  recover_stale_printing
  while true; do
    local processed=0 i row id printer text_b64
    for i in $(seq 1 "$BATCH_SIZE"); do
      row="$(claim_one || true)"
      if [[ -z "$row" ]]; then
        break
      fi
      IFS=$'\t' read -r id printer text_b64 <<<"$row"
      if [[ -z "${id:-}" || -z "${text_b64:-}" ]]; then
        break
      fi
      print_entry "$id" "${printer:-$PRINTER_NAME}" "$text_b64"
      processed=$((processed + 1))
    done

    if [[ "${GARMETIX_DOTMATRIX_RUN_ONCE:-false}" == "true" ]]; then
      exit 0
    fi

    if [[ "$processed" -eq 0 ]]; then
      sleep "$POLL_SECONDS"
    fi
  done
}

exec 9>"$LOCK_FILE"
if ! flock -n 9; then
  echo "Another Garmetix DotMatrix Bridge is already running." >&2
  exit 0
fi

main_loop
