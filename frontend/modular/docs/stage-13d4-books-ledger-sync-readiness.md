# Stage 13D.4 Books Ledger Sync Readiness

Version: 5.13.28
Branch: Version5

## Scope

This stage checks that party-ledger and bank-ledger synchronization remains an internal accounting concern. The modular Books pages can show linked/missing status, but internal flags such as party ledger flags are not exposed as user-editable fields.

## Commands

Dry run:

```powershell
npm.cmd run modular:books:ledger-sync-readiness
```

Live API check:

```powershell
[Environment]::SetEnvironmentVariable('GARMETIX_SMOKE_AUTH_TOKEN', '<token>', 'Process')
npm.cmd run modular:books:ledger-sync-readiness -- --live --require-token --strict-permissions
```

Fail when sync issues are present:

```powershell
npm.cmd run modular:books:ledger-sync-readiness -- --live --fail-on-sync-issues
```

## Safety

- `GET /api/accounting/ledger-sync/status` is checked.
- `GET /api/parties`, `GET /api/bank-accounts`, and `GET /api/ledgers` are checked.
- Ledger repair endpoints are intentionally not called.
- No ledger, party, bank account, voucher, or journal data is modified.

## Acceptance Notes

- Parties and bank accounts may show whether a ledger link exists.
- Internal flags like `IsParty` are not exposed to the user as editable fields.
- Existing sync issues are warnings by default so production data can be reviewed before repair.
