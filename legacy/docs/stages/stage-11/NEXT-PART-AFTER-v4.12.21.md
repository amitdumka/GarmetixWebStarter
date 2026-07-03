# Next Part After v4.12.21

## Stage 11D-107 — Day Book Source Page Deep-Link Fixes

Implement source page deep-link handling so links from Day Book open the exact transaction directly:

- `/billing?invoiceId=...` opens sale invoice detail/revise drawer
- `/billing?paymentId=...&invoiceId=...` opens customer receipt/invoice context
- `/purchase?purchaseInvoiceId=...` opens purchase inward detail/edit drawer
- `/purchase?vendorPaymentId=...` opens vendor payment detail/edit drawer
- `/vouchers?voucherId=...` opens voucher detail/edit drawer
- `/cash-vouchers?cashVoucherId=...` opens cash voucher detail/edit drawer
- `/accounting?journalEntryId=...` opens journal detail
