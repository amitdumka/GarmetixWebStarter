#!/usr/bin/env bash
# Portable helpers for .env files. Avoids sed -i/chmod hard failures on WSL /mnt/c.

is_windows_mount_path() {
  local path="${1:-}"
  [[ "$path" == /mnt/c/* || "$path" == /mnt/d/* || "$path" == /mnt/e/* || "$path" == /mnt/f/* ]]
}

env_format_value() {
  local value="${1:-}"
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
  tmp="$(mktemp "$(dirname "$file")/.${key}.tmp.XXXXXX")"
  awk -v key="$key" -v line="${key}=${formatted}" '
    BEGIN { done=0 }
    $0 ~ "^" key "=" { if (!done) { print line; done=1 } ; next }
    { print }
    END { if (!done) print line }
  ' "$file" > "$tmp"
  cat "$tmp" > "$file"
  rm -f "$tmp"
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
