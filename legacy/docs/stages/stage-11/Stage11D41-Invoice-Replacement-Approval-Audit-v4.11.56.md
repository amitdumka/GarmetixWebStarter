# Stage 11D-41 — Invoice Replacement Approval Audit — v4.11.56

## Goal

Move invoice replacement from a frontend two-step auto-cancel flow into a safer approval workflow:

1. Create revised invoice/inward.
2. Link revised document to the original document.
3. Show replacement in an owner/admin approval queue.
4. Cancel/reverse the old document only after approval.
5. Audit every replacement request and approval event.

## Implemented

### Backend

New module:

- `backend/Garmetix.Api/InvoiceReplacement/InvoiceReplacementDtos.cs`
- `backend/Garmetix.Api/InvoiceReplacement/InvoiceReplacementAudit.cs`
- `backend/Garmetix.Api/InvoiceReplacement/InvoiceReplacementEndpoints.cs`

New routes:

- `GET /api/invoice-replacements/pending`
- `GET /api/invoice-replacements/audit`
- `POST /api/invoice-replacements/sales/{revisedInvoiceId}/approve`
- `POST /api/invoice-replacements/purchase/{revisedInvoiceId}/approve`

Updated routes/contracts:

- `POST /api/billing/sales` accepts:
  - `originalInvoiceId`
  - `replacementApprovalRequested`
  - `replacementReason`
- `POST /api/purchase/inward` accepts:
  - `originalInvoiceId`
  - `replacementApprovalRequested`
  - `replacementReason`

The approval endpoints reuse the existing cancellation/reversal engines:

- sales invoice cancellation reverses stock, payment, customer balance and accounting.
- purchase invoice cancellation posts purchase return, stock out, ITC reversal, vendor balance reversal, debit note and accounting journal.

### Frontend

New page:

- `frontend/garmetix-web/pages/invoice-replacements/index.vue`

Updated pages:

- `frontend/garmetix-web/pages/billing/new.vue`
- `frontend/garmetix-web/pages/purchase/new.vue`
- `frontend/garmetix-web/components/AppShell.vue`
- `frontend/garmetix-web/components/AppShellLegacy.vue`
- `frontend/garmetix-web/composables/useAccessControl.ts`

### UI behavior

Old behavior:

- frontend saved revised invoice
- frontend immediately called cancel old invoice when switch was enabled

New behavior:

- frontend saves revised invoice/inward
- revised document stores `OriginalInvoiceId`
- frontend submits replacement approval request/audit event
- owner/admin approves from **Invoice Replacement Approvals**
- backend cancels/reverses old invoice only after approval

## Validation checklist

1. Create a normal sale invoice.
2. Click Revise from Sales register.
3. Edit revised invoice and enable replacement approval.
4. Save revised invoice.
5. Confirm old sale is still active before approval.
6. Open Invoice Replacement Approvals.
7. Approve the sale replacement.
8. Confirm old sale is cancelled and stock/payment/accounting reversal exists.
9. Repeat same flow for purchase inward.
10. Confirm audit page shows Requested, Approved and Completed events.

## No migration required

This stage uses existing fields/tables:

- `BaseInvoice.OriginalInvoiceId`
- `AuditLogEntries`

No new database table was added.

## Known limits

- This stage adds approval and audit without a separate replacement-status table.
- Pending queue is derived from revised documents where `OriginalInvoiceId` exists and the original is not cancelled.
- Rejection/hold workflow can be added later with a dedicated replacement ledger table if needed.
