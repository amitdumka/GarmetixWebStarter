# Stage 8H Package 16 - Persistent Env Deploy Hook (v4.8.5)

This package updates `deploy/deploy-to-macmini.sh` so deployment uses persistent env files instead of replacing secrets on every ZIP upgrade.

## Included

- `deploy/deploy-to-macmini.sh` now runs `~/garmetix-link-env.sh` before normal deployment starts.
- The script passes the current project root to the helper:
  - `bash ~/garmetix-link-env.sh "$ROOT_DIR"`
- Deployment stops if the helper is missing or fails.
- Remote deployment now re-links:
  - `/opt/garmetix/current/.env.production`
  - to `/opt/garmetix/shared/env/.env.production`
  after each new release is extracted.
- Added optional helper creator:
  - `deploy/create-garmetix-link-env-helper.sh`

## Bypass

For emergency/manual deploy only:

```bash
GARMETIX_SKIP_LINK_ENV=true ./deploy/deploy-to-macmini.sh
```

## Verification

1. Keep WSL env at `~/.garmetix/macmini.env`.
2. Keep Mac mini production env at `/opt/garmetix/shared/env/.env.production`.
3. Run `./deploy/deploy-to-macmini.sh`.
4. Confirm the first output says `Linking persistent Garmetix env files before deployment...`.
5. Confirm remote output says `Linked persistent production env`.
