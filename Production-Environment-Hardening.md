# Stage 5B — Production Environment Hardening

This stage prepares the app for safer production hosting on Linux or Mac mini with Docker Compose.

## Main additions

- Admin production readiness endpoint and page.
- Configurable CORS origins through `CORS_ALLOWED_ORIGINS`.
- Security response headers in the API.
- Forwarded-header support environment variable in production compose.
- Docker JSON log rotation for API, web, and PostgreSQL containers.
- `.env.production.example` with production-only settings.
- Secret generation and preflight scripts.
- Health monitoring, log tailing, update, and rollback scripts.
- Caddy and Cloudflare Tunnel examples.
- systemd service example for auto-start after reboot.

## First production setup

```bash
cp .env.production.example .env.production
scripts/linux/generate-secrets.sh .env.production
nano .env.production
scripts/linux/production-preflight.sh .env.production
docker compose --env-file .env.production -f docker-compose.prod.yml up -d --build
scripts/linux/monitor-health.sh .env.production
```

Then login as admin and open:

```text
/production-readiness
/system-health
/data-consistency
```

## HTTPS options

### Option A — Cloudflare Tunnel

Use this when the store has dynamic IP, router restrictions, or you do not want to expose ports directly.

1. Install `cloudflared`.
2. Create a Cloudflare tunnel.
3. Copy `infra/cloudflare/config.example.yml` to `infra/cloudflare/config.yml`.
4. Set tunnel id, credentials file, and hostname.
5. Run:

```bash
scripts/linux/start-cloudflare-tunnel.sh garmetix infra/cloudflare/config.yml
```

### Option B — Caddy reverse proxy

Use this when the server has a fixed reachable domain/IP.

1. Install Caddy.
2. Copy `infra/caddy/Caddyfile.example` to your Caddy config path.
3. Replace `garmetix.example.com`.
4. Set these in `.env.production`:

```env
PUBLIC_HTTPS_URL=https://your-domain.example
PASSWORD_RESET_FRONTEND_BASE_URL=https://your-domain.example
CORS_ALLOWED_ORIGINS=https://your-domain.example
ASPNETCORE_FORWARDEDHEADERS_ENABLED=true
```

## Auto-start with systemd

```bash
sudo cp deploy/systemd/garmetix.service /etc/systemd/system/garmetix.service
sudo systemctl daemon-reload
sudo systemctl enable garmetix
sudo systemctl start garmetix
sudo systemctl status garmetix
```

## Update workflow

```bash
scripts/linux/deploy-release.sh Garmetix-Stage5B-ProductionHardening-v1.5.zip .env.production /opt/garmetix
```

The script keeps a previous symlink so rollback can be performed:

```bash
scripts/linux/rollback-release.sh /opt/garmetix
```

## Production safety rules

- Never commit real `.env.production` or service-account JSON files.
- Always run backup verification and restore preflight before update.
- Keep at least one off-site backup destination.
- Do not enable Oracle inbound auto-apply until trusted-source rules are approved.
- Run `/data-consistency` after large import, restore, or version update.
- Keep one release ZIP and its `.env.production` backup outside the app folder.
