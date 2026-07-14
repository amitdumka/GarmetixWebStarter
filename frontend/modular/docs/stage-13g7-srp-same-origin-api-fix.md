# Stage 13G.7 SRP Same-Origin API Fix

Version: 5.13.47

## Problem

The SRP site loaded from `http://192.168.11.127:8088`, but the browser-side API config still pointed to a host that was not reachable from that browser context. The visible symptom was:

- API service: `API unreachable`
- Login error: `Failed to fetch`
- Current stage still showed an older deployed build

## Fix

- SRP whole-site deployment now defaults to path-based same-origin URLs:
  - API: `/api`
  - POS: `/pos/`
  - HR: `/hr/`
  - AI Sense: `/ai-sense/`
  - Books: `/books/`
  - Admin: `/admin/`
- Shared API client now supports relative API base URLs by resolving them against `window.location.origin`.

## Why This Works

The same static build now works from both:

- LAN: `http://192.168.11.127:8088`
- Cloudflare later: `https://srp.aadwikafashion.in`

The browser calls the API through the same origin at `/api`, and Nginx proxies it to the ASP.NET Core service.

## Verification

After redeploy:

```bash
npm run modular:deploy:srp:acceptance -- --live
```

Then open:

```text
http://192.168.11.127:8088/login
```

Expected:

- Current stage shows `Stage 13G.7 SRP Same-Origin API Fix`
- API service shows live
- Login request reaches `/api/auth/login`
