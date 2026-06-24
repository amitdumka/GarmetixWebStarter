# Stage 13B.2 - POS Local Workflow Hardening

Version: 5.13.7

## Scope

This stage keeps the shared ASP.NET Core API and PostgreSQL database unchanged. It hardens the modular POS frontend local browser workflows before adding deeper server-side writable checks.

## Added

- Central POS browser storage helper for sale drafts, held bills and print queue entries.
- Shared queue limits and corrupt JSON recovery for browser-local POS storage.
- Duplicate-safe upsert behavior for held bills and print queue entries.
- Duplicate action guards for return save, invoice print, day open/holiday and day close/correction actions.

## Touched Workflows

- New Sale: saves drafts and held bills through the shared helper.
- Hold Bills: reads, restores, removes and clears held bills through the shared helper.
- Print Queue: reads, clears and marks local print jobs through the shared helper.
- Sales Returns: adds return documents to the same print queue helper.
- Day Open and Day Close: ignores repeated actions while an API operation is already running.

## Validation

Run:

```powershell
npm.cmd run modular:check
npm.cmd --prefix modular run build:pos
npm.cmd run modular:validate -- --skip-builds
```

## Notes

The local queues remain browser-local by design. Server persistence for held bills or print queue audit can be added later if a store needs multi-terminal recovery.
