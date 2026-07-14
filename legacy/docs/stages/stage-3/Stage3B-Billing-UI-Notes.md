# Garmetix Stage 3B — Billing UI Completion Notes

## Package

`Garmetix-Stage3B-BillingUI-v0.6.zip`

## Goal

Stage 3B moves the billing screen from the old single-payment/walk-in-customer POS flow toward a production POS flow with customer selection, salesman capture, split payments, and customer balance adjustments.

This stage builds on Stage 3A Product Master changes and the earlier sale/purchase/inventory hardening work.

## Context carried forward

The previous audit found that billing had basic invoice creation, barcode scan, product autocomplete, payment mode, paid amount, bill discount, print/download, sale return, exchange, cancellation, stock update, loyalty points, and accounting journal posting.

The same audit identified these Stage 3B gaps:

- Existing customer selection was missing.
- Salesman selection was missing.
- Multiple payment modes were missing.
- Card/UPI/payment reference details were weak.
- Customer advance, credit note, and loyalty redemption were not available from the sale screen.

## Backend changes

### Billing DTOs

Updated `backend/Garmetix.Api/Billing/BillingDtos.cs`:

- Added customer option DTO.
- Added salesman option DTO.
- Added customer profile DTO.
- Added adjustment option DTO for credit notes and customer advance receipts.
- Expanded receipt payment DTO with gateway, settlement, and adjustment source fields.

### Billing endpoints

Updated `backend/Garmetix.Api/Billing/BillingEndpoints.cs`:

- Added `GET /api/billing/options`.
- Added `GET /api/billing/customers/search`.
- Added `GET /api/billing/customers/{customerId}/profile`.
- Sale creation now accepts payment rows from the UI instead of only one `PaymentMode` + `PaidAmount`.
- Sale creation now validates salesman scope.
- Sale creation now stores `InvoicePayment` rows for every payment split.
- Sale creation now supports adjustment-source payments:
  - `CustomerCreditBalance`
  - `CustomerAdvanceReceipt`
  - `CreditNote`
  - `LoyaltyRedemption`

### Salesman model registration

Updated `backend/Garmetix.Infrastructure/Data/GarmetixDbContext.cs`:

- Added `DbSet<Salesman> Salesmen`.
- Added index for company/store/name lookup.

### Runtime schema repair

Updated `backend/Garmetix.Api/Database/DatabaseSchemaRepairService.cs`:

- Added idempotent `Salesmen` table creation.
- Added `IX_Salesmen_CompanyId_StoreId_Name`.

### Quick setup

Updated `backend/Garmetix.Api/Setup/SetupEndpoints.cs`:

- Quick setup now seeds a default active `Manager` salesman for the main store.

### Migration

Added:

- `backend/Garmetix.Infrastructure/Data/Migrations/20260608100000_AddStage3BBillingCustomerPaymentUi.cs`

The runtime repair is still included because this project already relies on idempotent schema repair for older Docker volumes and mixed development databases.

## Frontend changes

Updated `frontend/garmetix-web/pages/billing/index.vue`:

### Customer/salesman

- Added existing customer dropdown.
- Added salesman dropdown.
- Added customer profile loading.
- Customer profile displays credit balance, loyalty points, and bill count.

### Split payments

- Replaced single `PaymentMode + Paid` form with editable payment rows.
- Supports Cash, Card, UPI, Wallet, IMPS, RTGS, NEFT, Cheque, and Demand Draft.
- Non-cash rows can capture bank account/reference.
- Rows can also capture gateway reference and settlement status.

### Customer adjustments

Added adjustment section:

- Store credit balance amount.
- Credit note selector + amount.
- Customer advance receipt selector + amount.
- Loyalty points redemption.

The UI sends these as `payments[]` rows with `adjustmentSourceType` so the backend can reduce the corresponding customer/note/advance/loyalty balances.

### Receipt

The receipt modal now displays payment rows with reference and adjustment source information.

## Known limitations after Stage 3B

- Accounting posting still aggregates collection at invoice level. It does not yet post separate accounting lines per split payment row.
- Card details are still stored in `InvoicePayment` reference fields, not as full `CardPayment` rows.
- Credit note and customer credit balance can represent related business value, so the POS operator should avoid applying the same credit twice until Stage 4 reconciliation adds stricter allocation rules.
- Invoice number generation is still count-based and should be fixed in Stage 4 sequence hardening.
- Stock concurrency locking is still pending for Stage 4.

## Recommended next stage

Move to **Stage 3C — Purchase UI Completion**:

- Vendor picker.
- Supplier invoice date and due date UI.
- Purchase payment history/allocation view.
- HSN/unit/tax snapshot visibility in purchase item rows.
- Better freight/tax fields.
