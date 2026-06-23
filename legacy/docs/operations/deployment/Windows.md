# Garmetix Deployment - Windows With Docker And Cloudflare

This guide is for running Garmetix on a Windows computer and exposing it through Cloudflare Tunnel when the internet IP is not static.

## 1. Requirements

Install:

- Docker Desktop
- Node.js, only for local frontend builds
- Cloudflare Tunnel, `cloudflared`, for public access

Check:

```powershell
docker --version
docker compose version
cloudflared --version
```

## 2. Configure Environment

Copy `.env.example` to `.env` and set strong values:

```powershell
Copy-Item .env.example .env
notepad .env
```

Important values:

```text
POSTGRES_PASSWORD=change-this
JWT_SIGNING_KEY=use-a-long-random-key
NUXT_PUBLIC_API_BASE=/api
NUXT_API_INTERNAL_BASE=http://api:5080/api
```

Keep `NUXT_PUBLIC_API_BASE=/api`. The browser will call the same public domain, and Nuxt will privately forward API calls to the backend container.

## 3. Start Garmetix

From the project root:

```powershell
.\scripts\windows\start-garmetix.ps1 -Build
```

If Windows says scripts are disabled, run the same script with a process-only bypass:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\scripts\windows\start-garmetix.ps1 -Build
```

After the first build, daily start can be:

```powershell
.\scripts\windows\start-garmetix.ps1
```

Open:

```text
http://localhost:3000
```

## 4. Health Check

Local health:

```powershell
.\scripts\windows\health-check.ps1
```

Local plus public health:

```powershell
.\scripts\windows\health-check.ps1 -PublicUrl "https://garmetix.aadwikafashion.in"
```

The API proxy is healthy when `/api/health` returns `databaseReady: true`.

## 5. Cloudflare Tunnel

For a temporary public URL:

```powershell
.\scripts\windows\start-cloudflare-tunnel.ps1
```

For a configured named tunnel:

```powershell
.\scripts\windows\start-cloudflare-tunnel.ps1 -TunnelName "garmetix"
```

To start Docker and then Cloudflare together:

```powershell
.\scripts\windows\start-garmetix.ps1 -StartTunnel -TunnelName "garmetix"
```

Only expose `http://localhost:3000`. Do not expose the API port separately; the Nuxt `/api` proxy handles backend calls.

## 6. Restart After Code Changes

Restart all services:

```powershell
.\scripts\windows\restart-garmetix.ps1
```

Rebuild and restart:

```powershell
.\scripts\windows\restart-garmetix.ps1 -Build
```

Restart only the API:

```powershell
.\scripts\windows\restart-garmetix.ps1 -Service api
```

Restart only the web app:

```powershell
.\scripts\windows\restart-garmetix.ps1 -Service web
```

## 7. Backup Database

Create a SQL backup:

```powershell
.\scripts\windows\backup-db.ps1
```

Backups are written into:

```text
backups/
```

The backup folder is ignored by git, so database files are not committed.

## 8. Restore Database

Restoring is protected by a confirmation switch:

```powershell
.\scripts\windows\restore-db.ps1 -BackupFile ".\backups\garmetix-YYYYMMDD-HHMMSS.sql" -ConfirmRestore
```

Before restoring, stop app users from entering new data.

## 9. First Admin

On a fresh database, open the login screen and create the first admin. After an admin exists, the First Admin tab is hidden and normal login is used.

## 10. Public Domain Checklist

Use these checks when `https://garmetix.aadwikafashion.in` is not working:

- Docker web container is running.
- `http://localhost:3000` opens on the Windows machine.
- `http://localhost:3000/api/health` returns `databaseReady: true`.
- Cloudflare tunnel points to `http://localhost:3000`.
- Public `https://garmetix.aadwikafashion.in/api/health` returns `databaseReady: true`.
