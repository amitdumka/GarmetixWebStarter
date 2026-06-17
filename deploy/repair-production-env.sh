#!/usr/bin/env bash
set -Eeuo pipefail
ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
# shellcheck source=deploy/lib/env-file.sh
source "${ROOT_DIR}/deploy/lib/env-file.sh"
ENV_FILE="${ROOT_DIR}/.env.production"
if [[ ! -f "$ENV_FILE" ]]; then
  echo "Missing $ENV_FILE" >&2
  exit 1
fi
# Fix old generated value that broke Bash source parsing.
if grep -q '^GSTIN_LOOKUP_SOURCE_NAME=Configured GSTIN Provider$' "$ENV_FILE"; then
  set_env_var "$ENV_FILE" GSTIN_LOOKUP_SOURCE_NAME "Configured GSTIN Provider"
fi
# Show suspicious non-comment lines without KEY=value format.
awk 'BEGIN{bad=0} /^[[:space:]]*#/ || /^[[:space:]]*$/ {next} !/^[A-Za-z_][A-Za-z0-9_]*=/ {print "Invalid .env line " NR ": " $0; bad=1} END{exit bad}' "$ENV_FILE"
echo "Repaired and checked $ENV_FILE"
