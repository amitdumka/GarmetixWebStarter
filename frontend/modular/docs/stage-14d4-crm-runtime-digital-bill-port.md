# Stage 14D.4 CRM Runtime Stabilization And Digital Bill Port

Version: `6.0.34`

This stage repairs CRM runtime failures reported after Stage 14D.3 and ports the first working Digital CRM surfaces from legacy into the modular CRM app.

## Fixed

- CRM table bodies no longer combine `v-for` with `v-else` on the same row.
- Customer edit form now unwraps raw or wrapped API responses before filling fields.
- CRM API helpers now expose `readRecord` for safer object response handling.

## Added

- `/marketing/digital-bills`
  - digital bill register
  - generate link by sale invoice ID/number
  - copy/open public link
  - send WhatsApp
  - disable and regenerate token
  - activity totals, timeline and feedback panel
- `/marketing/digital-bill-analytics`
  - store/date filters
  - engagement metric cards
  - daily trend table
- `/marketing/campaign-audiences`
  - store/date/segment/search filters
  - segment summary cards
  - copy mobile list
  - CSV export
  - public bill and Digital Bills handoff

## Remaining

- Full campaign create/preview/queue/send/mark-sent/cancel and ROI detail page.
- Review settings save form.
- WhatsApp settings save/test-send form.
- WhatsApp logs retry/copy workflow.
- Customer feedback register polish.
- Invoice ad banner CRUD.
- Public `/i/:token` page parity.
