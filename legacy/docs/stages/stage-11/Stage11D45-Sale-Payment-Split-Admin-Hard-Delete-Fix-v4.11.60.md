# Stage 11D-45 — Sale Payment Split + Admin Hard Delete Fix — v4.11.60

## Purpose

Fix sale invoice payment handling issues found during live deployment/testing:

1. Mixed payment sales such as `6000 cash + 1100 UPI` must affect petty cash/day closing as `6000 cash sale` and `1100 non-cash sale`, not the full invoice amount as non-cash.
2. Revised sale invoice copy flow must preserve the original bill amount and payment split so a revised invoice does not accidentally become due or lose the original split.
3. Admin/Owner needs a hard-delete cleanup action for already-cancelled sale invoices, with linked-row cleanup.

## Backend changes

### Payment split correction

Updated:

- `backend/Garmetix.Api/Accounting/PettyCashEndpoints.cs`
- `backend/Garmetix.Api/StoreDay/StoreDayEndpoints.cs`

Previous logic used invoice-level `PaymentMode`. When an invoice used `MixPayments`, the whole invoice paid amount was treated as non-cash.

New logic uses `InvoicePayments` rows:

- cash sale = sum of `InvoicePayments` where `PaymentMode == Cash`
- non-cash sale = sum of `InvoicePayments` where `PaymentMode != Cash`
- customer due remains `BillAmount - PaidAmount`

Example:

| Payment | Amount |
|---|---:|
| Cash | 6000 |
| UPI | 1100 |

Expected petty cash result:

| Field | Amount |
|---|---:|
| Cash sale impact | 6000 |
| Non-cash sale | 1100 |
| Customer due | 0 |

### Admin hard delete

Added endpoint:

```http
DELETE /api/billing/sales/{id}/hard-delete?confirmInvoiceNumber={invoiceNo}&reason={reason}
```

Security:

- Requires `GarmetixPolicies.Admin`.
- Allows hard delete only when the invoice is already `Cancelled`.
- Requires exact invoice number confirmation.
- Blocks hard delete if linked revised/return/exchange documents point to this invoice.

Rows cleaned:

- `InvoiceItems`
- `InvoicePayments`
- `CardPayments`
- related `StockMovements`
- related `JournalEntries` and `JournalLines`
- related bank transactions, statement lines and cheque logs
- related commercial notes
- related loyalty ledger rows
- optional audit entry cleanup with `deleteAudit=true`

The hard-delete action itself writes an `AuditLogEntry` with source `BillingAdminHardDelete`.

## Frontend changes

Updated:

- `frontend/garmetix-web/pages/billing/index.vue`
- `frontend/garmetix-web/pages/billing/new.vue`

### Billing register

Admin/Owner now sees a `Hard Delete` action for cancelled invoices only.

It asks the user to type the invoice number before calling the backend hard-delete endpoint.

### Revised sale copy

When opening `/billing/new?copyFrom={invoiceId}`:

- original item rows are copied
- original total bill amount is preserved by inferring bill-level discount when needed
- original payment split rows are copied from receipt payments
- non-cash rows default to the first available bank account so the user can adjust the reference/account before save

## Validation notes

Static validation confirms that the package contains:

- version `4.11.60`
- stage `Stage 11D-45 Sale Payment Split Admin Hard Delete Fix`
- `GET/POST/DELETE` route changes for billing hard delete
- mixed-payment calculation uses `InvoicePayments` rows in petty cash and store day closing
- revised invoice copy uses receipt payment rows

A full .NET/Nuxt build must be run on the target machine because this sandbox does not contain the .NET SDK.
