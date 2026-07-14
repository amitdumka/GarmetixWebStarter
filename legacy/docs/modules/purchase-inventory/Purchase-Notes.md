# Purchase Inward Notes

This chunk adds the first purchase workflow for Garmetix.

## API

- `POST /api/purchase/inward`
- Requires the `Purchase` policy.
- Uses one EF Core transaction.

The endpoint:

- creates or reuses a vendor by company/name/mobile
- creates a purchase invoice and purchase invoice items
- creates missing products when a new product name and barcode are supplied
- increases `Stock.PurchaseQty` for the selected store
- updates the vendor bill count, bill amount, and paid amount

## Frontend

The Nuxt dashboard has a `Purchase Inward` panel below the POS panel. You can add existing products or enter a new product name and barcode, then save the inward entry.

## Next Improvements

- add purchase invoice listing and print view
- add purchase return/cancellation with stock reversal
- add vendor payment vouchers from paid purchase amount
- add tax selector and category selector in the inward form
