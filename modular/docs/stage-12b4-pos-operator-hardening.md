# Stage 12B.4 - POS Operator Hardening

Version: 5.12.4

## Scope

This stage keeps the POS work inside `modular/apps/pos` and does not change the legacy app, backend API, or PostgreSQL schema.

## Added

- POS global route middleware that redirects unsigned or expired sessions to `/login`.
- Login redirect support so operators return to the protected page they first opened.
- POS sidebar filtering so logged-out operators only see Login.
- Sale counter scanner autofocus and keyboard shortcuts:
  - `F2` focuses the barcode/product field.
  - `F4` runs Save & Print.
  - `F8` adds another payment row.
  - `Esc` clears the current barcode/product search.
- Sale save validation for GSTIN, bill discount, customer credit, credit note, advance receipt, loyalty, and over-adjustment cases.

## How To Test

1. Run the POS app and open `/sale` without a token. It should redirect to `/login?redirect=/sale`.
2. Login and confirm it returns to the requested page.
3. Open `/sale` and press `F2`; the barcode/product field should focus.
4. Add a product and press `F8`; a payment row should be added.
5. Enter an invalid GSTIN and try Save & Print; the page should show a validation message.
6. Use customer adjustments above available balances; Save & Print should stop with a clear warning.

## Remaining Notes

- Keyboard shortcuts are browser-level while the sale page is mounted only.
- The route guard currently validates local token presence and expiry. Server-side permission verification remains an API responsibility.
- Sale return extraction is still planned as the next POS slice.
