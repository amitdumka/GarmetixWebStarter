# Production Security Hardening v4.6.6

Run these checks on the Mac mini after deployment and before allowing real billing data:

```bash
cd /opt/garmetix/current
./scripts/linux/production-security-hardening-check.sh .env.production
./scripts/linux/log-retention-check.sh .env.production
```

## Required production settings

- `RESET_DATABASE_ON_DEPLOY=false`
- `PUBLIC_HTTPS_URL=https://garmetix.aadwikafashion.in`
- `CORS_ALLOWED_ORIGINS=https://garmetix.aadwikafashion.in`
- strong random `POSTGRES_PASSWORD`
- strong random `JWT_SIGNING_KEY`
- Docker enabled at boot: `sudo systemctl enable --now docker`
- localhost-only container ports in Docker Compose
- Docker log rotation configured with `DOCKER_LOG_MAX_SIZE` and `DOCKER_LOG_MAX_FILE`

## Header expectations

The public HTTPS site should return these security headers:

- `Strict-Transport-Security`
- `X-Content-Type-Options`
- `X-Frame-Options`
- `Referrer-Policy`

Cloudflare can also enforce HTTPS-only mode and additional WAF/rate-limiting rules from the dashboard.
