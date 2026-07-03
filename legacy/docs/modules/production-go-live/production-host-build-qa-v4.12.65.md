# Production Host Build QA - v4.12.65

This page is for final runtime proof after the package is built on the actual Docker host.

Open **Maintenance → Host Build QA** after running:

```bash
docker compose up --build
```

## What it verifies automatically

- API can connect to PostgreSQL.
- Core tables can be queried without schema drift errors.
- Purchase return, ITC reversal, vendor settlement, journal and bank tables are queryable.
- API environment, JWT signing key and CORS configuration are reviewed.

## What must be verified manually

- Docker build finished without API/Nuxt errors.
- Public API health endpoint works.
- Login works by role.
- Sidebar links open.
- CSV exports download.
- Sale/purchase/return PDFs open.
- Backup/restore drill is complete.
- Message Logs have no unhandled errors.

Keep the exported CSV with the build log, backup proof and owner sign-off PDF.
