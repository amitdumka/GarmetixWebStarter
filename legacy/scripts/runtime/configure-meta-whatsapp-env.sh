#!/usr/bin/env bash
set -euo pipefail

ENV_FILE="${1:-/opt/garmetix/current/.env.production}"
PUBLIC_BASE_URL="${DigitalBills__PublicBaseUrl:-https://garmetix.aadwikafashion.in}"
META_BASE_URL="${WhatsApp__MetaCloudApiBaseUrl:-https://graph.facebook.com/v20.0}"
TOKEN="${WhatsApp__MetaWebhookVerifyToken:-}"

if [[ -z "$TOKEN" ]]; then
  if command -v openssl >/dev/null 2>&1; then
    TOKEN="$(openssl rand -hex 32)"
  else
    TOKEN="GarmetixMetaWebhook$(date +%s)"
  fi
fi

sudo touch "$ENV_FILE" 2>/dev/null || touch "$ENV_FILE"
set_env() {
  local key="$1"
  local value="$2"
  if grep -qE "^${key}=" "$ENV_FILE"; then
    sudo sed -i "s|^${key}=.*|${key}=${value}|" "$ENV_FILE" 2>/dev/null || sed -i "s|^${key}=.*|${key}=${value}|" "$ENV_FILE"
  else
    printf '\n%s=%s\n' "$key" "$value" | sudo tee -a "$ENV_FILE" >/dev/null 2>&1 || printf '\n%s=%s\n' "$key" "$value" >> "$ENV_FILE"
  fi
}

set_env DigitalBills__PublicBaseUrl "$PUBLIC_BASE_URL"
set_env WhatsApp__MetaWebhookVerifyToken "$TOKEN"
set_env WhatsApp__MetaCloudApiBaseUrl "$META_BASE_URL"

echo "Updated: $ENV_FILE"
echo "DigitalBills__PublicBaseUrl=$PUBLIC_BASE_URL"
echo "WhatsApp__MetaCloudApiBaseUrl=$META_BASE_URL"
echo "WhatsApp__MetaWebhookVerifyToken=$TOKEN"
echo
echo "Use this callback URL in Meta:"
echo "${PUBLIC_BASE_URL}/api/public/digital-bill-whatsapp/webhook/meta"
echo "Use the same verify token printed above in Meta Webhooks."
