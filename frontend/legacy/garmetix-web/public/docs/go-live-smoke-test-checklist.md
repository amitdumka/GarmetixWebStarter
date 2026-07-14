# Garmetix Go-Live Smoke Test Checklist

Run this checklist before first live billing and after every production update.

## Automated checks

1. API health returns healthy.
2. Admin login works.
3. Release Stabilization smoke checks run.
4. Production Readiness has no critical blockers.
5. Data Consistency has no critical issue.
6. Backup verification passes.
7. Restore preflight passes on latest backup.

## Manual business checks

1. Create or verify one product with barcode, HSN, tax, unit, product group, and stock.
2. Scan barcode in Billing.
3. Create one cash sale.
4. Create one split-payment sale.
5. Print/download sale receipt.
6. Create one sales return.
7. Create one purchase inward.
8. Create one partial purchase return.
9. Run GST HSN summary.
10. Run stock movement ledger report.
11. Run backup after tests.

## Command-line smoke test

```bash
scripts/linux/smoke-test.sh .env.production
GARMETIX_SMOKE_USER=admin GARMETIX_SMOKE_PASSWORD='password' scripts/linux/smoke-test.sh .env.production
```

Windows PowerShell:

```powershell
scripts/windows/smoke-test.ps1 -EnvFile .env.production
$env:GARMETIX_SMOKE_USER='admin'; $env:GARMETIX_SMOKE_PASSWORD='password'; scripts/windows/smoke-test.ps1 -EnvFile .env.production
```

## Go / no-go rule

Do not go live if any of these fail:

- Database health
- Admin login
- Backup verification
- Restore preflight
- Negative stock check
- GST header/item mismatch check
- Production secret/CORS checks
