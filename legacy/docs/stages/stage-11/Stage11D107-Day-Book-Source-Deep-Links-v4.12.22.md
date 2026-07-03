# Stage 11D-107 — Day Book Source Deep Links

Version: v4.12.22
Base: v4.12.21 Stage 11D-106 Day Book Date Navigation

## Goal

Make Day Book work like a real transaction register: selecting a row can open Day Book detail, and the source page link should open the same source transaction directly instead of only navigating to a list page.

## Implemented

### Billing source links

`/billing?invoiceId=<id>` now opens the exact sale invoice receipt.

### Purchase source links

`/purchase?purchaseInvoiceId=<id>` now opens the exact purchase inward receipt.

`/purchase?vendorPaymentId=<id>&purchaseInvoiceId=<id>` now opens the exact vendor payment detail popup and also opens the linked purchase receipt when available.

### Voucher source links

`/vouchers?voucherId=<id>` now opens the voucher print/detail modal.

### Cash voucher source links

`/cash-vouchers?cashVoucherId=<id>` now opens the cash voucher print/detail modal.

## Files changed

- `frontend/garmetix-web/pages/billing/index.vue`
- `frontend/garmetix-web/pages/purchase/index.vue`
- `frontend/garmetix-web/pages/vouchers/index.vue`
- `frontend/garmetix-web/pages/cash-vouchers/index.vue`
- `backend/Garmetix.Api/AppInfo/AppInfoEndpoints.cs`
- `frontend/garmetix-web/utils/appVersion.ts`
- `frontend/garmetix-web/package.json`
- `backend/Garmetix.Api/Garmetix.Api.csproj`
- `README.md`

## Notes

Journal source links still go to Accounting because the current Accounting page does not yet have a dedicated journal entry detail drawer. Journal rows still open correctly inside the Day Book detail drawer.

## Next part

Stage 11D-108 — Day Book Live QA + Detail Drawer Polish.
