# Stage 14D.5 CRM Customer Register Filters

Version: `6.0.35`

## What Changed

- Added GST status, credit balance, loyalty balance and page-size filters to the modular CRM customer register.
- Added customer-list pagination so large customer masters do not render every row at once.
- Hardened customer row actions with shared `readId()` lookup instead of relying only on `customer.id`.
- Added a customer edit fallback that loads from the customer list if the single-record response is not usable.
- Moved customer loyalty preview from an inline bottom section to a right-side slideover.

## Validation

- `npm.cmd --prefix frontend\modular --workspace @garmetix/crm-web run build`

## Next CRM Work

- Feedback and review settings parity.
- WhatsApp settings/logs and guarded retry/test-send actions.
- Campaign create, preview, queue, send and ROI review.
- Invoice ad banners and public digital bill page.
