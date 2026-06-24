# Stage 12H.1 - Deployment Split Guide

Version: 5.12.31

## Decision

Version5 keeps one ASP.NET Core API and one PostgreSQL database. Only the Nuxt frontends are split into independently built static apps.

## Apps And Public Hosts

| App | Folder | Build command | Deploy command | Public host |
| --- | --- | --- | --- | --- |
| Main Back Office | `modular/apps/main` | `npm --prefix modular run build:main` | `npm --prefix modular run deploy:main` | `garmetix.aadwikafashion.in` |
| POS | `modular/apps/pos` | `npm --prefix modular run build:pos` | `npm --prefix modular run deploy:pos` | `pos.garmetix.aadwikafashion.in` |
| HR | `modular/apps/hr` | `npm --prefix modular run build:hr` | `npm --prefix modular run deploy:hr` | `hr.garmetix.aadwikafashion.in` |
| AI Sense | `modular/apps/ai-sense` | `npm --prefix modular run build:ai-sense` | `npm --prefix modular run deploy:ai-sense` | `ai-sense.garmetix.aadwikafashion.in` |
| Books | `modular/apps/books` | `npm --prefix modular run build:books` | `npm --prefix modular run deploy:books` | `books.garmetix.aadwikafashion.in` |
| Admin/SaaS | `modular/apps/admin` | `npm --prefix modular run build:admin` | `npm --prefix modular run deploy:admin` | `admin.garmetix.aadwikafashion.in` |
| API | `legacy/backend/Garmetix.Api` | `npm run legacy:api:build` | Existing API deploy path | `api.garmetix.aadwikafashion.in` |

## Shared Runtime Rule

- Keep one API service.
- Keep one PostgreSQL database.
- Keep one auth/token/permission system.
- Each frontend receives the same `NUXT_PUBLIC_GARMETIX_API_BASE_URL`.
- Do not split the database or create per-app databases in Stage 12.

## Static Directory Layout

Recommended remote directories:

```text
/var/www/garmetix/main/current
/var/www/garmetix/pos/current
/var/www/garmetix/hr/current
/var/www/garmetix/ai-sense/current
/var/www/garmetix/books/current
/var/www/garmetix/admin/current
```

Each deploy script uploads to `releases/<timestamp>` and moves the `current` symlink after upload.

## Build-Time Environment

Use real values outside source control:

```bash
NUXT_PUBLIC_GARMETIX_API_BASE_URL=https://api.garmetix.aadwikafashion.in/api
NUXT_PUBLIC_GARMETIX_MAIN_URL=https://garmetix.aadwikafashion.in
NUXT_PUBLIC_GARMETIX_POS_URL=https://pos.garmetix.aadwikafashion.in
NUXT_PUBLIC_GARMETIX_HR_URL=https://hr.garmetix.aadwikafashion.in
NUXT_PUBLIC_GARMETIX_AI_SENSE_URL=https://ai-sense.garmetix.aadwikafashion.in
NUXT_PUBLIC_GARMETIX_BOOKS_URL=https://books.garmetix.aadwikafashion.in
NUXT_PUBLIC_GARMETIX_ADMIN_URL=https://admin.garmetix.aadwikafashion.in
```

Do not commit passwords, SSH private keys, Cloudflare tunnel credentials, or database connection strings.

## Cloudflare Tunnel Shape

Use the real tunnel token and tunnel credentials only on the server. Keep source control limited to examples.

```yaml
ingress:
  - hostname: api.garmetix.aadwikafashion.in
    service: http://localhost:5080
  - hostname: garmetix.aadwikafashion.in
    service: http://localhost:8080
  - hostname: pos.garmetix.aadwikafashion.in
    service: http://localhost:8081
  - hostname: hr.garmetix.aadwikafashion.in
    service: http://localhost:8082
  - hostname: ai-sense.garmetix.aadwikafashion.in
    service: http://localhost:8083
  - hostname: books.garmetix.aadwikafashion.in
    service: http://localhost:8084
  - hostname: admin.garmetix.aadwikafashion.in
    service: http://localhost:8085
  - service: http_status:404
```

The `service` values may point to Nginx, Caddy, Apache, Docker services, or another static server. The frontends should still be built as static outputs.

## Nginx Static Host Pattern

Each frontend can use this shape with its own host and root:

```nginx
server {
    listen 80;
    server_name example.garmetix-host.invalid;
    root /var/www/garmetix/example/current;
    index index.html;

    location / {
        try_files $uri $uri/ /index.html;
    }
}
```

Use the real host/root pairs from the app table above on the deployment server.

## Deployment Order

1. Deploy or verify the API first.
2. Confirm `GET /api/health` is reachable through `api.garmetix.aadwikafashion.in`.
3. Build and deploy Main.
4. Build and deploy POS, HR, AI Sense, Books and Admin as needed.
5. Confirm each app can log in and reach API health/status.
6. Keep `legacy/` available until modular parity is fully verified.

## Validation

Run:

```powershell
npm.cmd run modular:check
npm.cmd --prefix modular run build:main
npm.cmd --prefix modular run build:pos
npm.cmd --prefix modular run build:hr
npm.cmd --prefix modular run build:ai-sense
npm.cmd --prefix modular run build:books
npm.cmd --prefix modular run build:admin
npm.cmd run legacy:api:build
```

## Next Step

Stage 12H.2 should add a local all-app validation script so Stage 12Z can run one command to validate the modular static builds and the shared API build.
