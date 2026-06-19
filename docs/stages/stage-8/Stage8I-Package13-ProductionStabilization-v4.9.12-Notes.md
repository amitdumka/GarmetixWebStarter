# Stage 8I Package 13 - Production Stabilization Repair Pack (v4.9.12)

This package stabilizes Stage 8I before the next large HR/Attendance or SaaS licensing module.

## Included fixes

- Added explicit frontend route access rules for Cash Details, Store Day, Tailoring, backup and final acceptance pages.
- Changed Cash Details API from generic authenticated access to the Accounting permission matrix.
- Hardened Cash Details create/update payload validation.
- Prevented linked Day Opening/Closing cash details from changing store, date or source from the Cash Details edit screen.
- Aligned Store Day API with the Billing permission matrix for store operational roles.
- Synced frontend package, backend AppInfo, and .NET assembly metadata to `4.9.12`.
- Restored direct Petty Cash local-date save payload expected by the current release validator.
- Added Package 13 static validation script.

## Build code

```text
GARMETIX-8I-20260619-49120
```
