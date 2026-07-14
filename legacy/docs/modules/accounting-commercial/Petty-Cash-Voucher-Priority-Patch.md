# Petty Cash and Voucher Priority Patch

Implemented: 2026-06-12

## Petty Cash

- `GET /api/petty-cash-sheets/prepare` derives a proposed daily sheet for one store and date.
- Opening balance comes only from the immediately previous calendar day's saved cash in hand.
- Sales start from the day's sales-invoice total. Customer due and non-cash portions are deducted separately.
- Cash recoveries for older invoices are included as due receipts.
- Cash receipt, payment, and expense vouchers are grouped by voucher type.
- Bank cash withdrawals add to cash; bank cash deposits reduce cash.
- Users may change proposed values before saving.
- The API recalculates expected values after save. Differences above INR 0.01 create a structured warning in Application Message Logs.
- Owner/Admin email alerts are attempted only when SMTP is enabled. Delivery failure is logged and never rolls back the saved sheet.
- One sheet per store/date is enforced.
- The page's Cash In Hand metric uses the latest sheet for the current working store.
- Saving opens an A5 landscape print view with Income and Payment columns and verification signatures.

## Voucher

- New voucher numbers are server-generated and cannot be edited in the UI.
- Format: `StoreCode/yyyyMM/0001`, using one monthly number series per store.
- The existing persistent daily document sequence is used instead of browser timestamps.
- Print defaults to A5 single-copy mode. PDF headings now identify an accounting voucher and show a document reference rather than a non-existent scan code.

## Page Loading

- GET requests are cached for 20 seconds.
- Stable master lists such as companies, stores, ledgers, parties, employees, and bank accounts are cached for five minutes.
- Concurrent requests for the same authenticated resource share one in-flight request.
- Any create, update, or delete clears the response cache.

## Validation

- `dotnet build Garmetix.Api/Garmetix.Api.csproj`: passed with three pre-existing warnings.
- `npm.cmd run build`: passed. Existing external font metadata certificate and sourcemap warnings remain.
