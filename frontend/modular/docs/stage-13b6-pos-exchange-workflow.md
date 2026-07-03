# Stage 13B.6 POS Exchange Workflow

Version: 5.13.11
Branch: Version5

## Scope

This stage adds a dedicated POS sales exchange workflow without changing the shared ASP.NET Core API or PostgreSQL database.

## Added

- POS route: `/exchange`
- POS menu entry: `Exchange`
- Original invoice search using invoice number, QR/document text, customer name or mobile.
- Original invoice item return quantity selection.
- Replacement item scan/search cart using the existing product lookup API.
- Additional payment panel for exchange difference collection.
- Bank account validation for non-cash additional payment.
- Exchange request construction through the shared return/exchange contract helper.
- Print queue recovery for the created exchange invoice.

## API Contract

The page posts to the existing endpoint:

- `POST /api/billing/sales/{invoiceId}/exchange`

Payload is built through `createSalesExchangeRequest(...)` and matches:

- `SalesExchangeRequest`
- `SalesReturnItemRequest`
- `ExchangeSaleItemRequest`

The existing POS contract check continues to compare the frontend key arrays with backend DTO record parameters.

## Operator Flow

1. Open POS app.
2. Sign in.
3. Open `Exchange`.
4. Scan or search original sale invoice.
5. Enter return quantity for original items.
6. Scan replacement products.
7. Collect extra payment when replacement value is higher than return credit.
8. Save and print exchange invoice.
9. Use Print Queue if browser print is blocked.

## Validation

- `npm.cmd run modular:pos:contract`
- `npm.cmd --prefix modular run build:pos`
- `npm.cmd run modular:check`
- `npm.cmd run modular:deploy:preflight`
- `npm.cmd run modular:validate -- --skip-builds`

## Remaining Follow-Up

- Add an operator acceptance note for exchange testing on 14 inch laptop screens.
- Consider store-scoped replacement lookup if the receipt API later exposes original `storeId`.
- Add live smoke coverage once POS test credentials and seed exchange data are available.
