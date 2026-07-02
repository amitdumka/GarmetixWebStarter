# Stage 13G.1 SRP Deployment Foundation

Version: 5.13.41

## Goal

Add a repeatable deployment foundation for the Ubuntu desktop SRP target at `192.168.11.127` without disturbing the existing `garmetix.aadwikafashion.in` host.

## Public Host

`srp.aadwikafashion.in` is planned as a single Cloudflare Tunnel hostname.

Route layout:

- `/` serves Main Back Office.
- `/pos/` serves POS.
- `/hr/` serves HR.
- `/ai-sense/` serves AI Sense analytics.
- `/books/` serves Accounting/Books.
- `/admin/` serves Admin/SaaS.
- `/api/` reverse-proxies to the ASP.NET Core API on the same host.

## Files Added

- `modular/deploy/srp-whole-site-deploy.sh`
- `modular/deploy/srp-deploy.config.example.env`

## Files Updated

- Each modular Nuxt app now supports `GARMETIX_NUXT_BASE_URL` and app-specific base path env variables so static apps can run under path prefixes.
- `modular/.env.example` documents the base path variables and SRP config location.
- `modular/deploy/README.md` documents SRP commands.
- `modular/scripts/validate-structure.mjs` requires the SRP deploy files.

## Config

The script reads:

```bash
$GARMETIX_SRP_DEPLOY_CONFIG
```

or defaults to:

```bash
~/.config/garmetix/srp-deploy.env
```

Create it with:

```bash
npm run modular:deploy:srp -- --init-config
```

The checked-in example defaults to `amitkumar@192.168.11.127`.

For non-interactive deploys, keep passwords in a private file outside git:

```bash
~/.config/garmetix/srp-deploy.secrets.env
```

Supported keys:

```bash
SRP_SSH_PASSWORD=your-ssh-password
SRP_SUDO_PASSWORD=your-sudo-password
```

This mode requires `sshpass` on the machine running the deploy script. `rsync` is optional because the script can fall back to a compressed tar upload stream. The script never commits or prints password values.

## Generated Server Artifacts

Each release contains:

- `web-root/` with all static frontend apps.
- `api/` with the published ASP.NET Core API, unless API publish is skipped.
- `ops/nginx-garmetix-srp.conf` for local Nginx on the SRP host.
- `ops/garmetix-srp-api.service` for systemd API hosting.
- `ops/srp-api.env.template` for server-only API secrets and database connection values.
- `ops/cloudflared-garmetix-srp.yml` for Cloudflare Tunnel ingress.
- `ops/install-srp-on-host.sh` to apply Nginx and systemd templates after review.
- `modular/scripts/run-bash-script.mjs` chooses Git Bash on Windows or normal bash on Linux/macOS for npm deploy commands.

## Commands

Dry run:

```bash
npm run modular:deploy:srp -- --dry-run
```

Build and stage locally:

```bash
npm run modular:deploy:srp -- --build-only
```

Build and upload:

```bash
npm run modular:deploy:srp
```

Build, upload and apply host templates:

```bash
npm run modular:deploy:srp -- --install-remote
```

## Cloudflare Tunnel

On the SRP host, after cloudflared is installed and authenticated:

```bash
cloudflared tunnel create garmetix-srp
cloudflared tunnel route dns garmetix-srp srp.aadwikafashion.in
sudo cp /opt/garmetix-srp/current/ops/cloudflared-garmetix-srp.yml /etc/cloudflared/garmetix-srp.yml
sudo systemctl restart cloudflared
```

The real tunnel credential file must stay on the server and must not be committed.

## Risks

- First live install still needs server package readiness: `nginx`, `dotnet`, `cloudflared`, PostgreSQL access and SSH.
- `srp-api.env.template` contains placeholders only. The real server file must be edited before API start.
- Path-based hosting depends on all app links respecting the configured public URLs and base paths.
- If the actual Linux username differs from `amitkumar`, update `SRP_DEPLOY_TARGET` in the local config before upload.

## Next Stage

Stage 13G.2 should run a live host readiness preflight for SSH, Nginx, dotnet, cloudflared and PostgreSQL connectivity, then perform the first controlled SRP deployment when the host is reachable.
