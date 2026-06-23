#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
MODULAR_ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"
REPO_ROOT="$(cd "$MODULAR_ROOT/.." && pwd)"

APP_NAME="pos"
BUILD_DIR="$MODULAR_ROOT/apps/pos/.output/public"
RELEASE_ID="$(date -u +%Y%m%d%H%M%S)"

: "${POS_DEPLOY_TARGET:=amit@192.168.11.126}"
: "${POS_DEPLOY_REMOTE_DIR:=/var/www/garmetix/pos}"
: "${POS_DEPLOY_SSH_PORT:=22}"
: "${POS_DEPLOY_KEEP_RELEASES:=5}"
: "${NUXT_PUBLIC_GARMETIX_API_BASE_URL:=http://localhost:5080/api}"
: "${NUXT_PUBLIC_GARMETIX_POS_URL:=http://localhost:3101}"

REMOTE_RELEASE_DIR="$POS_DEPLOY_REMOTE_DIR/releases/$RELEASE_ID"
REMOTE_CURRENT_DIR="$POS_DEPLOY_REMOTE_DIR/current"

need_command() {
  if ! command -v "$1" >/dev/null 2>&1; then
    echo "Missing required command: $1" >&2
    exit 1
  fi
}

need_command npm
need_command ssh
need_command rsync

echo "Garmetix POS static deploy"
echo "Target: $POS_DEPLOY_TARGET"
echo "Remote directory: $POS_DEPLOY_REMOTE_DIR"
echo "Release: $RELEASE_ID"
echo

cd "$REPO_ROOT"

echo "Building POS static output..."
NUXT_PUBLIC_GARMETIX_API_BASE_URL="$NUXT_PUBLIC_GARMETIX_API_BASE_URL" \
NUXT_PUBLIC_GARMETIX_POS_URL="$NUXT_PUBLIC_GARMETIX_POS_URL" \
npm --prefix modular run build:pos

if [ ! -d "$BUILD_DIR" ]; then
  echo "Build output was not found: $BUILD_DIR" >&2
  exit 1
fi

echo "Preparing remote release directory..."
ssh -p "$POS_DEPLOY_SSH_PORT" "$POS_DEPLOY_TARGET" "mkdir -p '$REMOTE_RELEASE_DIR'"

echo "Uploading POS files..."
rsync -az --delete -e "ssh -p $POS_DEPLOY_SSH_PORT" "$BUILD_DIR/" "$POS_DEPLOY_TARGET:$REMOTE_RELEASE_DIR/"

echo "Activating release..."
ssh -p "$POS_DEPLOY_SSH_PORT" "$POS_DEPLOY_TARGET" "
  set -e
  ln -sfn '$REMOTE_RELEASE_DIR' '$REMOTE_CURRENT_DIR'
  find '$POS_DEPLOY_REMOTE_DIR/releases' -mindepth 1 -maxdepth 1 -type d | sort -r | tail -n +$((POS_DEPLOY_KEEP_RELEASES + 1)) | xargs -r rm -rf
"

echo
echo "POS deploy completed."
echo "Serve this directory from Nginx or another static server:"
echo "  $REMOTE_CURRENT_DIR"
echo
echo "Expected public env values at build time:"
echo "  NUXT_PUBLIC_GARMETIX_API_BASE_URL=$NUXT_PUBLIC_GARMETIX_API_BASE_URL"
echo "  NUXT_PUBLIC_GARMETIX_POS_URL=$NUXT_PUBLIC_GARMETIX_POS_URL"
