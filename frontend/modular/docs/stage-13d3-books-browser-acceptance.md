# Stage 13D.3 Books Browser Acceptance

Version: 5.13.27
Branch: Version5

## Scope

This stage adds repeatable browser acceptance checks for key Books pages on a 14 inch laptop viewport.

## Commands

Dry run:

```powershell
npm.cmd run modular:books:browser-acceptance
```

Live browser check:

```powershell
npm.cmd run modular:books:browser-acceptance -- --live
```

## Covered Routes

- `/accounting`
- `/vouchers`
- `/petty-cash`
- `/cash-details`
- `/audit`
- `/financial-year-locks`
- `/gst-returns`
- `/gst-reports`

## Acceptance Rules

- Each route renders its expected heading.
- The page remains usable at `1366x768`.
- Wide tables scroll inside their section instead of pushing the full page shell out of view.
- Console errors are warnings by default and failures with `--strict-console`.

## Safety

The live browser check seeds local auth state only. It does not create vouchers, bank entries, journal entries, GST posts, locks, or petty cash sheets.
