# GarmetixWebStarter v4.12.22

Stage 11D-107 Day Book Source Deep Links.

This package builds on v4.12.21 and improves Day Book source navigation. Day Book can already open transaction details in its own drawer; this stage makes the original source pages understand Day Book query links and auto-open the exact transaction detail/print drawer.

## Highlights

- Billing page reads `/billing?invoiceId=...` and opens that sale invoice receipt directly.
- Billing page also accepts customer receipt links when `invoiceId` is available.
- Purchase page reads `/purchase?purchaseInvoiceId=...` and opens the purchase inward receipt directly.
- Purchase page reads `/purchase?vendorPaymentId=...&purchaseInvoiceId=...` and opens the exact vendor payment detail popup.
- Vouchers page reads `/vouchers?voucherId=...` and opens the voucher print/detail modal.
- Cash Vouchers page reads `/cash-vouchers?cashVoucherId=...` and opens the cash voucher print/detail modal.
- Day Book date navigation from v4.12.21 remains unchanged.
- Source links remain safe: if a record is not in the current workspace, the page shows a warning rather than failing.

## Validate

```bash
python3 scripts/validation/stage11d107-day-book-source-deep-links-check.py
docker compose build --no-cache
docker compose up -d
```

## Next part

Stage 11D-108 — Day Book Live QA + Detail Drawer Polish.

That should fix any live issue found after opening sale, purchase, voucher, cash voucher, vendor payment and journal rows from Day Book.
