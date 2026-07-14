# Stage 11D-133 — DotMatrix Live Transaction Polish (v4.12.48)

## Goal

Make the Dot Matrix Daily Audit Journal safer and more useful for real store operations before physical Epson LX-810 QA.

## Completed

- Store-wise queue gating: live transaction queue rows are created only when Dot Matrix printing is enabled for the store.
- Store code/name line polish: transaction paper lines now use the store code instead of a short StoreId/GUID fragment.
- Stable deduplication keys: source/action/change-hash keys reduce accidental duplicate print rows.
- Better live transaction particulars for sale invoice, purchase inward, voucher, cash voucher, customer receipt, vendor payment, commercial note, bank/cash and attendance punch rows.
- Edit/delete audit polish: printed correction rows include a full-width heading, user, optional reason/remark and important field changes.
- Bank/UPI summary coverage now includes sale invoice payments, customer advances, purchase payments and non-cash vouchers.
- Attendance summary now includes employee code/name, check-in, check-out, total hours, status and missing check-out list.
- Admin queue actions added: Retry failed, Reset stuck and raw Text preview.
- DotMatrix README updated with operations, queue controls and production-safe worker guidance.

## Architecture retained

Main API remains a queue producer. The Ubuntu host-side DotMatrix Bridge remains the only production printer worker.

```text
Garmetix API -> DotMatrixPrintQueueEntries -> Ubuntu Bridge -> CUPS/lp -> Epson LX-810
```

## Pending QA

- Run `dotnet build` on a machine with the .NET SDK.
- Run frontend build.
- Deploy on Ubuntu and verify CUPS queue `EPSON_LX810`.
- Perform real Epson LX-810 80/136 column alignment test.
- Verify live sale, voucher, receipt, purchase, edit and delete paper lines with real data.
