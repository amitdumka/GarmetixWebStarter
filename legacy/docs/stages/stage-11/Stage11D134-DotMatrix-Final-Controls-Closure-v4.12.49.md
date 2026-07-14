# Stage 11D-134 — DotMatrix Final Controls Closure (v4.12.49)

## Goal

Close the Dot Matrix Daily Audit Journal code work so the remaining work is physical printer/build QA only.

## Completed

- Stable deduplication for manual/test/day opening/day closing queue entries.
- Exact-row reprint action with `REPRINT COPY` header.
- Admin pause/resume store printing controls.
- Queue status counters on the Dot Matrix page.
- Queue reprint endpoint and UI button.
- Bridge health/status script for Ubuntu service, CUPS printer queue and recent logs.
- README-dotmatrix updated for final operations, printer change, queue controls and QA checklist.

## Architecture

Production remains safe and non-blocking:

```text
Garmetix API -> DotMatrixPrintQueueEntries -> Ubuntu Bridge -> CUPS/lp -> Epson LX-810
```

The API does not wait for printer hardware during billing. Printer failures stay in the queue for retry/reprint.

## Remaining QA only

- Run `dotnet build` on a machine with the .NET SDK.
- Run frontend build.
- Deploy on Ubuntu and verify CUPS queue `EPSON_LX810`.
- Perform actual Epson LX-810 alignment tests for 80 and 136 columns.
- Verify live sale, voucher, receipt, purchase, edit, delete, day opening and day closing paper output with real data.
