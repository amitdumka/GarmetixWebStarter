# Garmetix Dot Matrix Daily Audit Journal

Version: `v4.12.52`  
Stage: `Stage 11D-137 DotMatrix Queue Stats EF Fix`  
Default printer queue: `EPSON_LX310` for Epson LX-310 / compatible Epson ESC/P dot-matrix printers.


## v4.12.52 queue stats EF fix

- Fixed production 500 on `GET /api/dot-matrix-print/queue/stats` when opening Accounting → Dot Matrix Print.
- Cause: EF Core could not translate direct `GroupBy(...).Select(new DotMatrixQueueStatDto(...))` record-constructor projection.
- Fix: the endpoint now avoids `GroupBy` entirely and returns fixed status counters using simple EF-safe `CountAsync` filters.
- No printer/CUPS/systemd setting change is required for this patch.
## Purpose

This module prints a continuous paper audit journal for daily store operations:

- Day Opening with cash denomination/details and operation time.
- Live line-by-line transaction print immediately after the record is saved in the database.
- Edited/deleted/cancelled record audit lines.
- Day Closing with cash details and a full-width paper-saving day summary.
- Bank/UPI summary.
- Attendance details with present employees, check-in/check-out, absent names and late check-in names.
- All business printed times are in Indian Standard Time using `Asia/Kolkata`.

## Recommended architecture

Production must use the bridge architecture:

```text
Garmetix API container
  - Saves business transaction
  - Creates DotMatrixPrintQueueEntries row
  - Returns response to user without waiting for printer

Ubuntu host DotMatrix Bridge service
  - Reads pending rows from PostgreSQL
  - Sends raw text to CUPS/lp
  - Updates row as Printed or Failed

CUPS
  - Owns the Epson LX-310 printer queue
```

This keeps billing/API safe. If the Epson printer is offline, jammed, out of paper, or CUPS fails, the main app continues working and queue rows stay Failed/Pending for retry.

## Default production settings

`.env.production` should use:

```env
DOTMATRIX_PRINTING_ENABLED=false
DOTMATRIX_PRINTING_RUN_WORKER=false
DOTMATRIX_PRINTER_NAME=EPSON_LX310
DOTMATRIX_OUTPUT_MODE=BridgeService
DOTMATRIX_SPOOL_DIRECTORY=/app/data/dotmatrix-spool
DOTMATRIX_TIMEZONE_ID=Asia/Kolkata
DOTMATRIX_LINE_WIDTH=136
DOTMATRIX_POLL_SECONDS=5
DOTMATRIX_RETRY_LIMIT=10
```

Meaning:

- `DOTMATRIX_PRINTING_RUN_WORKER=false`: API container does not print and does not run printer worker.
- `DOTMATRIX_PRINTER_NAME=EPSON_LX310`: default CUPS printer queue name.
- `DOTMATRIX_OUTPUT_MODE=BridgeService`: queue is meant for the Ubuntu host bridge.
- `DOTMATRIX_PRINTING_ENABLED=false`: safe global default. Enable printing store-wise from the app after printer setup.

## Ubuntu deployment

After deploying Garmetix to Ubuntu, install CUPS and the DotMatrix Bridge:

```bash
cd /opt/garmetix/current
sudo GARMETIX_DOTMATRIX_PRINTER=EPSON_LX310 ./deploy/install-dotmatrix-bridge-ubuntu.sh
```

The installer:

- Installs `cups` and `cups-client`.
- Enables CUPS.
- Creates `/etc/garmetix-dotmatrix.env`.
- Installs `garmetix-dotmatrix-bridge.service`.
- Stops the older spool-file timer if present.
- Starts the bridge service.

## Add Epson LX-310 printer in Ubuntu

First check detected printer devices:

```bash
lpinfo -v
lpstat -p -d
```

If the printer is already added in Ubuntu with queue name `EPSON_LX310`, the bridge will use it.

If it is not added and you know the device URI, create a raw CUPS queue:

```bash
sudo GARMETIX_DOTMATRIX_PRINTER=EPSON_LX310 \
  GARMETIX_DOTMATRIX_DEVICE_URI='usb://EPSON/LX-310' \
  ./deploy/install-dotmatrix-bridge-ubuntu.sh
```

The exact device URI must come from `lpinfo -v`. For dot-matrix audit printing, raw text is preferred because alignment is controlled by fixed-width characters, not browser/PDF layout.

## Test printer from Ubuntu

```bash
printf 'GARMETIX EPSON LX-310 TEST\n' | lp -d EPSON_LX310 -o raw
```

If this does not print, fix Ubuntu/CUPS first before testing from Garmetix.

## Enable printing in Garmetix UI

Open:

```text
Accounting → Dot Matrix Print
```

For each store:

```text
Enable printing: ON
Printer name: EPSON_LX310
Output mode: Ubuntu host bridge service (recommended)
Line width: 136 column / full width
Timezone: Asia/Kolkata
Print transactions immediately: ON
Print day opening/closing: ON
Print edits/deletes: ON
Include attendance in day summary: ON
Include bank/UPI summary: ON
```

Then press **Save Settings** and **Queue Test Print**.

## Service commands

Check service:

```bash
sudo systemctl status garmetix-dotmatrix-bridge.service --no-pager
```

Watch logs:

```bash
sudo journalctl -u garmetix-dotmatrix-bridge.service -f
```

Restart service:

```bash
sudo systemctl restart garmetix-dotmatrix-bridge.service
```

Disable bridge temporarily:

```bash
sudo systemctl stop garmetix-dotmatrix-bridge.service
```

Re-enable:

```bash
sudo systemctl start garmetix-dotmatrix-bridge.service
```

## Queue behavior

Statuses:

```text
Pending  → waiting for bridge
Printing → bridge claimed row and is sending to CUPS
Printed  → lp accepted the print job successfully
Failed   → printer/lp failed; can retry
Skipped  → manually skipped or disabled
```

The bridge only marks a row `Printed` after the `lp` command succeeds. It does not mark a row printed only because a file was created.

If bridge restarts while a row is `Printing`, rows older than 5 minutes are moved back to `Failed` with retry count increased.

## Edit/delete audit lines

Physical paper cannot be changed. So edited/deleted records are printed as new audit lines:

```text
EDITED RECORD BELOW - dd-MM-yyyy hh:mm AM/PM IST - User: ...
<updated transaction line>
Changed: Amount: old -> new | Discount: old -> new
```

```text
DELETED / CANCELLED RECORD BELOW - dd-MM-yyyy hh:mm AM/PM IST - User: ...
<deleted/cancelled transaction line>
```

## Paper-saving summary layout

Use 136-column mode where possible. The summary is arranged in full-width blocks:

```text
SALES SUMMARY | TRANSACTION SUMMARY | CASH SUMMARY
BANK / UPI SUMMARY across full width
ATTENDANCE DETAILS across full width
```

This avoids wasting paper by printing long narrow lists.

## Troubleshooting

Printer not printing:

```bash
lpstat -p -d
printf 'TEST\n' | lp -d EPSON_LX310 -o raw
sudo journalctl -u cups -n 100 --no-pager
sudo journalctl -u garmetix-dotmatrix-bridge.service -n 100 --no-pager
```

Queue stuck Pending:

- Check the store has printing enabled in `Accounting → Dot Matrix Print`.
- Check `Output mode = Ubuntu host bridge service`.
- Check bridge service is running.
- Check `/etc/garmetix-dotmatrix.env` has correct `PROJECT_DIR` and `GARMETIX_DOTMATRIX_PRINTER`.

API should not print:

- Keep `DOTMATRIX_PRINTING_RUN_WORKER=false`.
- Use the bridge service for production.

Changing printer later:

1. Add/change CUPS printer queue.
2. Update `/etc/garmetix-dotmatrix.env`:

```env
GARMETIX_DOTMATRIX_PRINTER=NEW_PRINTER_QUEUE
```

3. Update Garmetix UI store setting printer name.
4. Restart bridge:

```bash
sudo systemctl restart garmetix-dotmatrix-bridge.service
```

## v4.12.48 live transaction polish

Stage 11D-133 strengthens the daily audit journal so paper output is closer to a real store register:

- Queue rows are created only when the selected store has Dot Matrix printing enabled.
- Live transaction lines use Store Code / Store Name context instead of a short GUID.
- Sale, purchase, voucher, cash voucher, customer receipt, vendor payment, commercial note, bank/cash and attendance punch rows have better particulars.
- Edited/deleted/cancelled rows print a full-width heading, user, optional reason/remark and changed-field summary.
- Deduplication keys are now stable per source/action/change hash, reducing accidental duplicate paper lines.
- Day summary Bank/UPI totals include invoice payments, customer advances, purchase/vendor payments and non-cash vouchers.
- Attendance summary includes employee code/name, check-in, check-out, total hours, status and missing check-out list.
- Admin queue has Retry failed, Reset stuck and Text preview actions.

## Operational flow

1. Complete Ubuntu printer/CUPS setup.
2. Install the host bridge.
3. In Garmetix, enable printing store-wise.
4. Queue a test print.
5. Open day. Day opening cash details print first.
6. After this, sale/payment/receipt/purchase/voucher rows print immediately after DB save.
7. Edited/deleted records print as correction audit lines; old printed lines are never removed.
8. Close day. Closing cash details and full-width day summary print.

## Queue controls from app

Open:

```text
Accounting → Dot Matrix Print
```

Useful buttons:

```text
Retry failed  - moves failed rows back to Pending for bridge retry.
Reset stuck   - moves old Printing rows back to Pending if bridge/service stopped mid-print.
Text          - previews the exact raw text that will be sent to Epson/CUPS.
Skip          - marks a selected row as skipped when paper print is intentionally not required.
```

## Recommended production rule

Keep the main API safe:

```env
DOTMATRIX_PRINTING_RUN_WORKER=false
DOTMATRIX_OUTPUT_MODE=BridgeService
```

Only the Ubuntu host-side service should touch printer hardware.



## v4.12.52 queue stats EF fix

Stage 11D-134 completes the remaining DotMatrix service controls before physical printer QA:

- Manual/test/day opening/day closing queue rows now use stable deduplication keys.
- Reprint action creates a fresh queue row with a `REPRINT COPY` paper header and original queue reference.
- Store printing can be paused/resumed from the Dot Matrix page without deleting settings.
- Queue status counters show Pending / Printing / Printed / Failed / Skipped counts.
- Ubuntu health script added:

```bash
sudo ./deploy/check-dotmatrix-bridge-status.sh
```

This script checks the bridge systemd service, CUPS printer queue, printer defaults and recent bridge logs.

## Completion status

Code-level DotMatrix work is now closed for:

```text
Day opening cash print
Live transaction print after DB save
Edited/deleted audit lines
Day closing cash print
Full-width day summary
Bank/UPI summary
Attendance details
Database queue
Ubuntu host bridge service
Epson LX-310 default printer setting
Pause/resume/retry/reprint/skip/reset/text preview controls
Deployment and operations documentation
```

The only remaining work is environment QA on your actual Ubuntu/Epson setup:

```text
1. dotnet build / docker compose build
2. CUPS queue EPSON_LX310 detection
3. Raw text test print
4. 80/136-column alignment test
5. Real sale/purchase/voucher/day-close paper verification
```
