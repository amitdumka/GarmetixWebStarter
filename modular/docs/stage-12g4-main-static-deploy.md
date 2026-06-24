# Stage 12G.4 - Main Static Deploy

Version: 5.12.30

## Scope

This stage adds the static deployment handoff for the modular Main Back Office frontend.

## Added

- `modular/deploy/main-static-deploy.sh`
- `npm --prefix modular run deploy:main`
- Main Back Office deployment notes in `modular/deploy/README.md`
- Main deploy environment examples in `modular/.env.example`

## Deployment Shape

The script:

1. Builds the Main Back Office Nuxt static output.
2. Uploads `.output/public` to a timestamped remote release folder.
3. Updates a `current` symlink.
4. Keeps the latest configured releases.

Default target:

```bash
MAIN_DEPLOY_TARGET=amit@192.168.11.126
MAIN_DEPLOY_REMOTE_DIR=/var/www/garmetix/main
```

Ubuntu desktop override:

```bash
MAIN_DEPLOY_TARGET=amitkumar@192.168.11.127
```

No passwords, Cloudflare tokens or private keys are stored in the script.

## Production Env Example

Use environment variables at build time:

```bash
MAIN_DEPLOY_TARGET=amit@192.168.11.126 \
MAIN_DEPLOY_REMOTE_DIR=/var/www/garmetix/main \
NUXT_PUBLIC_GARMETIX_API_BASE_URL=https://api.garmetix.aadwikafashion.in/api \
NUXT_PUBLIC_GARMETIX_MAIN_URL=https://garmetix.aadwikafashion.in \
bash modular/deploy/main-static-deploy.sh
```

Cloudflare Tunnel should route `garmetix.aadwikafashion.in` to the static web server serving `/var/www/garmetix/main/current`.

## Validation

Run:

```powershell
npm.cmd run modular:check
npm.cmd --prefix modular run build:main
npm.cmd run legacy:api:build
```

## Next Step

Stage 12H.1 should add a consolidated deployment split guide covering all modular frontends, the shared API, Cloudflare Tunnel public hostnames, and the one-database/no-backend-split rule.
