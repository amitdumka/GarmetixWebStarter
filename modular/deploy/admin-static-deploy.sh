#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
MODULAR_ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"
REPO_ROOT="$(cd "$MODULAR_ROOT/.." && pwd)"

APP_NAME="admin"
BUILD_DIR="$MODULAR_ROOT/apps/admin/.output/public"
RELEASE_ID="$(date -u +%Y%m%d%H%M%S)"

: "${ADMIN_DEPLOY_TARGET:=amit@192.168.11.126}"
: "${ADMIN_DEPLOY_REMOTE_DIR:=/var/www/garmetix/admin}"
: "${ADMIN_DEPLOY_SSH_PORT:=22}"
: "${ADMIN_DEPLOY_KEEP_RELEASES:=5}"
: "${NUXT_PUBLIC_GARMETIX_API_BASE_URL:=http://localhost:5080/api}"
: "${NUXT_PUBLIC_GARMETIX_ADMIN_URL:=http://localhost:3105}"

REMOTE_RELEASE_DIR="$ADMIN_DEPLOY_REMOTE_DIR/releases/$RELEASE_ID"
REMOTE_CURRENT_DIR="$ADMIN_DEPLOY_REMOTE_DIR/current"

need_command() {
  if ! command -v "$1" >/dev/null 2>&1; then
    echo "Missing required command: $1" >&2
    exit 1
  fi
}

need_command npm
need_command ssh
need_command rsync

echo "Garmetix Admin static deploy"
echo "Target: $ADMIN_DEPLOY_TARGET"
echo "Remote directory: $ADMIN_DEPLOY_REMOTE_DIR"
echo "Release: $RELEASE_ID"
echo

cd "$REPO_ROOT"

echo "Building Admin static output..."
NUXT_PUBLIC_GARMETIX_API_BASE_URL="$NUXT_PUBLIC_GARMETIX_API_BASE_URL" \
NUXT_PUBLIC_GARMETIX_ADMIN_URL="$NUXT_PUBLIC_GARMETIX_ADMIN_URL" \
npm --prefix modular run build:admin

if [ ! -d "$BUILD_DIR" ]; then
  echo "Build output was not found: $BUILD_DIR" >&2
  exit 1
fi

echo "Preparing remote release directory..."
ssh -p "$ADMIN_DEPLOY_SSH_PORT" "$ADMIN_DEPLOY_TARGET" "mkdir -p '$REMOTE_RELEASE_DIR'"

echo "Uploading Admin files..."
rsync -az --delete -e "ssh -p $ADMIN_DEPLOY_SSH_PORT" "$BUILD_DIR/" "$ADMIN_DEPLOY_TARGET:$REMOTE_RELEASE_DIR/"

echo "Activating release..."
ssh -p "$ADMIN_DEPLOY_SSH_PORT" "$ADMIN_DEPLOY_TARGET" "
  set -e
  ln -sfn '$REMOTE_RELEASE_DIR' '$REMOTE_CURRENT_DIR'
  find '$ADMIN_DEPLOY_REMOTE_DIR/releases' -mindepth 1 -maxdepth 1 -type d | sort -r | tail -n +$((ADMIN_DEPLOY_KEEP_RELEASES + 1)) | xargs -r rm -rf
"

echo
echo "Admin deploy completed."
echo "Serve this directory from Nginx or another static server:"
echo "  $REMOTE_CURRENT_DIR"
echo
echo "Expected public env values at build time:"
echo "  NUXT_PUBLIC_GARMETIX_API_BASE_URL=$NUXT_PUBLIC_GARMETIX_API_BASE_URL"
echo "  NUXT_PUBLIC_GARMETIX_ADMIN_URL=$NUXT_PUBLIC_GARMETIX_ADMIN_URL"
