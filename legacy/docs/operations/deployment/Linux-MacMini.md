# Garmetix Deployment - Linux or Mac mini

This guide runs Garmetix with Docker Compose:

- Nuxt web app on port `3000`
- ASP.NET Core API on port `5080`
- PostgreSQL on port `5432`

## 1. Install Requirements

Install Docker Desktop on Mac mini, or Docker Engine with the Compose plugin on Linux.

Check:

```bash
docker --version
docker compose version
```

## 2. Configure Production Environment

Copy the example file:

```bash
cp .env.example .env
```

Edit `.env` and change at least:

```text
POSTGRES_PASSWORD=...
JWT_SIGNING_KEY=...
NUXT_PUBLIC_API_BASE=/api
NUXT_API_INTERNAL_BASE=http://api:5080/api
```

The browser should use `/api`. Nuxt forwards that privately to the ASP.NET API. This also works behind Cloudflare Tunnel because only the Nuxt web port needs to be public.

## 3. Start The App

```bash
docker compose -f docker-compose.prod.yml up --build -d
```

Open:

```text
http://YOUR-SERVER-IP:3000
```

For Cloudflare Tunnel on Windows, expose only the web app:

```powershell
cloudflared tunnel --url http://localhost:3000
```

Then open the generated `trycloudflare.com` URL. The frontend will call `/api/...` on the same URL, and Nuxt will forward those requests to the backend.

Check backend connectivity through the same public URL:

```text
https://YOUR-TUNNEL-URL/api/health
```

The response should show `databaseReady: true`.

View logs:

```bash
docker compose -f docker-compose.prod.yml logs -f
```

Stop:

```bash
docker compose -f docker-compose.prod.yml down
```

## 4. First Admin

Open the web app and use the `First Admin` tab. After the admin exists, use normal login.

If login fails, check:

```bash
docker compose -f docker-compose.prod.yml logs api
docker compose -f docker-compose.prod.yml logs postgres
```

## 5. Automatic HR And Payroll

The API has a hosted job:

- Last day of the month: generates monthly attendance.
- First day of the month: generates previous-month payslips.

Default schedule:

```text
PAYROLL_AUTOMATION_TIME_ZONE=Asia/Kolkata
PAYROLL_AUTOMATION_RUN_HOUR=2
PAYROLL_AUTOMATION_RUN_MINUTE=0
```

The job is idempotent. If records already exist, it updates them.

## 6. Backup Database

Create the backup folder once:

```bash
mkdir -p backups
```

Backup:

```bash
docker compose -f docker-compose.prod.yml exec postgres pg_dump -U "$POSTGRES_USER" "$POSTGRES_DB" > backups/garmetix-$(date +%F).sql
```

If your shell does not load `.env`, run:

```bash
docker compose -f docker-compose.prod.yml exec postgres pg_dump -U garmetix garmetix > backups/garmetix-$(date +%F).sql
```

## 7. Restore Database

Stop app writes first:

```bash
docker compose -f docker-compose.prod.yml stop api web
```

Restore:

```bash
cat backups/garmetix-YYYY-MM-DD.sql | docker compose -f docker-compose.prod.yml exec -T postgres psql -U garmetix garmetix
```

Start app again:

```bash
docker compose -f docker-compose.prod.yml start api web
```

## 8. Update App

After copying new code:

```bash
docker compose -f docker-compose.prod.yml up --build -d
```

PostgreSQL data stays in the `garmetix_pg` Docker volume.

## 9. Useful Checks

Containers:

```bash
docker compose -f docker-compose.prod.yml ps
```

API:

```bash
curl http://localhost:5080/
```

Web:

```bash
curl http://localhost:3000/
```
