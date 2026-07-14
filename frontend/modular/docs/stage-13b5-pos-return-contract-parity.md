# Stage 13B.5 - POS Return Contract Parity

Version: 5.13.10

## Scope

This stage keeps the API and database unchanged. It hardens the modular POS return frontend against backend DTO drift and makes the sales return payload construction explicit.

## Added

- POS return contract helper for `SalesReturnRequest` and `SalesReturnItemRequest`.
- Contract key arrays for future sales exchange payloads.
- Contract parity checks for sale, payment detail, return and exchange billing DTOs.
- Return submit button now remains disabled until a required non-cash refund bank account is selected.

## Updated Workflows

- Sales Returns now calls `createSalesReturnRequest(...)` before posting to `/api/billing/sales/{id}/returns`.
- Zero-refund returns send `refundPaymentMode` and `bankAccountId` as `null`.
- Return rows are filtered and normalized before posting.
- The existing POS contract check now verifies backend DTO shape for returns and exchange as well as sales.

## Validation

Run:

```powershell
npm.cmd run modular:pos:contract
npm.cmd --prefix modular run build:pos
npm.cmd run modular:validate -- --skip-builds
```

## Remaining Follow-Up

A dedicated exchange screen can now reuse the exchange contract key arrays when the replacement-item workflow is added to the POS app.
