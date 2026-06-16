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

if [[ -f "${ROOT_DIR}/deploy/macmini.env" ]]; then
  # shellcheck disable=SC1091
  source "${ROOT_DIR}/deploy/macmini.env"
fi
if [[ -f "$ENV_FILE" ]]; then
  set -a
  # shellcheck disable=SC1090
  source "$ENV_FILE"
  set +a
fi
DOMAIN="${DOMAIN:-${PUBLIC_DOMAIN:-garmetix.aadwikafashion.in}}"
TUNNEL_NAME="${CLOUDFLARE_TUNNEL_NAME:-garmetix-macmini}"

require_value() {
  local name="$1" value="${!1:-}"
  if [[ -z "$value" || "$value" == CHANGE_ME* ]]; then
    echo "Set ${name} in deploy/macmini.env before running Cloudflare automation." >&2
    exit 1
  fi
}

require_value CLOUDFLARE_API_TOKEN
require_value CLOUDFLARE_ACCOUNT_ID
require_value CLOUDFLARE_ZONE_ID

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

set_env_var() {
  local file="$1" key="$2" value="$3"
  local escaped
  escaped="$(printf '%s' "$value" | sed 's/[&/\\]/\\&/g')"
  if grep -qE "^${key}=" "$file" 2>/dev/null; then
    sed -i "s/^${key}=.*/${key}=${escaped}/" "$file"
  else
    printf '%s=%s\n' "$key" "$value" >>"$file"
  fi
}

"${ROOT_DIR}/deploy/create-production-env.sh"

TUNNEL_ID="${CLOUDFLARE_TUNNEL_ID:-}"
TUNNEL_TOKEN="${CLOUDFLARE_TUNNEL_TOKEN:-}"

if [[ -z "$TUNNEL_ID" || -z "$TUNNEL_TOKEN" || "$TUNNEL_TOKEN" == CHANGE_ME* ]]; then
  echo "Creating Cloudflare Tunnel '${TUNNEL_NAME}'..."
  payload="$(jq -n --arg name "$TUNNEL_NAME" '{name:$name, config_src:"cloudflare"}')"
  if ! response="$(cf POST "/accounts/${CLOUDFLARE_ACCOUNT_ID}/cfd_tunnel" "$payload" 2>/tmp/garmetix-cf-create.err)"; then
    echo "Tunnel name may already exist; creating a timestamped tunnel instead." >&2
    TUNNEL_NAME="${TUNNEL_NAME}-$(date +%Y%m%d%H%M%S)"
    payload="$(jq -n --arg name "$TUNNEL_NAME" '{name:$name, config_src:"cloudflare"}')"
    response="$(cf POST "/accounts/${CLOUDFLARE_ACCOUNT_ID}/cfd_tunnel" "$payload")"
  fi
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

set_env_var "$ENV_FILE" CLOUDFLARE_TUNNEL_ID "$TUNNEL_ID"
set_env_var "$ENV_FILE" CLOUDFLARE_TUNNEL_TOKEN "$TUNNEL_TOKEN"
set_env_var "$ENV_FILE" PUBLIC_DOMAIN "$DOMAIN"
set_env_var "$ENV_FILE" PUBLIC_HTTPS_URL "https://${DOMAIN}"
set_env_var "$ENV_FILE" NUXT_PUBLIC_SITE_URL "https://${DOMAIN}"
set_env_var "$ENV_FILE" PASSWORD_RESET_FRONTEND_BASE_URL "https://${DOMAIN}"
set_env_var "$ENV_FILE" CORS_ALLOWED_ORIGINS "https://${DOMAIN}"
set_env_var "$ENV_FILE" API_BASE_URL "https://${DOMAIN}/api"
chmod 600 "$ENV_FILE"

echo "Cloudflare Tunnel ready. Tunnel ID: ${TUNNEL_ID}"
echo "The token was saved to .env.production. Keep that file private."
