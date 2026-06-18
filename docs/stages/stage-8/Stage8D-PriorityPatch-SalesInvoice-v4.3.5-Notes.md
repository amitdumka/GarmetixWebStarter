# Stage 8D Priority Patch - Sales Invoice Stability

Version: 4.3.5

Build: `GARMETIX-8D-20260616-4350`

## Delivered

- Added the dedicated `/billing/new` invoice workspace.
- Added mobile customer search, automatic customer creation, GSTIN validation, customer balance visibility and Manager default selection.
- Added resilient local draft recovery, amount/percent item discounts, responsive cart detail and contextual payment fields.
- Rounded the final payable amount, exposed round-off and automatically printed the first saved copy.
- Wrapped Billing manual transactions in EF Core's configured retry execution strategy.
- Applied GSTIN state-code interstate detection and corrected advance-receipt adjustment precedence.

## Validation

- Backend build passed without warnings.
- 64 non-PostgreSQL backend tests passed.
- Nuxt production build passed.
- Docker API/web rebuild and health startup passed.
- A live sale transaction probe reached document/stock locking and returned the expected stock validation response instead of the former execution-strategy exception.
- Browser checks passed at desktop and 390x844 mobile sizes with no horizontal document overflow.
