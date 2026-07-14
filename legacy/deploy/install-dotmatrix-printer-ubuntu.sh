#!/usr/bin/env bash
set -Eeuo pipefail

# Backward-compatible wrapper retained for old instructions.
# Production v4.12.47+ uses the DB-backed Ubuntu host-side DotMatrix Bridge, not the old spool-file timer.
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
exec "$SCRIPT_DIR/install-dotmatrix-bridge-ubuntu.sh" "$@"
