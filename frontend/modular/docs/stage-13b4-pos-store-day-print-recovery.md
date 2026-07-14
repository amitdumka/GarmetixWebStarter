# Stage 13B.4 - POS Store Day Print Recovery

Version: 5.13.9

## Scope

This stage keeps the shared ASP.NET Core API and PostgreSQL database unchanged. It hardens the modular POS store-day screens around mixed API response shapes and petty cash PDF printing after close.

## Added

- Shared petty cash PDF opening through the POS document helper.
- Store-day response helpers for direct status responses and wrapped close responses.
- Store-day error payload parsing helper for opening-balance confirmation conflicts.
- Day close now attempts petty cash print after a successful close.
- Day close success is preserved even if the browser blocks or fails the petty cash print window.

## Updated Workflows

- Day Open: normalizes returned status and gives a clearer entry-allowed success message.
- Holiday / Closed Day: normalizes returned status and uses the API status message.
- Day Close: stores close response, updates status, then opens petty cash PDF; print failure becomes a warning, not a false close failure.
- Reopen / Delete Close: normalizes returned status after correction.
- Print Petty Cash: uses the same browser-block aware PDF helper.

## Validation

Run:

```powershell
npm.cmd --prefix modular run build:pos
npm.cmd run modular:check
npm.cmd run modular:validate -- --skip-builds
```

## Remaining Follow-Up

Server-side print audit and multi-terminal petty cash print recovery can be added later if required for store audit policy.
