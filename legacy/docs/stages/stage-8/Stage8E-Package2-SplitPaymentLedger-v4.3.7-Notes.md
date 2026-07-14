# Stage 8E Package 2 - Split Payment Ledger Hardening (v4.3.7)

Build code: `GARMETIX-8E-20260616-4370`

## Scope

This package continues Stage 8E accounting and payment hardening after the v4.3.6 cash voucher conversion audit baseline.

## Implemented

- Sales invoices now pass normalized payment rows into accounting instead of posting the invoice-level `MixPayments` value as one settlement amount.
- Each payment row creates its own debit to the correct settlement ledger and matching credit to the customer ledger.
- Cash, card, UPI, wallet, IMPS, RTGS, NEFT, cheque and demand draft rows keep their own bank-account/reference context.
- Customer advance, credit note, store credit/sales return credit and loyalty redemption rows are routed to distinct settlement/adjustment ledgers.
- Bank transaction references for split sales payments use `SI-{InvoiceNumber}-PAY-{RowNumber}` so reconciliation rows no longer overwrite each other.
- Cheque logs preserve the real payment reference/cheque number where supplied instead of only storing the invoice reference.
- Invoice payments now include `PaymentDetailsJson`, a structured snapshot for mode, account, reference, gateway, settlement, cheque, card, UPI, wallet and adjustment metadata.
- Duplicate use of the same advance receipt, credit note, store credit/customer balance or loyalty redemption row is rejected before balances are mutated.
- Nuxt font discovery is pinned to local/no-provider mode so offline builds do not depend on external Google/Bunny/Fontshare/Fontsource metadata.

## Files changed

- `backend/Garmetix.Api/Accounting/AccountingPostingService.cs`
- `backend/Garmetix.Api/Billing/BillingDtos.cs`
- `backend/Garmetix.Api/Billing/BillingEndpoints.cs`
- `backend/Garmetix.Api/ImportExport/ImportExportEndpoints.cs`
- `backend/Garmetix.Domain/Generated/Models/Inventory/Invoicing.cs`
- `backend/Garmetix.Api/Database/DatabaseSchemaRepairService.cs`
- `backend/Garmetix.Infrastructure/Data/Migrations/20260616133000_AddInvoicePaymentDetails.cs`
- `frontend/garmetix-web/nuxt.config.ts`
- version and roadmap files

## Remaining Stage 8E work

- Complete bank reconciliation and cheque clear/bounce lifecycle screens/audit.
- Add financial-year locking and stronger journal-balancing checks.
- Harden automatic Party and Bank Account ledger synchronization.
- Add customer/vendor due dashboards and payment comparison summaries.
