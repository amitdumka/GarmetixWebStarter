# Dot Matrix Daily Audit Journal

This module creates a compact, fixed-width dot-matrix audit print register for Indian store operations.

## Printed sections

### Day Opening

- Store name / store code
- Business date
- Opened at time in IST
- Opened by user
- Cash denomination details
- Opening cash total
- Transaction journal start marker

### Live Transactions

Each transaction is queued after it is saved to the database. Supported sources:

- Sale invoice
- Purchase inward
- Voucher
- Cash voucher
- Customer receipt
- Vendor payment
- Customer advance receipt
- Commercial note
- Bank/cash transaction
- Manual cash detail
- Attendance punch

### Edited / Deleted Records

Edits and delete/cancel actions are append-only print events.

- Edited records print `EDITED RECORD BELOW`.
- Deleted/cancelled records print `DELETED / CANCELLED RECORD BELOW`.
- Changed fields print old value -> new value where available.

### Day Closing

- Store name / store code
- Closed at time in IST
- Closed by user
- Closing cash denomination details
- Full-width day summary
- Bank / UPI summary
- Attendance details

## Day Summary contents

The Day Summary uses the full available line width to save paper. In 136-column mode, it prints three side-by-side sections:

- Sales Summary
- Transaction Summary
- Cash Summary

Required fields:

- Sales Count
- Sales Amount
- Cash Sales
- UPI Sales
- Card/Bank Sales
- Customer Receipts
- Vendor Payments
- Expenses
- Cash Vouchers
- Purchase Inward
- Opening Cash
- Cash In
- Cash Out
- Closing Cash
- Bank / UPI Summary
- Present Employees with check-in/check-out
- Absent Employees
- Late Check-in Employees

## Ubuntu host printer setup

Use CUPS on Ubuntu and keep Docker printing decoupled from the physical USB/parallel printer.

Recommended setup:

```bash
sudo ./deploy/install-dotmatrix-printer-ubuntu.sh
```

Then configure `/etc/garmetix-dotmatrix.env`:

```bash
GARMETIX_DOTMATRIX_PRINTER="EPSON_LQ_310"
GARMETIX_DOTMATRIX_SPOOL_DIR="/opt/garmetix/current/data/dotmatrix-spool"
GARMETIX_DOTMATRIX_LP_OPTIONS="-o raw"
```

Enable and run:

```bash
sudo systemctl enable --now garmetix-dotmatrix-spooler.timer
sudo systemctl status garmetix-dotmatrix-spooler.timer
```
