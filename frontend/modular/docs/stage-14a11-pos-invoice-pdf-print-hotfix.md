# Stage 14A.11 POS Invoice PDF Print Hotfix

Version: `6.0.27`

## Problem

Live SRP invoice printing reached the API, but `/api/billing/sales/{id}/pdf` returned `500`.

Message Logs showed:

- Source: `API`
- Event: `UnhandledException`
- Exception: `System.IO.FileNotFoundException`
- Missing assembly: `QRCoder, Version=1.8.0.0`
- Failing path: invoice PDF QR rendering.

## Fix

- Invoice PDF generation now catches QR rendering failures and draws a scan-code fallback box instead of failing the whole PDF.
- POS document printing now uses a local legacy-style `apiUrl()` builder, matching the stable legacy `useServerDocumentPrint` URL behavior.

## Validation

- Rebuild POS.
- Rebuild API.
- Deploy SRP.
- Re-test an authenticated PDF request for the same invoice id that previously returned `500`.
- Confirm `/pos/history` and `/pos/sale` can print after the API returns a valid PDF.

## Follow-Up

- Confirm why the SRP runtime did not load `QRCoder.dll` even though local publish output contains it.
- Keep QR fallback in place permanently so printing is not blocked by optional QR rendering.
