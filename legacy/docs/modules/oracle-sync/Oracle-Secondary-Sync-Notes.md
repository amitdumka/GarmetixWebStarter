# Oracle Cloud Secondary Database Sync

This module adds a secondary Oracle database sync layer for Garmetix.

## Purpose

- PostgreSQL remains the primary transactional database for Garmetix.
- Oracle Cloud Free Tier / Autonomous Database can act as a shared common-ground database for other connected apps.
- The first implementation is **push-first** from PostgreSQL to Oracle.
- Inbound/bidirectional merge rules are intentionally not enabled yet because they need business rules for conflicts, ownership, deletes, and edits made by external apps.

## What gets created locally

The API creates these PostgreSQL helper tables automatically:

- `OracleSyncState`
- `OracleSyncRuns`

These tables store checkpoints and last run state only. They do not replace your local business tables.

## What gets created in Oracle

When `OracleSync:CreateOracleSchema=true`, the API creates these Oracle hub tables:

- `GARMETIX_SYNC_EVENTS`
- `GARMETIX_SYNC_STATE`

Other apps can read `GARMETIX_SYNC_EVENTS` and apply the latest event per `TENANT_ID + ENTITY_NAME + ENTITY_ID`.

## API Endpoints

Admin-only endpoints:

- `GET /api/oracle-sync/status`
- `POST /api/oracle-sync/test`
- `POST /api/oracle-sync/repair`
- `POST /api/oracle-sync/run`

Request body for manual sync:

```json
{
  "entityName": "Customer",
  "repairFirst": true
}
```

Leave `entityName` null/empty to sync all configured entities.

## Supported entities

- Company
- StoreGroup
- Store
- Customer
- Vendor
- Product
- Stock
- Invoice
- PurchaseInvoice
- Voucher
- CashVoucher
- CommercialNote
- CustomerAdvanceReceipt
- LoyaltyProgram
- LoyaltyPointLedger
- GstReturnDraft
- JournalEntry
- JournalLine
- Transaction
- PettyCashSheet
- Employee
- SalaryPaySlip

## Configuration

`.env` example:

```env
ORACLE_SYNC_ENABLED=false
ORACLE_SYNC_RUN_ON_STARTUP=false
ORACLE_SYNC_INTERVAL_SECONDS=300
ORACLE_SYNC_BATCH_SIZE=250
ORACLE_SYNC_CONNECTION_STRING=User Id=your_oracle_user;Password=your_oracle_password;Data Source=your_oracle_tns_alias_or_connect_descriptor
ORACLE_SYNC_SCHEMA=
ORACLE_SYNC_TENANT_ID=garmetix-default
ORACLE_SYNC_SOURCE_APPLICATION=GarmetixWeb
ORACLE_SYNC_DIRECTION=PushToOracle
ORACLE_SYNC_CREATE_SCHEMA=true
ORACLE_SYNC_PUSH_DELETED_ROWS=true
ORACLE_SYNC_COMMAND_TIMEOUT_SECONDS=60
ORACLE_SYNC_ENTITY_NAMES=Customer,Vendor,Product,Stock,Invoice,PurchaseInvoice
```

For Oracle Autonomous Database wallet/TNS setup, mount the wallet directory into the API container later and use the Oracle connection string recommended by your Oracle DB setup.

## UI

Open:

```text
http://localhost:3000/oracle-sync
```

You can:

- Check config status
- Test Oracle connection
- Repair Oracle/local sync tables
- Run sync for all entities or one entity

## Next guided step

After the Oracle hub is stable, the next major enhancement can be inbound sync from Oracle to PostgreSQL. That requires decisions for:

- Which apps can create/update which entity types
- Conflict rules when the same record is edited in two apps
- Delete handling
- Approval/validation before accepting external writes
- Mapping external app fields to Garmetix models
