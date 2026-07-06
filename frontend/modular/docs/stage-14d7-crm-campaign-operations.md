# Stage 14D.7 CRM Campaign Operations

Version: `6.0.39`

## Goal

Promote modular CRM campaigns from a read-only register to a usable Digital CRM campaign workbench.

## Implemented

- Replaced the generic campaign table with a dedicated campaign operations page at `/marketing/campaigns`.
- Added register filters for status and search.
- Added campaign metrics for recipients, prepared/manual, sent and failed counts.
- Added create campaign modal with:
  - segment/date/store/search filters,
  - manual or WhatsApp channel selection,
  - template name,
  - message title/body,
  - offer URL,
  - recipient limit,
  - queue-now option.
- Added campaign audience preview before save.
- Added campaign detail panel with recipient list and message copy handoff.
- Added lifecycle actions:
  - queue,
  - send WhatsApp,
  - mark sent,
  - cancel.
- Added send-result summary after WhatsApp processing.
- Added ROI review for engagement, repeat purchase rate and repeat sales.

## Backend Contracts Used

- `GET /api/digital-bill-campaigns`
- `GET /api/digital-bill-campaigns/{id}`
- `GET /api/digital-bill-campaigns/{id}/roi`
- `POST /api/digital-bill-campaigns/preview`
- `POST /api/digital-bill-campaigns`
- `POST /api/digital-bill-campaigns/{id}/queue`
- `POST /api/digital-bill-campaigns/{id}/send-whatsapp`
- `POST /api/digital-bill-campaigns/{id}/mark-sent`
- `POST /api/digital-bill-campaigns/{id}/cancel`

## Validation

- CRM static build passed with `npm --prefix frontend/modular --workspace @garmetix/crm-web run build`.
- Known Nuxt font-provider certificate warnings remain non-blocking.

## Next

- Stage 14D.8: invoice ad banners create/edit/delete and public `/i/:token` digital bill customer page.
