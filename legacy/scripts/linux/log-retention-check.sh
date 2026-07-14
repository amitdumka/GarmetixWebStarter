#!/usr/bin/env bash
set -euo pipefail
ENV_FILE="${1:-.env.production}"
ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
cd "${ROOT_DIR}"

echo "== Docker log retention check =="
if [[ -f "deploy/lib/env-file.sh" ]]; then
  # shellcheck source=/dev/null
  source "deploy/lib/env-file.sh"
  normalize_env_file "${ENV_FILE}" || true
fi

docker compose --env-file "${ENV_FILE}" -f docker-compose.prod.yml -f deploy/docker-compose.cloudflare.yml ps

echo
for name in garmetix-api-1 garmetix-web-1 garmetix-postgres-1 garmetix-cloudflared-1; do
  if docker inspect "${name}" >/dev/null 2>&1; then
    echo "--- ${name}"
    docker inspect --format 'driver={{.HostConfig.LogConfig.Type}} options={{json .HostConfig.LogConfig.Config}}' "${name}"
    log_path="$(docker inspect --format '{{.LogPath}}' "${name}" 2>/dev/null || true)"
    if [[ -n "${log_path}" && -f "${log_path}" ]]; then
      du -h "${log_path}" || true
    fi
  fi
done

echo
printf 'Expected json-file options: max-size=%s max-file=%s
' "${DOCKER_LOG_MAX_SIZE:-10m}" "${DOCKER_LOG_MAX_FILE:-5}"
