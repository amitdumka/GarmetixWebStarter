#!/usr/bin/env bash
# Standalone health check for the optional Postfix relay - safe to run from cron/monitoring.
# Never wired into the main application's health checks or deploy scripts; this relay is
# entirely optional infrastructure the module does not depend on.
set -euo pipefail

CONTAINER_NAME="garmetix-communication-postfix-relay"

if ! docker inspect "$CONTAINER_NAME" >/dev/null 2>&1; then
    echo "FAIL: container $CONTAINER_NAME does not exist (relay is not deployed here)."
    exit 1
fi

status=$(docker inspect -f '{{.State.Health.Status}}' "$CONTAINER_NAME" 2>/dev/null || echo "unknown")
if [ "$status" != "healthy" ]; then
    echo "FAIL: $CONTAINER_NAME health status is '$status'."
    exit 1
fi

echo "OK: $CONTAINER_NAME is healthy."
