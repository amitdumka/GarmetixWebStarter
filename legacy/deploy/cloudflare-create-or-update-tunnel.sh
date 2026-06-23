#!/usr/bin/env bash
set -Eeuo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
ENV_FILE="${ROOT_DIR}/.env.production"
DOMAIN="${DOMAIN:-garmetix.aadwikafashion.in}"
TUNNEL_NAME="${CLOUDFLARE_TUNNEL_NAME:-garmetix-macmini}"
CF_API="https://api.cloudflare.com/client/v4"

need() {
  command -v "$1" >/dev/null 2>&1 || { echo "Missing command: $1" >&2; exit 1; }
}
need curl
need jq

# shellcheck source=deploy/lib/env-file.sh
source "${ROOT_DIR}/deploy/lib/env-file.sh"

if [[ -f "${ROOT_DIR}/deploy/macmini.env" ]]; then
  normalize_env_file "${ROOT_DIR}/deploy/macmini.env"
  # shellcheck disable=SC1091
  source "${ROOT_DIR}/deploy/macmini.env"
fi
if [[ -f "$ENV_FILE" ]]; then
  normalize_env_file "$ENV_FILE"
  : "${PUBLIC_DOMAIN:=$(dotenv_get "$ENV_FILE" PUBLIC_DOMAIN "")}"
  : "${PUBLIC_HTTPS_URL:=$(dotenv_get "$ENV_FILE" PUBLIC_HTTPS_URL "")}"
  : "${CLOUDFLARE_ACCOUNT_ID:=$(dotenv_get "$ENV_FILE" CLOUDFLARE_ACCOUNT_ID "")}"
  : "${CLOUDFLARE_ZONE_ID:=$(dotenv_get "$ENV_FILE" CLOUDFLARE_ZONE_ID "")}"
  : "${CLOUDFLARE_TUNNEL_ID:=$(dotenv_get "$ENV_FILE" CLOUDFLARE_TUNNEL_ID "")}"
  : "${CLOUDFLARE_TUNNEL_TOKEN:=$(dotenv_get "$ENV_FILE" CLOUDFLARE_TUNNEL_TOKEN "")}"
fi
DOMAIN="${DOMAIN:-${PUBLIC_DOMAIN:-garmetix.aadwikafashion.in}}"
TUNNEL_NAME="${CLOUDFLARE_TUNNEL_NAME:-garmetix-macmini}"

is_blank_or_placeholder() {
  local value="${1:-}"
  [[ -z "$value" || "$value" == CHANGE_ME* || "$value" == *CHANGE_ME* ]]
}

is_probably_cloudflare_id() {
  local value="${1:-}"
  [[ "$value" =~ ^[0-9a-fA-F]{32}$ ]]
}

require_token() {
  if is_blank_or_placeholder "${CLOUDFLARE_API_TOKEN:-}"; then
    echo "Set CLOUDFLARE_API_TOKEN in deploy/macmini.env before running Cloudflare automation." >&2
    exit 1
  fi
}

cf() {
  local method="$1" url="$2" data="${3:-}"
  if [[ -n "$data" ]]; then
    curl -fsS "${CF_API}${url}" \
      --request "$method" \
      --header "Authorization: Bearer ${CLOUDFLARE_API_TOKEN}" \
      --header "Content-Type: application/json" \
      --data "$data"
  else
    curl -fsS "${CF_API}${url}" \
      --request "$method" \
      --header "Authorization: Bearer ${CLOUDFLARE_API_TOKEN}"
  fi
}

root_domain_from_hostname() {
  local host="$1"
  # For this deployment domain this returns aadwikafashion.in from garmetix.aadwikafashion.in.
  # If you later use a different multi-level TLD, set CLOUDFLARE_ZONE_ID manually.
  awk -F. '{print $(NF-1)"."$NF}' <<<"$host"
}

resolve_account_id() {
  if ! is_blank_or_placeholder "${CLOUDFLARE_ACCOUNT_ID:-}" && is_probably_cloudflare_id "${CLOUDFLARE_ACCOUNT_ID}"; then
    return 0
  fi

  if ! is_blank_or_placeholder "${CLOUDFLARE_ACCOUNT_ID:-}"; then
    echo "Ignoring invalid CLOUDFLARE_ACCOUNT_ID value. A valid Cloudflare ID is usually 32 hex characters." >&2
  else
    echo "CLOUDFLARE_ACCOUNT_ID is blank; trying to auto-detect it from the API token..."
  fi

  local accounts count detected err_file
  err_file="$(safe_temp_file garmetix-cf-accounts)"
  if ! accounts="$(cf GET "/accounts" 2>"$err_file")"; then
    echo "Could not auto-detect Cloudflare account ID." >&2
    echo "Fix: open Cloudflare dashboard -> right sidebar/API section -> copy Account ID, then set CLOUDFLARE_ACCOUNT_ID in deploy/macmini.env." >&2
    echo "Cloudflare API error:" >&2
    cat "$err_file" >&2 || true
    rm -f "$err_file"
    exit 1
  fi
  rm -f "$err_file"

  count="$(printf '%s' "$accounts" | jq '.result | length')"
  if [[ "$count" -lt 1 ]]; then
    echo "No Cloudflare accounts were visible to this token. Add Account Read / Tunnel permissions or paste Account ID manually." >&2
    exit 1
  fi
  if [[ "$count" -gt 1 ]]; then
    echo "Multiple Cloudflare accounts are visible. Using the first one. Paste CLOUDFLARE_ACCOUNT_ID manually if this is not correct." >&2
  fi
  detected="$(printf '%s' "$accounts" | jq -r '.result[0].id')"
  if ! is_probably_cloudflare_id "$detected"; then
    echo "Cloudflare returned an invalid account id: $detected" >&2
    exit 1
  fi
  CLOUDFLARE_ACCOUNT_ID="$detected"
  export CLOUDFLARE_ACCOUNT_ID
  echo "Detected Cloudflare Account ID: ${CLOUDFLARE_ACCOUNT_ID}"
}

resolve_zone_id() {
  if ! is_blank_or_placeholder "${CLOUDFLARE_ZONE_ID:-}" && is_probably_cloudflare_id "${CLOUDFLARE_ZONE_ID}"; then
    return 0
  fi

  if ! is_blank_or_placeholder "${CLOUDFLARE_ZONE_ID:-}"; then
    echo "Ignoring invalid CLOUDFLARE_ZONE_ID value. A valid Cloudflare Zone ID is usually 32 hex characters." >&2
  else
    echo "CLOUDFLARE_ZONE_ID is blank; trying to auto-detect it from the domain..."
  fi

  local zone_name zones detected err_file
  zone_name="$(root_domain_from_hostname "$DOMAIN")"
  err_file="$(safe_temp_file garmetix-cf-zones)"
  if ! zones="$(cf GET "/zones?name=${zone_name}" 2>"$err_file")"; then
    echo "Could not auto-detect Cloudflare Zone ID for ${zone_name}." >&2
    echo "Fix: open Cloudflare dashboard -> Websites -> ${zone_name} -> Overview -> copy Zone ID." >&2
    echo "Cloudflare API error:" >&2
    cat "$err_file" >&2 || true
    rm -f "$err_file"
    exit 1
  fi
  rm -f "$err_file"
  detected="$(printf '%s' "$zones" | jq -r '.result[0].id // empty')"
  if ! is_probably_cloudflare_id "$detected"; then
    echo "Could not find a valid Zone ID for ${zone_name}. Paste CLOUDFLARE_ZONE_ID manually in deploy/macmini.env." >&2
    exit 1
  fi
  CLOUDFLARE_ZONE_ID="$detected"
  export CLOUDFLARE_ZONE_ID
  echo "Detected Cloudflare Zone ID: ${CLOUDFLARE_ZONE_ID}"
}

verify_token() {
  echo "Verifying Cloudflare API token..."
  local err_file
  err_file="$(safe_temp_file garmetix-cf-verify)"
  if ! verify_response="$(cf GET "/user/tokens/verify" 2>"$err_file")"; then
    echo "Cloudflare API token verification failed." >&2
    echo "Make sure deploy/macmini.env contains a Cloudflare API token, not a tunnel token." >&2
    cat "$err_file" >&2 || true
    rm -f "$err_file"
    exit 1
  fi
  rm -f "$err_file"
  if [[ "$(printf '%s' "$verify_response" | jq -r '.success')" != "true" ]]; then
    echo "Cloudflare API token verification returned failure:" >&2
    printf '%s\n' "$verify_response" >&2
    exit 1
  fi
}

require_token
verify_token
resolve_account_id
resolve_zone_id

"${ROOT_DIR}/deploy/create-production-env.sh"

TUNNEL_ID="${CLOUDFLARE_TUNNEL_ID:-}"
TUNNEL_TOKEN="${CLOUDFLARE_TUNNEL_TOKEN:-}"

if [[ -z "$TUNNEL_ID" || -z "$TUNNEL_TOKEN" || "$TUNNEL_TOKEN" == CHANGE_ME* ]]; then
  echo "Creating Cloudflare Tunnel '${TUNNEL_NAME}'..."
  payload="$(jq -n --arg name "$TUNNEL_NAME" '{name:$name, config_src:"cloudflare"}')"
  create_err="$(safe_temp_file garmetix-cf-create)"
  if ! response="$(cf POST "/accounts/${CLOUDFLARE_ACCOUNT_ID}/cfd_tunnel" "$payload" 2>"$create_err")"; then
    echo "Tunnel name may already exist; creating a timestamped tunnel instead." >&2
    cat "$create_err" >&2 || true
    rm -f "$create_err"
    TUNNEL_NAME="${TUNNEL_NAME}-$(date +%Y%m%d%H%M%S)"
    payload="$(jq -n --arg name "$TUNNEL_NAME" '{name:$name, config_src:"cloudflare"}')"
    response="$(cf POST "/accounts/${CLOUDFLARE_ACCOUNT_ID}/cfd_tunnel" "$payload")"
  fi
  rm -f "${create_err:-}"
  TUNNEL_ID="$(printf '%s' "$response" | jq -r '.result.id')"
  TUNNEL_TOKEN="$(printf '%s' "$response" | jq -r '.result.token')"
fi

if [[ -z "$TUNNEL_ID" || "$TUNNEL_ID" == "null" || -z "$TUNNEL_TOKEN" || "$TUNNEL_TOKEN" == "null" ]]; then
  echo "Cloudflare did not return tunnel id/token." >&2
  exit 1
fi

echo "Configuring tunnel ingress for ${DOMAIN} -> http://web:3000"
ingress_payload="$(jq -n --arg host "$DOMAIN" '{config:{ingress:[{hostname:$host,service:"http://web:3000",originRequest:{}},{service:"http_status:404"}]}}')"
cf PUT "/accounts/${CLOUDFLARE_ACCOUNT_ID}/cfd_tunnel/${TUNNEL_ID}/configurations" "$ingress_payload" >/dev/null

echo "Creating/updating Cloudflare DNS CNAME ${DOMAIN} -> ${TUNNEL_ID}.cfargotunnel.com"
record_lookup="$(cf GET "/zones/${CLOUDFLARE_ZONE_ID}/dns_records?type=CNAME&name=${DOMAIN}")"
record_id="$(printf '%s' "$record_lookup" | jq -r '.result[0].id // empty')"
record_payload="$(jq -n --arg name "$DOMAIN" --arg content "${TUNNEL_ID}.cfargotunnel.com" '{type:"CNAME", proxied:true, name:$name, content:$content, ttl:1}')"
if [[ -n "$record_id" ]]; then
  cf PUT "/zones/${CLOUDFLARE_ZONE_ID}/dns_records/${record_id}" "$record_payload" >/dev/null
else
  cf POST "/zones/${CLOUDFLARE_ZONE_ID}/dns_records" "$record_payload" >/dev/null
fi

set_env_var "$ENV_FILE" CLOUDFLARE_ACCOUNT_ID "$CLOUDFLARE_ACCOUNT_ID"
set_env_var "$ENV_FILE" CLOUDFLARE_ZONE_ID "$CLOUDFLARE_ZONE_ID"
set_env_var "$ENV_FILE" CLOUDFLARE_TUNNEL_ID "$TUNNEL_ID"
set_env_var "$ENV_FILE" CLOUDFLARE_TUNNEL_TOKEN "$TUNNEL_TOKEN"
set_env_var "$ENV_FILE" PUBLIC_DOMAIN "$DOMAIN"
set_env_var "$ENV_FILE" PUBLIC_HTTPS_URL "https://${DOMAIN}"
set_env_var "$ENV_FILE" NUXT_PUBLIC_SITE_URL "https://${DOMAIN}"
set_env_var "$ENV_FILE" PASSWORD_RESET_FRONTEND_BASE_URL "https://${DOMAIN}"
set_env_var "$ENV_FILE" CORS_ALLOWED_ORIGINS "https://${DOMAIN}"
set_env_var "$ENV_FILE" API_BASE_URL "https://${DOMAIN}/api"
chmod_private_if_possible "$ENV_FILE"

# Save resolved IDs back into the local deploy config, but do not rewrite the token.
if [[ -f "${ROOT_DIR}/deploy/macmini.env" ]]; then
  set_env_var "${ROOT_DIR}/deploy/macmini.env" CLOUDFLARE_ACCOUNT_ID "$CLOUDFLARE_ACCOUNT_ID"
  set_env_var "${ROOT_DIR}/deploy/macmini.env" CLOUDFLARE_ZONE_ID "$CLOUDFLARE_ZONE_ID"
fi

echo "Cloudflare Tunnel ready. Tunnel ID: ${TUNNEL_ID}"
echo "The tunnel token was saved to .env.production. Keep that file private."
