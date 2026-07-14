# Stage 13G.13 SRP Dashboard Runtime Repair

Version: 5.13.53

## Problem

After the SRP public redirect repair, the Back Office login page opened and authentication succeeded, but the main dashboard still displayed:

```text
Garmetix API request failed with 404
```

The SRP Nginx access log showed the browser was calling:

```text
GET /api/
```

instead of the intended:

```text
GET /api/dashboard/business
```

The dashboard title and description were also blank in the rendered page, meaning the read-model component needed a runtime-safe fallback when page props are missing in a static deployment.

## Fix

`MainDashboardReadModel` now derives safe route-based defaults when props are empty:

- `/` and `/dashboard` use `dashboard/business`
- `/reports` uses `dashboard/business` with report wording
- `/dashboard/todays` uses `dashboard/todays`
- `/dashboard/store-manager` uses `dashboard/store-manager`

The component now uses effective title, description, endpoint and row-key values for both rendering and API loading.

## Validation

Run:

```bash
npm run check
npm run build:main
curl -k -I https://srp.aadwikafashion.in/login
curl -k -I https://srp.aadwikafashion.in/pos
curl -k https://srp.aadwikafashion.in/api/health
```

Expected:

- no public redirect exposes `:8088`
- `/api/health` returns healthy JSON
- after login, the Back Office dashboard calls `/api/dashboard/business` and no longer shows the `/api/` 404

