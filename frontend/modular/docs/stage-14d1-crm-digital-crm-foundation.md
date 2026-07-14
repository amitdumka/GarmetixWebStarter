# Stage 14D.1 CRM And Digital CRM Foundation

Version: `6.0.30`

This stage creates the dedicated modular CRM frontend under `frontend/modular/apps/crm`.

## Goal

Merge the customer CRM and Digital Bill CRM workflows into one modular app:

- Customer master and customer detail
- Customer dues reconciliation
- Loyalty program and customer loyalty ledger
- Digital bill register and engagement counters
- Feedback, review settings, WhatsApp settings/logs
- Audiences, campaigns, ad banners and production acceptance

## Added In This Stage

- New Nuxt app workspace package: `@garmetix/crm-web`.
- CRM app shell with shared `ModularAppShell`.
- CRM login and auth guard.
- Read-only CRM table component for first API parity checks.
- Initial CRM pages for customers, loyalty, dues, digital bills, analytics, audiences, campaigns, feedback, review settings, WhatsApp settings/logs, ad banners and acceptance.
- Route ownership moved from Main/POS planning into CRM for CRM-owned routes.
- CRM added to app registry, smoke routes, env examples and SRP deployment path `/crm/`.

## Legacy References

- `frontend/legacy/garmetix-web/pages/customers`
- `frontend/legacy/garmetix-web/pages/loyalty`
- `frontend/legacy/garmetix-web/pages/marketing`
- `frontend/legacy/garmetix-web/pages/i/[token].vue`

## Next Stages

1. Customer register/detail/new write parity.
2. Loyalty program save and manual adjustment parity.
3. Digital Bill register actions: generate, send WhatsApp, disable, regenerate token and activity timeline.
4. Campaign audience, campaign creation and WhatsApp campaign guarded actions.
5. Public digital bill customer page in modular CRM.
