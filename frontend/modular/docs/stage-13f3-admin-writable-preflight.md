# Stage 13F.3 Admin/SaaS Writable Preflight

Version: 5.13.38
Branch: Version5

## Purpose

Stage 13F.3 adds a guarded writable/live preflight for Admin/SaaS operations that can affect production state. The gate verifies backend endpoint shape, DTO contracts, confirmation phrases and modular UI read-only guardrails without executing dangerous actions.

## Covered Operations

- Local backup create, restore preview, restore and delete.
- Backup maintenance cleanup.
- Google Drive backup upload, delete and restore.
- Factory reset.
- License generation, activation and activation removal.
- Import/export validation and commit.

## Safety Rules

- Dry-run is the default.
- Live mode performs only safe GET checks for prerequisites and status.
- The script prints every skipped POST/DELETE operation.
- Supplying `--allow-dangerous-mutation` only records a warning; this gate still refuses mutation.
- Factory reset remains hidden from modular Admin UI because the backend is Admin-policy protected but not yet SuperAdmin-only.

## Commands

```bash
npm run modular:admin:writable-preflight
```

Optional live prerequisite check:

```bash
npm run modular:admin:writable-preflight -- --live --require-token --strict-permissions
```

## Next Step

Stage 13F.4 should close the Admin/SaaS lane with a closure gate covering readiness, browser acceptance, writable preflight, deployment docs, validation wiring and remaining risks.
