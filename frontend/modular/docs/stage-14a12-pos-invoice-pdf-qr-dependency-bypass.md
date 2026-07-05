# Stage 14A.12 POS Invoice PDF QR Dependency Bypass

Version: `6.0.28`

## Problem

Stage 14A.11 added a QR fallback around invoice PDF rendering, but live SRP testing still returned `500`.

Message Logs showed the CLR failed while resolving `QRCoder` before the fallback catch could run.

## Fix

- Invoice PDF rendering no longer calls the QR rendering service directly.
- The invoice PDF prints a scan-code fallback box in the QR position.
- The text footer still includes the invoice scan code reference.

## Follow-Up

- Repair QR packaging separately.
- Restore real QR blocks only after live SRP PDF endpoint verification proves the QR dependency loads correctly.
