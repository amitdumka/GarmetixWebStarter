# Stage 13B.11 POS Save After Resume Readiness

Version: 5.13.16
Branch: Version5

## Scope

This stage adds a safe readiness check for the POS path where a cashier resumes a held bill and then continues toward Save & Print. It does not save a real invoice, print a real invoice, alter the database, or require test credentials.

## Added

- Script: `modular/scripts/pos-save-after-resume-readiness.mjs`
- Root command: `npm.cmd run modular:pos:save-after-resume`
- Modular command: `npm.cmd --prefix modular run pos:save-after-resume`
- Validation coverage in `modular/scripts/validate-all.mjs`

## What It Checks

The script verifies that:

- Hold Bills writes the resumed draft to POS sale draft storage.
- Hold Bills navigates resumed work back to `/sale`.
- Sale page restores `garmetix.pos.sale.draft.v1`.
- Sale page builds the backend `billing/sales` request through the shared sale contract helper.
- Sale page blocks non-cash payments without a bank account.
- Sale page adds print recovery after a successful save.
- Sale page clears the draft after the save path.
- Sale contract carries nullable `salesmanId`.
- Backend can resolve or create the default `Manager` salesman when a resumed draft has no salesman id.

## Draft Safety

The seeded readiness draft is in-memory only. It confirms:

- company, store group and store ids are present
- at least one sale item exists
- quantity is positive
- paid amount is not above bill amount
- cash payment does not require a bank account
- non-cash payment requires a bank account

## Command

```powershell
npm.cmd run modular:pos:save-after-resume
```

## Validation

- `npm.cmd run modular:pos:save-after-resume`
- `npm.cmd run modular:check`
- `npm.cmd run modular:validate -- --skip-builds`

## Remaining Follow-Up

- Run a true save-after-resume live acceptance after disposable products, store, stock, salesman and payment fixtures are available.
- Keep browser-level save-and-print tests opt-in because they create real billing data.
