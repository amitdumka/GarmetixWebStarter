# Oracle Secondary Sync v4 - Cloud Readiness and Auto-Apply Policy

This update adds the next Oracle sync layer after the ownership matrix.

## What changed

- Added Oracle Cloud readiness endpoint and UI panel.
- Added explicit trusted-source auto-apply policy endpoint and UI table.
- Added a guarded auto-apply action for inbound Oracle events.
- Added config keys for auto-apply entity allowlist and trusted external source applications.
- Fixed missing Oracle v3 helper methods used by inbound ownership/apply logic.

## New backend endpoints

- `GET /api/oracle-sync/cloud-readiness`
- `GET /api/oracle-sync/auto-apply-policy`
- `POST /api/oracle-sync/inbound/auto-apply`

## Safe auto-apply rules

Auto-apply only runs when all conditions are true:

1. `ORACLE_SYNC_APPLY_INBOUND_AUTOMATICALLY=true`
2. The entity appears in `ORACLE_SYNC_AUTO_APPLY_ENTITIES`.
3. Ownership matrix allows inbound apply and auto-apply for that entity.
4. The source application appears in `ORACLE_SYNC_TRUSTED_SOURCES`, unless `ORACLE_SYNC_REQUIRE_TRUSTED_SOURCE_FOR_AUTO_APPLY=false`.
5. The event is still in `PendingReview` status.

Transactional, GST, accounting, stock, loyalty ledger, and invoice data remain blocked from automatic inbound overwrite.

## Suggested production starting config

```env
ORACLE_SYNC_DIRECTION=Bidirectional
ORACLE_SYNC_PULL_EXTERNAL_EVENTS=true
ORACLE_SYNC_APPLY_INBOUND_AUTOMATICALLY=false
ORACLE_SYNC_AUTO_APPLY_ENTITIES=Customer,Vendor,Product,ProductCategory,ProductSubCategory
ORACLE_SYNC_TRUSTED_SOURCES=ExternalCRM,ExternalInventoryApp
ORACLE_SYNC_REQUIRE_TRUSTED_SOURCE_FOR_AUTO_APPLY=true
ORACLE_SYNC_AUTO_APPLY_BATCH_SIZE=50
```

Keep `ORACLE_SYNC_APPLY_INBOUND_AUTOMATICALLY=false` until a real Oracle Cloud Free Tier database and connected external app are tested.

## Oracle Free Tier readiness

For Autonomous Database, mount the wallet folder and set:

```env
ORACLE_SYNC_TNS_ADMIN=/app/secrets/oracle-wallet
ORACLE_SYNC_CONNECTION_STRING=User Id=APP_USER;Password=APP_PASSWORD;Data Source=your_low_tns_alias
```

Then use the Oracle Sync page:

1. Test Oracle.
2. Repair Storage.
3. Pull.
4. Review inbound queue.
5. Apply/Reject manually.
6. Enable trusted auto-apply only after validation.
