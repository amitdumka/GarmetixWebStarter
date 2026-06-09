#!/usr/bin/env bash
set -euo pipefail

ENV_FILE="${1:-.env.production}"

if [[ ! -f "$ENV_FILE" ]]; then
  echo "Missing env file: $ENV_FILE" >&2
  echo "Copy .env.production.example first." >&2
  exit 1
fi

random_secret() {
  if command -v openssl >/dev/null 2>&1; then
    openssl rand -base64 48 | tr -d '\n='
  else
    LC_ALL=C tr -dc 'A-Za-z0-9' </dev/urandom | head -c 64
  fi
}

set_env_value() {
  local key="$1"
  local value="$2"
  if grep -q "^${key}=" "$ENV_FILE"; then
    sed -i.bak "s#^${key}=.*#${key}=${value}#" "$ENV_FILE"
  else
    printf '\n%s=%s\n' "$key" "$value" >> "$ENV_FILE"
  fi
}

current_postgres_password="$(grep '^POSTGRES_PASSWORD=' "$ENV_FILE" | cut -d= -f2- || true)"
current_jwt="$(grep '^JWT_SIGNING_KEY=' "$ENV_FILE" | cut -d= -f2- || true)"

if [[ -z "$current_postgres_password" || "$current_postgres_password" == REPLACE_* || "$current_postgres_password" == change-this* ]]; then
  set_env_value POSTGRES_PASSWORD "$(random_secret)"
  echo "POSTGRES_PASSWORD generated."
else
  echo "POSTGRES_PASSWORD already set; left unchanged."
fi

if [[ -z "$current_jwt" || "$current_jwt" == REPLACE_* || "$current_jwt" == change-this* || ${#current_jwt} -lt 32 ]]; then
  set_env_value JWT_SIGNING_KEY "$(random_secret)$(random_secret)"
  echo "JWT_SIGNING_KEY generated."
else
  echo "JWT_SIGNING_KEY already set; left unchanged."
fi

rm -f "${ENV_FILE}.bak"
chmod 600 "$ENV_FILE" || true

echo "Secret generation completed for $ENV_FILE. Review PUBLIC_DOMAIN, SMTP, CORS, backup, and tunnel settings before deployment."
