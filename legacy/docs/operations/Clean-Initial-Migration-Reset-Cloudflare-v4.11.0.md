# Clean Initial Migration + Mac mini Reset + Cloudflare Token Guard v4.11.0

## Purpose

This patch is for a fresh Mac mini deployment where old PostgreSQL Docker volumes still contain an older schema. The API crash showed the old `Users` table was missing the current `IsSuperAdmin` column, so the startup defaults query failed before the app could become healthy.

## Database changes

- Deleted the previous EF migration files.
- Created a new clean baseline migration:
  - `20260623123000_InitialCreate.cs`
  - `20260623123000_InitialCreate.Designer.cs`
  - `GarmetixDbContextModelSnapshot.cs`
- Updated `FreshSchemaBaselineMigrationId` in `Program.cs` to `20260623123000_InitialCreate`.
- Added an idempotent schema repair for older `Users` tables:
  - `IsSuperAdmin`
  - `PinHash`
  - `RemoteUserId`
  - `AppOperation`

## Deployment reset changes

`deploy/run-production.sh` and `deploy/reset-production-database.sh` now remove old containers and known Garmetix PostgreSQL volumes for both the intended `garmetix` Compose project and the accidental `current` Compose project that can happen when deploying through `/opt/garmetix/current` symlink.

Known removed volumes include:

- `garmetix_garmetix_pg`
- `current_garmetix_pg`
- `garmetix_pg`
- `garmetix_postgres_data`
- `current_postgres_data`
- `postgres_data`

Use this only for a clean/fresh install:

```bash
RESET_DATABASE_ON_DEPLOY=true ./deploy/deploy-to-macmini.sh
```

After the deploy package is created, the local flag is reset to false to avoid accidental future data loss.

## Cloudflare changes

- `deploy/cloudflare-create-or-update-tunnel.sh` now refreshes the connector token from the configured tunnel ID using the Cloudflare token endpoint.
- This prevents the earlier bug where DNS pointed to a new tunnel ID but `cloudflared` started an old tunnel because the token belonged to the old tunnel.
- `deploy/docker-compose.cloudflare.yml` now uses `$${CLOUDFLARE_TUNNEL_TOKEN}` and `env_file: .env.production` so the container reads the token from the production env instead of stale shell interpolation.
- `deploy/deploy-to-macmini.sh` now uploads the freshly generated `.env.production` into `/opt/garmetix/shared/env/.env.production` on every deploy.

## Verification

After deploy:

```bash
cd /opt/garmetix/current
COMPOSE_PROJECT_NAME=garmetix docker compose --env-file .env.production -f docker-compose.prod.yml -f deploy/docker-compose.cloudflare.yml ps
docker logs --tail=80 garmetix-cloudflared-1 | grep 'Starting tunnel'
docker logs --tail=120 garmetix-api-1
curl -I http://127.0.0.1:3000
curl -I https://garmetix.aadwikafashion.in
```

The cloudflared log must show the same tunnel ID as Cloudflare DNS.
