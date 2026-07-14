# Stage 12H.3 - Deploy Preflight

Version: 5.12.33

## Goal

Add a safe deployment readiness check before using the static deploy scripts.

## Commands

From the repository root:

```powershell
npm.cmd run modular:deploy:preflight
```

From the modular folder:

```powershell
npm.cmd run deploy:preflight
```

## What It Checks

- Root and modular package files.
- Shared ASP.NET API project path.
- Modular `.env.example` public URL keys.
- Deploy scripts for Main, POS, HR, AI Sense, Books, and Admin.
- Static build outputs for every modular app.
- `index.html` in every generated static app output.
- Obvious secret patterns in deploy-related files.
- Local command availability for `node`, `npm`, and `git`.

## Remote Checks

Remote checks are optional and non-destructive:

```powershell
npm.cmd run modular:deploy:preflight -- --remote --target=server
npm.cmd run modular:deploy:preflight -- --remote --target=desktop
```

Targets:

| Target | Default SSH target |
| --- | --- |
| `server` | `amit@192.168.11.126` |
| `desktop` | `amitkumar@192.168.11.127` |

The script uses SSH with `BatchMode=yes`, so it will not store or ask for passwords. Configure SSH keys for full remote checks, or let the deploy scripts prompt interactively when you actually deploy.

## Override Variables

Use environment variables outside source control:

```bash
GARMETIX_DEPLOY_SERVER_TARGET=amit@192.168.11.126
GARMETIX_DEPLOY_SERVER_SSH_PORT=22
GARMETIX_DEPLOY_DESKTOP_TARGET=amitkumar@192.168.11.127
GARMETIX_DEPLOY_DESKTOP_SSH_PORT=22
```

Per-app remote directory variables are also respected:

```bash
MAIN_DEPLOY_REMOTE_DIR=/var/www/garmetix/main
POS_DEPLOY_REMOTE_DIR=/var/www/garmetix/pos
HR_DEPLOY_REMOTE_DIR=/var/www/garmetix/hr
AI_SENSE_DEPLOY_REMOTE_DIR=/var/www/garmetix/ai-sense
BOOKS_DEPLOY_REMOTE_DIR=/var/www/garmetix/books
ADMIN_DEPLOY_REMOTE_DIR=/var/www/garmetix/admin
```

## Strict Mode

By default, Windows-only Bash/rsync warnings do not fail the preflight because deployments can be run from Git Bash, WSL, or the Ubuntu host.

To treat warnings as failures:

```powershell
npm.cmd run modular:deploy:preflight -- --strict
```

## Next Step

Stage 12H.4 should add host-specific deployment notes for the Ubuntu server and Ubuntu desktop, still without storing credentials.
