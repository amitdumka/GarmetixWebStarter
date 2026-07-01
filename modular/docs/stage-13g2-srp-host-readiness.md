# Stage 13G.2 SRP Host Readiness

Version: 5.13.42

## Goal

Add a WSL-friendly readiness gate before the first live SRP deployment to `amitkumar@192.168.11.127`.

## What Was Added

- `modular/deploy/srp-host-readiness.sh`
- `npm run modular:deploy:srp:readiness`
- Automatic Windows-side config discovery when running from WSL under `/mnt/c/Users/...`
- Optional remote base package installation with `--install-remote-packages`
- Root `outputs/` ignore rule so generated deploy releases stay out of git

## What It Checks

Local WSL/runtime:

- `node`
- `npm`
- `dotnet`
- `ssh`
- `sshpass` when password auth is configured
- `tar`
- `rsync` as optional because tar stream upload can be used

Remote Ubuntu host:

- SSH login
- sudo password acceptance
- OS identity
- `tar`
- Nginx
- .NET runtime with ASP.NET Core runtime availability
- `cloudflared`
- PostgreSQL client
- SRP API env file at `/etc/garmetix/srp-api.env`
- Cloudflare tunnel credential/config files
- SRP remote base directory
- SRP Nginx/API ports

## Commands

From WSL:

```bash
cd /mnt/c/Users/amitn/Documents/Codex/2026-06-04/i-have-class-model-written-in/outputs/GarmetixWebStarter
git pull origin Version5
npm run modular:deploy:srp:readiness -- --local-only
npm run modular:deploy:srp:readiness
```

If base packages are missing on the Ubuntu host:

```bash
npm run modular:deploy:srp:readiness -- --install-remote-packages
```

Then continue:

```bash
npm run modular:deploy:srp -- --build-only
npm run modular:deploy:srp -- --install-remote
```

## Notes

- The readiness script reads the same SRP config and private secrets files as the deploy script.
- Password values must stay in `~/.config/garmetix/srp-deploy.secrets.env` or the Windows-side equivalent, never in git.
- .NET and cloudflared may still require manual installation if they are not available through the host's apt repositories.

## Next Stage

Stage 13G.3 should run the readiness script from WSL against the live host, fix missing host prerequisites, then perform the first controlled SRP build/upload/install.
