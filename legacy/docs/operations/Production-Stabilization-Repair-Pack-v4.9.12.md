# Production Stabilization Repair Pack v4.9.12

This package locks down the Stage 8I production-facing gaps found after the Cash Details Register package.

## Frontend route access repair

Explicit access rules are now configured for these routes:

- `/cash-details` - Accounting, Remote Accountant, Store Manager, Power User, Admin/Owner
- `/store-day` - Billing/Store operations users, Store Manager, Power User, Admin/Owner
- `/tailoring` - Billing/Store operations users, Store Manager, Power User, Admin/Owner
- `/backup-maintenance` - Admin/Owner only
- `/gst-final-acceptance` - Admin/Owner only
- `/print-final-acceptance` - Admin/Owner only
- `/permission-final-acceptance` - Admin/Owner only
- `/post-go-live-acceptance` - Admin/Owner only
- `/stage8g-completion` - Admin/Owner only

## Cash Details API hardening

`/api/cash-details` now requires the Accounting matrix policy instead of only a valid login.

Payload validation now blocks:

- empty store id
- missing date
- negative note/coin counts
- negative cash amount

## Linked day-record protection

When a cash detail is linked to Day Opening or Day Closing, edit is limited to amount and denomination counts.

Blocked during linked edit:

- changing store
- changing date
- changing source

This keeps Day Opening/Closing records from being moved accidentally from the Cash Details screen.

## Store Day API access alignment

`/api/store-day` now uses the Billing matrix policy, matching operational Store Day roles such as Store Manager and Salesman while still allowing Admin/Owner and Power User.

## Validation

Run the static check:

```bash
python3 scripts/validation/stage8i-package13-static-checks.py
```

Then run Docker build and smoke test on your deployment machine.

## Version metadata

Frontend package/app metadata, backend AppInfo and .NET assembly metadata now report v4.9.12 with build code `GARMETIX-8I-20260619-49120`.

## Petty Cash local date validator compatibility

Petty Cash save payload now sends the selected local date directly as `${form.onDate}T00:00:00`, preserving India/local calendar dates and satisfying the current release static validator.
