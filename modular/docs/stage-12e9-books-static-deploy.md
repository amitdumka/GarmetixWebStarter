# Stage 12E.9 - Books Static Deploy

Version: 5.12.23

## Scope

This stage adds the static deployment handoff for the modular Books frontend.

## Added

- `modular/deploy/books-static-deploy.sh`
- `npm --prefix modular run deploy:books`
- Books deployment notes in `modular/deploy/README.md`

## Deployment Shape

The script:

1. Builds the Books Nuxt static output.
2. Uploads `.output/public` to a timestamped remote release folder.
3. Updates a `current` symlink.
4. Keeps the latest configured releases.

Default target:

```bash
BOOKS_DEPLOY_TARGET=amit@192.168.11.126
BOOKS_DEPLOY_REMOTE_DIR=/var/www/garmetix/books
```

Ubuntu desktop override:

```bash
BOOKS_DEPLOY_TARGET=amitkumar@192.168.11.127
```

No passwords, Cloudflare tokens or private keys are stored in the script.

## Production Env Example

Use environment variables at build time:

```bash
BOOKS_DEPLOY_TARGET=amit@192.168.11.126 \
BOOKS_DEPLOY_REMOTE_DIR=/var/www/garmetix/books \
NUXT_PUBLIC_GARMETIX_API_BASE_URL=https://api.garmetix.aadwikafashion.in/api \
NUXT_PUBLIC_GARMETIX_BOOKS_URL=https://books.garmetix.aadwikafashion.in \
bash modular/deploy/books-static-deploy.sh
```

Cloudflare Tunnel should route `books.garmetix.aadwikafashion.in` to the static web server serving `/var/www/garmetix/books/current`.

## Validation

Run:

```powershell
npm.cmd run modular:check
npm.cmd --prefix modular run build:books
```

## Next Step

Stage 12F.1 should begin the Admin/SaaS modular frontend foundation for setup, users, roles, license, system health, message logs and deployment diagnostics.
