# Stage 13B.8 POS Server Held Bills

Version: 5.13.13
Branch: Version5

## Scope

This stage moves POS held bills from browser-only storage to server-backed persistence while keeping the existing browser localStorage fallback.

## Added Backend

- Domain model: `PosHeldBill`
- DbSet and EF model configuration for `PosHeldBills`
- Idempotent schema repair: `RepairPosHeldBillStorageAsync`
- API route group: `/api/pos/held-bills`

## API Routes

- `GET /api/pos/held-bills`
- `POST /api/pos/held-bills`
- `DELETE /api/pos/held-bills/{id}`
- `DELETE /api/pos/held-bills`

The endpoints use the existing Billing authorization policy and `WorkspaceScope` so held bills stay company/store scoped.

## Added Frontend Behavior

- Sale still writes a held bill to local storage first.
- Sale then attempts to sync the held bill to the API.
- Hold Bills loads server rows and merges local fallback rows.
- Resume writes the draft back to the Sale page and removes the server/local held bill.
- Remove and Clear now call the API when possible and keep local fallback cleanup.
- Local held bill ids are sent as `clientHeldBillId`; backend `id` remains a Guid to avoid JSON Guid parsing errors.

## Operational Notes

- Held bills still do not affect accounting, stock or invoices until `Save & Print`.
- If the API is unavailable, the cashier can continue with browser-local held bills.
- Once the API is reachable, new held bills can be resumed from another POS browser in the same workspace.

## Validation

- `npm.cmd run legacy:api:build`
- `npm.cmd --prefix modular run build:pos`
- `npm.cmd run modular:check`
- `npm.cmd run modular:deploy:preflight`
- `npm.cmd run modular:validate -- --skip-builds`

## Remaining Follow-Up

- Add live browser/API acceptance once a local test login and sample stock are available.
- Add optional held-bill expiry or stale cleanup rules if store operations require it.
