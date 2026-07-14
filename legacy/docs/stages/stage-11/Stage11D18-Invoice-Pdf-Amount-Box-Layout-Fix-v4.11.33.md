# Stage 11D-18 Invoice PDF Amount Box Layout Fix - v4.11.33

## Problem

In multi-page A4/A5 purchase invoices, the final-page amount block was taller than the page summary panel. It overlapped the signature strip and footer.

## Fix

- Increased the reserved final-page summary area.
- Reduced continuation page row count from 28 to 26 on A4.
- Drew compact amount rows so totals fit inside the summary box.
- Applied the same fix to both purchase and sale invoice PDFs.

## Files

- `backend/Garmetix.Api/Purchase/PurchasePdfDocument.cs`
- `backend/Garmetix.Api/Billing/InvoicePdfDocument.cs`
