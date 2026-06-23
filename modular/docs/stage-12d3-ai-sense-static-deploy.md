# Stage 12D.3 - AI Sense Static Deploy

Version: 5.12.14

## Scope

This stage adds deployment automation for the modular AI Sense frontend only. It does not deploy automatically, change Docker, modify database schema, split the API, or store credentials.

## Added

- `modular/deploy/ai-sense-static-deploy.sh`
  - Builds `@garmetix/ai-sense-web` as a static Nuxt app.
  - Uploads `.output/public` to an SSH target with `rsync`.
  - Creates timestamped release folders.
  - Flips a `current` symlink to the new release.
  - Prunes old releases.
- `modular/deploy/README.md`
  - AI Sense deployment example.
  - Ubuntu server and desktop target examples.
  - Nginx static hosting shape.
- `.env.example` deploy variable notes.
- `npm --prefix modular run deploy:ai-sense` script alias.

## Required Local Commands

- `npm`
- `ssh`
- `rsync`

On Windows, run the shell script from Git Bash, WSL, or an Ubuntu machine. On Ubuntu, run it directly with `bash`.

## Example

```bash
AI_SENSE_DEPLOY_TARGET=amit@192.168.11.126 \
AI_SENSE_DEPLOY_REMOTE_DIR=/var/www/garmetix/ai-sense \
NUXT_PUBLIC_GARMETIX_API_BASE_URL=https://api.your-domain.example/api \
NUXT_PUBLIC_GARMETIX_AI_SENSE_URL=https://ai-sense.your-domain.example \
npm --prefix modular run deploy:ai-sense
```

## Security

- Passwords are not stored in the script or docs.
- Use SSH keys, SSH agent, or an interactive SSH password prompt.
- Cloudflare Tunnel tokens and credentials must remain outside source control.

## How To Test Without Deploying

1. Run `npm --prefix modular run build:ai-sense`.
2. Confirm `modular/apps/ai-sense/.output/public` exists.
3. Review the deploy command with `AI_SENSE_DEPLOY_TARGET` and `AI_SENSE_DEPLOY_REMOTE_DIR` set for the target machine.
4. Run the deploy script only when SSH access is ready.

## Next Step

Stage 12E should begin the Books modular app with accounting dashboard and read-only accounting route coverage before adding write-sensitive ledger/voucher actions.
