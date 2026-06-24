#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
MODULAR_ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"
REPO_ROOT="$(cd "$MODULAR_ROOT/.." && pwd)"

APP_NAME="main"
BUILD_DIR="$MODULAR_ROOT/apps/main/.output/public"
RELEASE_ID="$(date -u +%Y%m%d%H%M%S)"

: "${MAIN_DEPLOY_TARGET:=amit@192.168.11.126}"
: "${MAIN_DEPLOY_REMOTE_DIR:=/var/www/garmetix/main}"
: "${MAIN_DEPLOY_SSH_PORT:=22}"
: "${MAIN_DEPLOY_KEEP_RELEASES:=5}"
: "${NUXT_PUBLIC_GARMETIX_API_BASE_URL:=http://localhost:5080/api}"
: "${NUXT_PUBLIC_GARMETIX_MAIN_URL:=http://localhost:3100}"

REMOTE_RELEASE_DIR="$MAIN_DEPLOY_REMOTE_DIR/releases/$RELEASE_ID"
REMOTE_CURRENT_DIR="$MAIN_DEPLOY_REMOTE_DIR/current"

need_command() {
  if ! command -v "$1" >/dev/null 2>&1; then
    echo "Missing required command: $1" >&2
    exit 1
  fi
}

need_command npm
need_command ssh
need_command rsync

echo "Garmetix Main Back Office static deploy"
echo "Target: $MAIN_DEPLOY_TARGET"
echo "Remote directory: $MAIN_DEPLOY_REMOTE_DIR"
echo "Release: $RELEASE_ID"
echo

cd "$REPO_ROOT"

echo "Building Main Back Office static output..."
NUXT_PUBLIC_GARMETIX_API_BASE_URL="$NUXT_PUBLIC_GARMETIX_API_BASE_URL" \
NUXT_PUBLIC_GARMETIX_MAIN_URL="$NUXT_PUBLIC_GARMETIX_MAIN_URL" \
npm --prefix modular run build:main

if [ ! -d "$BUILD_DIR" ]; then
  echo "Build output was not found: $BUILD_DIR" >&2
  exit 1
fi

echo "Preparing remote release directory..."
ssh -p "$MAIN_DEPLOY_SSH_PORT" "$MAIN_DEPLOY_TARGET" "mkdir -p '$REMOTE_RELEASE_DIR'"

echo "Uploading Main Back Office files..."
rsync -az --delete -e "ssh -p $MAIN_DEPLOY_SSH_PORT" "$BUILD_DIR/" "$MAIN_DEPLOY_TARGET:$REMOTE_RELEASE_DIR/"

echo "Activating release..."
ssh -p "$MAIN_DEPLOY_SSH_PORT" "$MAIN_DEPLOY_TARGET" "
  set -e
  ln -sfn '$REMOTE_RELEASE_DIR' '$REMOTE_CURRENT_DIR'
  find '$MAIN_DEPLOY_REMOTE_DIR/releases' -mindepth 1 -maxdepth 1 -type d | sort -r | tail -n +$((MAIN_DEPLOY_KEEP_RELEASES + 1)) | xargs -r rm -rf
"

echo
echo "Main Back Office deploy completed."
echo "Serve this directory from Nginx or another static server:"
echo "  $REMOTE_CURRENT_DIR"
echo
echo "Expected public env values at build time:"
echo "  NUXT_PUBLIC_GARMETIX_API_BASE_URL=$NUXT_PUBLIC_GARMETIX_API_BASE_URL"
echo "  NUXT_PUBLIC_GARMETIX_MAIN_URL=$NUXT_PUBLIC_GARMETIX_MAIN_URL"
