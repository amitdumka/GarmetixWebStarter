# v4.11.92 Purchase Import Posting QA + Product Polish

Stage 11D-77 focuses on the review-to-post step for scanned supplier invoice import.

## Added

- Stronger backend validation before posting:
  - gross total
  - distributed line discount
  - taxable total
  - tax total
  - freight and round-off
  - scanned grand total vs calculated grand total
- Header discount no longer blocks posting when it already matches distributed line discounts.
- New product creation guard:
  - cost price required
  - MRP required for new product draft
  - category and subcategory required for new product draft
  - duplicate barcode still blocks posting
- Review page reconciliation panel:
  - gross
  - discount
  - taxable
  - tax
  - line total
  - freight + round off
  - calculated grand
  - scanned grand difference
- Size/color split templates:
  - Shirt S/M/L/XL/XXL
  - Jeans 30/32/34/36/38/40
  - Kurta 38/40/42/44/46
  - Free size
  - Custom
- Size split now accepts `size=qty` entries, for example:
  - `38=2`
  - `40=2`
  - `42=1`
- Bulk similar product scan for new import lines.
- Failed/rejected scanned invoice history cleanup endpoint and UI button.
- Posted import proofs remain protected and cannot be bulk deleted.

## Test checklist

1. Upload S.K APPARELS invoice.
2. Verify discount, taxable, tax and grand total match.
3. Split a quantity line using `38=2`, `40=2`, `42=1`.
4. Click **Find similar all**.
5. Generate missing barcodes.
6. Save draft.
7. Confirm warnings are clear.
8. Post inward.
9. Confirm product/stock/accounting/vendor balance.
10. Create a failed/rejected draft and use **Clean failed**.
11. Confirm posted proof is not deleted.
