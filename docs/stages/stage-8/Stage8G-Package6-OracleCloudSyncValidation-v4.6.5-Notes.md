# Stage 8G Package 6 - Oracle Cloud Sync Validation (v4.6.5)

This package continues production go-live validation with a focused Oracle Cloud / external-app synchronization readiness layer.

## Included

- Mac mini readiness script for Oracle wallet/TNS path checks and sync endpoint probes.
- Go-live documentation for Oracle Cloud wallet/TNS, external-app event review, manual apply, auto-apply policy, and dead-letter checks.
- Version marker updated to v4.6.5 / `GARMETIX-8G-20260617-4650`.

## Operator flow

1. Copy Oracle wallet files to a secure directory on the Mac mini.
2. Configure `ORACLE_SYNC_TNS_ADMIN`, `ORACLE_SYNC_WALLET_DIRECTORY`, `ORACLE_SYNC_CONNECTION_STRING`, direction, and trusted sources in `.env.production`.
3. Run `./scripts/linux/oracle-cloud-readiness-check.sh .env.production`.
4. Use the Oracle Sync screen to test connection, pull external app events, review inbound queue, apply/reject, then enable auto-apply only for safe entities.
