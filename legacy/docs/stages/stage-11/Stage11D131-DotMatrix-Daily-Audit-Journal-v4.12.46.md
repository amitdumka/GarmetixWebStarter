# Stage 11D-131 — Dot Matrix Daily Audit Journal (v4.12.46)

## Goal

Add an Epson/ESC-P style dot-matrix daily audit journal for store operations. The feature is designed for Ubuntu server/desktop deployments where the Epson dot-matrix printer is attached to the host machine.

## Implemented roadmap

1. **Print queue foundation**
   - Added `DotMatrixPrintSettings` for store-wise printer configuration.
   - Added `DotMatrixPrintQueueEntries` for pending/printed/failed/skipped print jobs.
   - Queue entries preserve source type, source id, source number, business date, operation time, action type, amount, mode and the final fixed-width text.

2. **Immediate transaction audit lines**
   - SaveChanges now queues print lines after records are added to the database unit of work.
   - Covered source types include sale invoice, purchase inward, voucher, cash voucher, customer receipt, vendor payment, customer advance receipt, commercial note, bank/cash transaction, manual cash detail and attendance punch.
   - Transaction lines are fixed-width and use the operation print time in IST.

3. **Edit/delete audit lines**
   - Edited records print with `EDITED RECORD BELOW` header.
   - Deleted/cancelled records print with `DELETED / CANCELLED RECORD BELOW` header.
   - Changed fields are summarized as old value -> new value where available.

4. **Day opening print**
   - Day opening queues a full-width section with store, operation time, opened-by user and cash denomination details.
   - Opening cash details are printed before normal transaction journal lines.

5. **Day closing summary print**
   - Day closing queues a full-width closing section with cash denomination details and a compact daily summary.
   - Summary includes Sales Count, Sales Amount, Cash Sales, UPI Sales, Card/Bank Sales, Customer Receipts, Vendor Payments, Expenses, Cash Vouchers, Purchase Inward, Opening Cash, Cash In, Cash Out and Closing Cash.
   - Bank / UPI summary is printed in multi-column rows to use 132/136-column paper width.
   - Attendance details include present employees with check-in/check-out, absent employee names and late check-in names.

6. **Admin UI**
   - Added `/dot-matrix-print` page under Accounting.
   - Supports store-wise settings, test print queueing, queue viewer, retry and skip actions.
   - Avoids empty-string select values to prevent Nuxt UI SelectItem runtime issues.

7. **Ubuntu deployment support**
   - Added CUPS/spool deployment configuration.
   - Added `deploy/install-dotmatrix-printer-ubuntu.sh`.
   - Added host-side spool sender `deploy/print-dotmatrix-spool.sh`.
   - Added systemd timer/service under `deploy/systemd/`.

## Configuration

Default API configuration:

```json
"DotMatrixPrinting": {
  "Enabled": false,
  "RunWorker": true,
  "PrinterName": "",
  "OutputMode": "SpoolFile",
  "SpoolDirectory": "/app/data/dotmatrix-spool",
  "TimeZoneId": "Asia/Kolkata",
  "LineWidth": 136,
  "PollSeconds": 5,
  "BatchSize": 20,
  "RetryLimit": 10,
  "LpCommand": "lp",
  "LpArgumentsTemplate": "-d {printer} -o raw"
}
```

Recommended production flow:

- Keep API `OutputMode=SpoolFile`.
- Bind mount `./data/dotmatrix-spool:/app/data/dotmatrix-spool`.
- Install the Ubuntu host spooler and let the host call `lp` against the CUPS Epson printer queue.

## Operator sequence

1. Add Epson printer in Ubuntu CUPS.
2. Run `deploy/install-dotmatrix-printer-ubuntu.sh` and provide the CUPS printer name.
3. In Garmetix, open **Accounting → Dot Matrix Print**.
4. Select store, enable printing, set printer name and line width 136.
5. Queue a test print.
6. Do Day Opening: opening cash details print first.
7. Transactions print immediately after DB save.
8. Edits/deletes print audit correction lines.
9. Do Day Closing: closing cash details and full-width day summary print.

## Known notes

- The app uses `Asia/Kolkata` for printed time. Docker should also be run with `TZ=Asia/Kolkata` for consistency.
- Spool mode marks the app queue entry printed once the spool file is written; the host spooler then sends the file to CUPS and moves it to `printed/` or `failed/`.
- Direct `LpCommand` mode is available but is not the recommended Docker production mode unless the API container has CUPS/client access and printer visibility.
