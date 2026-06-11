# Oracle Secondary Sync v5 — Real Oracle + External App Test Kit

This stage adds a practical validation path for Oracle Cloud Free Tier and one connected external app.

## What was added

- `GET /api/oracle-sync/external-app-test-plan`
- `POST /api/oracle-sync/external-app-test`
- Oracle Sync UI button: **External App Test**
- External app simulator assets:
  - `tools/oracle-external-app-simulator/README.md`
  - `tools/oracle-external-app-simulator/insert-sample-customer-event.sql`
  - `scripts/oracle-external-app-smoke-test.ps1`
  - `scripts/oracle-external-app-smoke-test.sh`

## What the smoke test does

1. Repairs local Oracle sync tables.
2. Opens the configured Oracle connection.
3. Repairs Oracle hub tables.
4. Seeds one `Customer` event into `GARMETIX_SYNC_EVENTS` with `SOURCE_APPLICATION = ExternalAppSmokeTest`.
5. Pulls the event back into Garmetix inbound review queue.
6. Leaves the event pending review, unless your auto-apply rules explicitly allow it.

## Why this is safe

The test uses shared master data only. It does not create billing, purchase, stock, GST, accounting, or ledger records automatically.

## Recommended first real test

1. Set Oracle env variables.
2. Run Docker with no-cache API rebuild.
3. Log in as Admin.
4. Open `/oracle-sync`.
5. Run **Test Oracle**.
6. Run **Repair Storage**.
7. Run **External App Test**.
8. Check the inbound queue.
9. Apply or reject the sample customer.

## Pending after this stage

- Run this test against the real Oracle Cloud Free Tier wallet/connection.
- Connect a second real app and write one real event into `GARMETIX_SYNC_EVENTS`.
- Decide which shared master entities can be auto-applied in production.
