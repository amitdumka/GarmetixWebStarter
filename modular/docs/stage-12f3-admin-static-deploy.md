# Stage 12F.3 - Admin Static Deploy

Version: 5.12.26

## Scope

This stage adds the static deployment handoff for the modular Admin/SaaS frontend.

## Added

- `modular/deploy/admin-static-deploy.sh`
- `npm --prefix modular run deploy:admin`
- Admin deployment notes in `modular/deploy/README.md`
- Admin deploy environment examples in `modular/.env.example`

## Deployment Shape

The script:

1. Builds the Admin Nuxt static output.
2. Uploads `.output/public` to a timestamped remote release folder.
3. Updates a `current` symlink.
4. Keeps the latest configured releases.

Default target:

```bash
ADMIN_DEPLOY_TARGET=amit@192.168.11.126
ADMIN_DEPLOY_REMOTE_DIR=/var/www/garmetix/admin
```

Ubuntu desktop override:

```bash
ADMIN_DEPLOY_TARGET=amitkumar@192.168.11.127
```

No passwords, Cloudflare tokens or private keys are stored in the script.

## Production Env Example

Use environment variables at build time:

```bash
ADMIN_DEPLOY_TARGET=amit@192.168.11.126 \
ADMIN_DEPLOY_REMOTE_DIR=/var/www/garmetix/admin \
NUXT_PUBLIC_GARMETIX_API_BASE_URL=https://api.garmetix.aadwikafashion.in/api \
NUXT_PUBLIC_GARMETIX_ADMIN_URL=https://admin.garmetix.aadwikafashion.in \
bash modular/deploy/admin-static-deploy.sh
```

Cloudflare Tunnel should route `admin.garmetix.aadwikafashion.in` to the static web server serving `/var/www/garmetix/admin/current`.

## Validation

Run:

```powershell
npm.cmd run modular:check
npm.cmd --prefix modular run build:admin
```

## Next Step

Stage 12G.1 should begin the Main Back Office modular cleanup by auditing what remains in `modular/apps/main`, documenting route ownership gaps, and preparing the first safe route-level optimization pass.
