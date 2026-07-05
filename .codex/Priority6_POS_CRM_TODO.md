# Priority 6 POS And CRM Working TODO

## Active Base

- Workspace: `C:\AIArea\Codex\GarmetixWebStarter`
- Branch: `version6`
- Version lane: `6.0.x`
- Antigravity reference workspace: `D:\AIArea\GarmetixWebStarter`
- Rule: read Antigravity files only when explicitly requested or when the user names a file/module for comparison.

## Backups

Current POS page backups before the Priority 6 port:

- `.codex/backups/stage-14a9-pos-antigravity-layout/app.vue.bak`
- `.codex/backups/stage-14a9-pos-antigravity-layout/sale.vue.bak`

Use these backups if the fullscreen counter layout must be reverted without touching unrelated Version6 work.

## Files Compared

- Current Version6 POS sale: `frontend/modular/apps/pos/pages/sale.vue`
- Current Version6 POS shell app: `frontend/modular/apps/pos/app.vue`
- Antigravity POS sale: `D:\AIArea\GarmetixWebStarter\frontend\modular\apps\pos\pages\sale.vue`
- Antigravity POS app: `D:\AIArea\GarmetixWebStarter\frontend\modular\apps\pos\app.vue`
- Antigravity POS history: `D:\AIArea\GarmetixWebStarter\frontend\modular\apps\pos\pages\history.vue`
- Legacy CRM docs:
  - `legacy/docs/roadmaps/Garmetix-Digital-Bill-CRM-Roadmap.md`
  - `legacy/docs/roadmaps/Garmetix-Digital-Bill-CRM-TODO.md`

## Priority 6 Completed In This Slice

- Port fullscreen POS counter layout into Version6 POS sale page.
- Enable Nuxt layouts in POS app so `/sale` can use fullscreen and other pages keep app shell.
- Add POS `/history` sale listing route.
- Add POS sale history to route registry, sidebar and smoke routes.
- Keep barcode/search behavior that normalizes selected datalist text back to barcode before lookup.
- Show Digital Bill CRM signals in sale history:
  - public link availability
  - WhatsApp status
  - open count
  - PDF download count
  - review click count
- Port legacy hidden-iframe PDF printing into modular POS invoice/history/print queue actions.
- Move POS operational notifications to right-bottom toast placement.
- Change new sale invoice numbers to `StoreCode-YYYYMM-INV-NumberSeries`.

## POS Follow-Up

- Run live operator review for `/sale` on 14-inch laptop width and check scanner, cart, payments footer and right totals panel.
- Manually verify browser print dialog and Epson/DotMatrix handoff from Save & Print, sale history print, and print queue recovery.
- Port any remaining server/dot-matrix print-service behavior after comparing legacy LX-310 deployment scripts.
- Confirm `/history` recent sale API fields on live data:
  - `digitalBillPublicPath`
  - `digitalBillPublicToken`
  - `digitalBillWhatsAppStatus`
  - CRM counters
- Add optional date range and cashier/store filters to `/history` after live field confirmation.
- Add QR/barcode scan search into `/history` once standard invoice QR payload format is finalized.
- Add sale detail Digital Bill activity timeline when backend `/api/digital-bills/{id}/activity` is ported into modular CRM.

## CRM / Digital CRM Module Roadmap

Preferred module name: `crm`.

Routes to port from legacy:

- `/customers`
- `/customers/new`
- `/customers/:id`
- `/customers/dues-reconciliation`
- `/loyalty`
- `/marketing/digital-bills`
- `/marketing/digital-bill-analytics`
- `/marketing/customer-feedback`
- `/marketing/review-settings`
- `/marketing/whatsapp-settings`
- `/marketing/whatsapp-logs`
- `/marketing/ad-banners`
- `/marketing/campaign-audiences`
- `/marketing/campaigns`
- public `/i/:token` digital bill customer page

Backend/API surfaces already present in Version6 backend to reuse:

- `backend/Garmetix.Api/Marketing/DigitalBillCrmEndpoints.cs`
- `backend/Garmetix.Api/Marketing/DigitalBillCrmService.cs`
- `backend/Garmetix.Api/Marketing/DigitalBillCrmDtos.cs`
- `backend/Garmetix.Api/Marketing/DigitalBillWhatsAppService.cs`
- `backend/Garmetix.Api/Customers/CustomerDuesReconciliationEndpoints.cs`

CRM implementation order:

1. Customer register and customer detail parity.
2. Loyalty setup, customer loyalty ledger and manual point adjustment.
3. Customer dues reconciliation.
4. Digital bills register with generate/copy/open/send WhatsApp/activity actions.
5. Public digital bill page `/i/:token`.
6. WhatsApp settings and logs.
7. Review settings and customer feedback.
8. Ad banners and campaign audiences.
9. Digital bill analytics cards/trends.
10. Campaign creation/scheduling, send logs and ROI reporting.

## Deployment Notes

- Deploy to `.127` only after build/smoke checks pass.
- Avoid database backup/deploy repetition unless schema/model/data mutation is involved.
- Do not touch `CLAUDE.md`.
