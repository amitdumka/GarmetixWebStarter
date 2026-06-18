#!/usr/bin/env bash
# Portable helpers for .env files. Avoids sed -i/chmod hard failures on WSL /mnt/c.

is_windows_mount_path() {
  local path="${1:-}"
  [[ "$path" == /mnt/c/* || "$path" == /mnt/d/* || "$path" == /mnt/e/* || "$path" == /mnt/f/* ]]
}

normalize_env_file() {
  local file="$1" tmp
  [[ -f "$file" ]] || return 0
  tmp="$(mktemp "$(dirname "$file")/.normalize.tmp.XXXXXX")"
  # Remove CR characters from CRLF files. Use cat > file so it works on WSL /mnt/c where mv/chmod can fail.
  tr -d '\r' < "$file" > "$tmp"
  cat "$tmp" > "$file"
  rm -f "$tmp"
}

env_format_value() {
  local value="${1:-}"
  value="${value%$'\r'}"
  # .env values with whitespace must be quoted for Bash source compatibility.
  if [[ "$value" =~ [[:space:]\#\"\'\\\$\`] ]]; then
    value="${value//\\/\\\\}"
    value="${value//\"/\\\"}"
    value="${value//\$/\\\$}"
    value="${value//\`/\\\`}"
    printf '"%s"' "$value"
  else
    printf '%s' "$value"
  fi
}

set_env_var() {
  local file="$1" key="$2" value="${3:-}" tmp formatted
  formatted="$(env_format_value "$value")"
  mkdir -p "$(dirname "$file")"
  touch "$file"
  normalize_env_file "$file"
  tmp="$(mktemp "$(dirname "$file")/.${key}.tmp.XXXXXX")"
  awk -v key="$key" -v line="${key}=${formatted}" '
    { sub(/\r$/, "", $0) }
    BEGIN { done=0 }
    $0 ~ "^" key "=" { if (!done) { print line; done=1 } ; next }
    { print }
    END { if (!done) print line }
  ' "$file" > "$tmp"
  cat "$tmp" > "$file"
  rm -f "$tmp"
  normalize_env_file "$file"
}

dotenv_get() {
  local file="$1" key="$2" default_value="${3:-}" line value
  [[ -f "$file" ]] || { printf '%s' "$default_value"; return; }
  line="$(grep -E "^${key}=" "$file" 2>/dev/null | tail -n 1 || true)"
  line="${line%$'\r'}"
  if [[ -z "$line" ]]; then
    printf '%s' "$default_value"
    return
  fi
  value="${line#*=}"
  value="${value%$'\r'}"
  if [[ "${value:0:1}" == '"' && "${value: -1}" == '"' ]]; then
    value="${value:1:${#value}-2}"
  elif [[ "${value:0:1}" == "'" && "${value: -1}" == "'" ]]; then
    value="${value:1:${#value}-2}"
  fi
  printf '%s' "${value:-$default_value}"
}

chmod_private_if_possible() {
  local path="$1"
  chmod 600 "$path" 2>/dev/null || true
}

chmod_executable_if_possible() {
  local pattern="$1"
  chmod +x $pattern 2>/dev/null || true
}

safe_temp_file() {
  local name="${1:-garmetix}"
  mktemp "${TMPDIR:-/tmp}/${name}.XXXXXX"
}
