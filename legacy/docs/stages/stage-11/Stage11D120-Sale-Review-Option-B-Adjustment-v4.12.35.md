# Stage 11D-120 — Sale Review Option B Adjustment

Version: 4.12.35

## Scope

Adds the approved Option B adjustment flow to Sales → Sale Review.

## Implemented

- New backend endpoint `POST /api/sale-review/adjustments/apply`.
- Corrects selected/eligible sale invoice items that were charged 18% GST while unit basic value is within the ₹2,499 apparel threshold.
- Recalculates the affected item at 5% GST and chooses the nearest allowed inclusive line value without crossing the ₹2,499 basic threshold.
- Reduces invoice totals and invoice payment rows by the Extra Amount difference.
- Reposts the sale invoice journal after correction.
- Creates an Extra Amount receipt voucher dated as the original invoice date.
- Voucher particulars and remarks include invoice number and item barcode(s).
- Protects against duplicate posting for the same invoice using a Sale Review adjustment marker.
- Sale Review UI now has an Apply Option B action on invoice rows with GST issues.

## Test checklist

1. Open Sales → Sale Review with Only issues enabled.
2. Verify invoice with 18% GST under threshold appears.
3. Click Apply Option B.
4. Confirm invoice item tax changes to 5%.
5. Confirm invoice bill amount reduces by Extra Amount.
6. Confirm invoice payment total reduces by Extra Amount.
7. Confirm voucher receipt is created dated as invoice date.
8. Confirm voucher remarks contain invoice number and barcode(s).
9. Confirm Sale Review no longer shows the corrected row as an issue.
