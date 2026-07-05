# Stage 14A.13 POS Sales History Register Parity

Version: `6.0.29`

This stage tightens the modular POS Sales History page against the legacy Billing / Invoice Register page.

## Added

- Switched POS `/history` from `billing/sales/recent?take=100` to the paged `billing/sales` register API.
- Added register controls for search, invoice status, date preset, custom date range and page size.
- Added server summary cards for invoice count, sales amount, paid/due balance and cancelled count.
- Added first, previous, next, last pagination controls.
- Added guarded invoice cancellation through `POST billing/sales/{id}/cancel`.
- Added guarded hard delete for already-cancelled invoices through `DELETE billing/sales/{id}/hard-delete`.
- Kept the modular hidden-iframe PDF print flow and Digital Bill CRM link/counter display.

## Legacy Reference

- `frontend/legacy/garmetix-web/pages/billing/index.vue`
- Backend register endpoints in `backend/Garmetix.Api/Billing/BillingEndpoints.cs`

## Remaining POS Register Follow-Up

- Add cashier/store filters if the live operator flow needs them in POS, not only Back Office.
- Promote Digital Bill generate, WhatsApp send and activity timeline actions into the future CRM module slice.
- Replace browser confirm/prompt with a Nuxt UI modal if cancellation/hard-delete UX needs audit-grade notes.
