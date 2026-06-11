# Oracle Secondary Sync v2 Notes

This version upgrades the Oracle Cloud secondary database feature from push-only to a safe bidirectional-ready bridge.

## Direction modes

`OracleSync:Direction` supports:

- `PushToOracle` / `PushOnly`: PostgreSQL primary rows are pushed to Oracle hub events.
- `PullFromOracle` / `PullOnly`: external Oracle events are pulled into local review storage.
- `Bidirectional` / `TwoWay`: push and pull run in one sync pass.

## Conflict policy

`OracleSync:ConflictPolicy` supports:

- `ManualReview` — default. External Oracle events are stored locally for review.
- `GarmetixWins` — external events are stored for audit and marked ignored by Garmetix.
- `OracleWins` — external events are queued as candidates for future guided apply.
- `LatestWins` — external events are queued until entity version comparison rules are approved.

The app intentionally does **not** auto-overwrite PostgreSQL records yet. This protects billing, stock, GST, and accounting data until the entity ownership matrix is approved.

## Oracle wallet / Autonomous DB

For Oracle Autonomous Database Free Tier, unzip the wallet into a folder mounted into the API container and set either:

```env
ORACLE_SYNC_WALLET_DIRECTORY=/app/oracle-wallet
ORACLE_SYNC_TNS_ADMIN=/app/oracle-wallet
```

Then set the connection string using your TNS alias, for example:

```env
ORACLE_SYNC_CONNECTION_STRING=User Id=ADMIN;Password=your-password;Data Source=yourdb_high
```

The API sets the `TNS_ADMIN` environment variable before opening the Oracle connection.

## Local safety tables

The app repairs/creates these local PostgreSQL sync support tables:

- `OracleSyncState`
- `OracleSyncRuns`
- `OracleSyncInboundEvents`
- `OracleSyncDeadLetters`

## Oracle hub tables

The app repairs/creates these Oracle hub tables:

- `GARMETIX_SYNC_EVENTS`
- `GARMETIX_SYNC_STATE`
- `GARMETIX_SYNC_DEAD_LETTERS`

## API endpoints

- `GET /api/oracle-sync/status`
- `GET /api/oracle-sync/history`
- `GET /api/oracle-sync/inbound`
- `GET /api/oracle-sync/dead-letters`
- `POST /api/oracle-sync/test`
- `POST /api/oracle-sync/repair`
- `POST /api/oracle-sync/run`
- `POST /api/oracle-sync/pull`
- `POST /api/oracle-sync/dead-letters/{id}/retry`
- `POST /api/oracle-sync/dead-letters/{id}/resolve`

## Next guided decision

Before enabling automatic inbound merge, decide which app owns each entity. Recommended starting point:

| Entity | Recommended owner |
|---|---|
| Billing, purchase, stock | Garmetix |
| Customer profile | Shared or CRM |
| Vendor profile | Garmetix or procurement app |
| Employee profile | HR app |
| GST returns/accounting | Garmetix |
| Product catalog | Garmetix or catalog app |

After that decision, implement entity-specific merge rules.
