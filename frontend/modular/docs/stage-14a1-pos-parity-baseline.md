# Stage 14A.1 POS Parity Baseline

Version: `6.0.1`

This stage starts Version6 modular parity work with POS first. The goal is to
make one module complete and testable before moving to HR, Books, Main Back
Office, Admin/SaaS or AI Sense.

## Changes

- Moved the visible modular version stream from `5.13.65` to `6.0.1`.
- Marked the current stage as `Stage 14A.1 POS Parity Baseline`.
- Aligned modular workspace, app and shared package versions to Version6.
- Repaired POS route ownership so the registry points to POS-local routes:
  `/day-open`, `/sale`, `/hold-bills`, `/returns`, `/exchange`, `/print`,
  and `/day-close`.
- Added a non-mutating POS parity baseline script.

## Safety Rule

This stage does not create, edit, delete or post any live transaction data. It
only validates code structure, route ownership and the POS-first migration plan.

Before any controlled live POS write test:

- Take a `.127` database backup.
- Use a known test customer and test item where possible.
- Verify print/PDF output without blocking invoice save.
- Keep legacy frontend available as fallback.

## POS Acceptance Order

1. Sale invoice save and print.
2. Sale return.
3. Sale exchange.
4. Held bill hold/resume/remove.
5. Day open/day close.
6. Print queue and reprint.
7. DotMatrix queue handoff for store-day/daily journal.
8. Non-GST/off-book boundaries.

## Validation

Run:

```bash
npm run modular:pos:parity-baseline
npm run modular:check
```
