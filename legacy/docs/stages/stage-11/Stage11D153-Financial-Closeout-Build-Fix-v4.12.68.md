# Stage 11D-153 Financial Closeout Build Fix - v4.12.68

## Scope

This stage fixes API publish errors reported during Docker build after v4.12.67.

## Build errors fixed

- `FinancialYearCloseoutMetricDto` constructor calls missing `Description`.
- Invalid `Invoice.StoreGroupId` access in Financial Year Closeout sale invoice scope.
- Invalid `PurchaseInvoice.Paid` access in Production Go-Live Master Acceptance.

## Implementation

- `FinancialYearCloseoutMetricDto.Description` now defaults to an empty string, preserving old and new metric call sites.
- Sale invoice store-group filtering uses resolved store IDs because sale invoices contain `StoreId` but not `StoreGroupId`.
- Purchase paid amount is derived from grouped `PurchasePayment` rows.

## Version

- Version: `4.12.68`
- Stage: `Stage 11D-153 Financial Closeout Build Fix`
- Build code: `GARMETIX-11D153-20260703-4268`
