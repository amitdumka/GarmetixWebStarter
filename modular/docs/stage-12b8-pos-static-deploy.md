# Stage 12B.8 - POS Static Deploy

Version: 5.12.8

## Scope

This stage adds deployment automation for the modular POS frontend only. It does not deploy automatically, change Docker, modify the backend API, split the database, or store credentials.

## Added

- `modular/deploy/pos-static-deploy.sh`
  - Builds `@garmetix/pos-web` as a static Nuxt app.
  - Uploads `.output/public` to an SSH target with `rsync`.
  - Creates timestamped release folders.
  - Flips a `current` symlink to the new release.
  - Prunes old releases.
- `modular/deploy/README.md`
  - POS deployment example.
  - Ubuntu server and desktop target examples.
  - Nginx static hosting shape.
- `.env.example` deploy variable notes.
- `npm --prefix modular run deploy:pos` script alias.

## Required Local Commands

- `npm`
- `ssh`
- `rsync`

On Windows, run the shell script from Git Bash, WSL, or an Ubuntu machine. On Ubuntu, run it directly with `bash`.

## Example

```bash
POS_DEPLOY_TARGET=amit@192.168.11.126 \
POS_DEPLOY_REMOTE_DIR=/var/www/garmetix/pos \
NUXT_PUBLIC_GARMETIX_API_BASE_URL=https://api.your-domain.example/api \
NUXT_PUBLIC_GARMETIX_POS_URL=https://pos.your-domain.example \
npm --prefix modular run deploy:pos
```

## Security

- Passwords are not stored in the script or docs.
- Use SSH keys, SSH agent, or an interactive SSH password prompt.
- Cloudflare Tunnel tokens and credentials must remain outside source control.

## How To Test Without Deploying

1. Run `npm --prefix modular run build:pos`.
2. Confirm `modular/apps/pos/.output/public` exists.
3. Review the deploy command with `POS_DEPLOY_TARGET` and `POS_DEPLOY_REMOTE_DIR` set for the target machine.
4. Run the deploy script only when SSH access is ready.
