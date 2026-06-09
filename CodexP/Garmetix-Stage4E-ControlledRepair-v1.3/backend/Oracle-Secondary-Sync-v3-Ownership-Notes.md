# Oracle Secondary Sync v3 - Entity Ownership and Inbound Merge

This version keeps PostgreSQL as the primary transactional database and Oracle Cloud as the common hub for connected apps.

## Ownership matrix

The Oracle Sync page now exposes an entity ownership matrix from `GET /api/oracle-sync/ownership`.

Default rules:

- Shared master data can be applied after review: `Company`, `StoreGroup`, `Store`, `Customer`, `Vendor`, `Product`, `ProductCategory`, `ProductSubCategory`, and `Employee`.
- Garmetix-owned transactional data is blocked by default: sales invoices, purchase invoices, stock, vouchers, cash vouchers, debit/credit notes, customer advances, GST returns, journals, transactions, loyalty ledgers, petty cash, and payslips.

## Inbound queue

External Oracle events are pulled into `OracleSyncInboundEvents`. They are not applied automatically by default.

Admin actions added:

- `POST /api/oracle-sync/inbound/{id}/apply`
- `POST /api/oracle-sync/inbound/{id}/reject`

The UI shows Apply/Reject buttons in `/oracle-sync`.

## Conflict policy

- `ManualReview`: pulled events remain reviewable and can be manually applied.
- `GarmetixWins`: local row is kept unless admin force-applies.
- `OracleWins`: inbound row may overwrite shared master fields when applied.
- `LatestWins`: local row is kept when the local version is newer than Oracle payload.

## Merge safety

Only scalar fields are copied during apply. Navigation properties and computed/not-mapped properties are ignored.

For delete events, shared master rows are soft-deleted by setting `Deleted=true` when the local row exists.

## Next real-world step

Connect the Oracle Free Tier database and one external app, then confirm which shared master entities should stay manual-review versus auto-apply.
