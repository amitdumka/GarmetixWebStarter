# Digital Bill CRM TODO

## Done in v4.11.62 Digital Bill CRM MVP

- Backend domain models added under `backend/Garmetix.Domain/Generated/Models/Marketing/DigitalBillCrm.cs`.
- API endpoints and service added under `backend/Garmetix.Api/Marketing/`.
- DbContext DbSets/indexes added.
- Startup schema repair added for existing Docker volumes.
- Manual migration added: `backend/Garmetix.Infrastructure/Data/Migrations/20260626172000_AddDigitalBillCrm.cs`.
- Public invoice page added: `frontend/garmetix-web/pages/i/[token].vue`.
- Admin pages added:
  - `frontend/garmetix-web/pages/marketing/digital-bills.vue`
  - `frontend/garmetix-web/pages/marketing/review-settings.vue`
  - `frontend/garmetix-web/pages/marketing/customer-feedback.vue`
- Menu and route rules added for Marketing & CRM.
- Sale invoice creation attempts digital bill generation after successful commit and returns public path/token when available.

## Done in v4.11.63 WhatsApp Auto-Send slice

- Added `WhatsAppProviderSetting` domain model for store-wise provider setup.
- Added `WhatsAppProviderSettings` DbSet/indexes, migration and schema repair.
- Added `DigitalBillWhatsAppService` with:
  - `ManualOnly` provider mode for safe manual rollout.
  - `MetaCloudApi` dispatch mode for WhatsApp Cloud API credentials.
  - provider fallback to manual log.
  - retry-limit protection.
  - `DigitalBills:PublicBaseUrl` support for hosted invoice links in messages.
- Added authenticated APIs:
  - `GET /api/digital-bill-whatsapp-settings`
  - `PUT /api/digital-bill-whatsapp-settings/store/{storeId}`
  - `POST /api/digital-bill-whatsapp-settings/test-send`
  - `GET /api/digital-bill-whatsapp-logs`
  - `POST /api/digital-bill-whatsapp-logs/{id}/retry`
  - `POST /api/digital-bills/{id}/send-whatsapp`
- Sale invoice creation now attempts WhatsApp auto-send after digital bill generation when the selected store has enabled auto-send.
- Digital Bills page now shows WhatsApp status and has a manual WhatsApp send button.
- WhatsApp Settings page is now active.
- WhatsApp Logs page is now active with filter, copy message and retry actions.
- Version bumped to `4.11.63` / `Stage 11D-48 Digital Bill WhatsApp Auto-Send`.



## Hotfix in v4.11.65 Digital Bill Invoice Number Hotfix

- Fixed manual Digital Bill generation from Marketing & CRM → Digital Bills.
- The previous UI accepted visible invoice numbers like `S-20260625-0001`, but backend route only accepted sale invoice GUIDs.
- Added safe API endpoint `POST /api/digital-bills/sales/generate` with body `{ "invoiceKey": "..." }`.
- Kept backward-compatible route `POST /api/digital-bills/sales/{invoiceKey}/generate`.
- Generation now resolves either sale invoice GUID or invoice number within the current workspace.

## Done in v4.11.64 Sale Register Integration slice

- Sale invoice search/list DTO now includes Digital Bill CRM status fields:
  - digital bill id/path/token
  - active/disabled state
  - WhatsApp status
  - open/download/review counters
  - last WhatsApp sent timestamp
- Sale invoice receipt DTO now includes the same Digital Bill CRM status fields.
- Added billing-friendly APIs so billers do not need to leave the billing screen:
  - `POST /api/billing/sales/{id}/digital-bill`
  - `POST /api/billing/sales/{id}/digital-bill/send-whatsapp`
- Sale invoice register now shows a `Digital Bill` status column.
- Sale invoice register row actions now support:
  - generate digital bill when missing
  - copy digital bill link
  - open customer web bill
  - send/resend WhatsApp digital bill
- Receipt modal now shows a Digital Bill CRM action panel with generate/copy/open/send buttons and open count/status.
- Version bumped to `4.11.64` / `Stage 11D-49 Digital Bill Sale Register Integration`.


## Done in v4.11.66 Ads, Webhook, Analytics slice

- Activated `Marketing & CRM → Ad Banners` with real CRUD UI.
- Added authenticated ad banner APIs under `/api/invoice-ad-banners`.
- Public invoice page now sends banner id in click tracking events.
- Backend increments `InvoiceAdBanner.ClickCount` for public banner clicks.
- Added Meta WhatsApp webhook verify/receive endpoints:
  - `GET /api/public/digital-bill-whatsapp/webhook/meta`
  - `POST /api/public/digital-bill-whatsapp/webhook/meta`
- Webhook updates WhatsApp logs and digital invoice WhatsApp status to `Sent`, `Delivered`, `Read`, or `Failed` where provider message id matches.
- Added `WhatsApp__MetaWebhookVerifyToken` env/example config.
- Activated `Marketing & CRM → Digital Bill Analytics` with date/store filters and daily trend table.
- Version bumped to `4.11.66` / `Stage 11D-51 Digital Bill Ads Webhook Analytics`.

## Must test first

1. Build backend on .NET 10 SDK:

```bash
dotnet build backend/Garmetix.Api/Garmetix.Api.csproj
```

2. Build frontend:

```bash
cd frontend/garmetix-web
npm install
npm run build
```

3. Run database migration/repair in Docker production environment.
4. Set hosted public URL if WhatsApp links must use Cloudflare domain:

```bash
DigitalBills__PublicBaseUrl=https://garmetix.aadwikafashion.in
```

5. Configure Marketing & CRM → WhatsApp Settings:
   - Start with `ManualOnly`, `Provider enabled=true`, `Auto-send after sale=true`.
   - Create sale invoice.
   - Check Digital Bills WhatsApp status becomes `ManualPending`.
   - Check WhatsApp Logs and copy the message.
6. For live WhatsApp Cloud API:
   - Provider: `MetaCloudApi`.
   - Fill API token and phone number ID.
   - Use approved template name/language if sending outside customer service window.
   - Use Test Send.
   - Create a sale invoice and verify status/log.

## Next implementation prompt

Continue Digital Bill CRM Stage 5:

- Add customer activity drawer from sale register/digital bill register.
- Add expired-link/admin retention cleanup tools.
- Add banner upload/path support or URL-only guarded workflow with image validation.
- Add WhatsApp provider-specific implementations for Gupshup/Interakt/AiSensy/Wati if selected.
- Add campaign grouping and ROI reporting for banners/review/feedback.
- Add tests for invalid/disabled/expired public token, ManualOnly auto-send, missing mobile, provider disabled, retry limit, webhook status update, register actions, receipt modal digital bill actions, and banner click tracking.


## Done in v4.11.68 SelectItem stability hotfix

- Replaced empty-string `USelect` store options in Digital Bill Ad Banners and Analytics pages with `__ALL_STORES__` sentinel values.
- Preserved API behavior by translating the sentinel back to omitted/null store filters before requests/payloads.
- This fixes the Nuxt UI runtime error: `A <SelectItem /> must have a value prop that is not an empty string`.

## Done in v4.11.67 Activity History slice

- Added `GET /api/digital-bills/{id}/activity` with summary metrics, timeline, raw events, WhatsApp logs and customer feedback.
- Added Digital Bills page Activity action and activity modal/drawer for per-invoice customer journey review.
- Added Sale Register and receipt modal Activity action that opens the matching Digital Bill activity history.
- Fixed the v4.11.66 backend typo in `GenerateForSaleAsync` where `CancellationToken` was duplicated in the method signature.
- Version bumped to `4.11.67` / `Stage 11D-52 Digital Bill Activity History`.

## Done in v4.11.69 Campaign Audiences slice

- Added authenticated API `GET /api/digital-bill-audiences`.
- Groups Digital Bill customers by mobile/name and returns campaign-ready audience rows.
- Segments added:
  - all customers
  - opened bill
  - not opened
  - PDF downloaded
  - review clicked
  - review pending
  - feedback submitted
  - low feedback
  - WhatsApp failed
  - WhatsApp pending
  - no mobile
- Added `Marketing & CRM → Campaign Audiences` page.
- Page supports date/store/search/segment filters.
- Added summary cards that switch segment with one click.
- Added copy mobile list action for WhatsApp/manual campaigns.
- Added CSV export for campaign handoff.
- Added per-row recommendation text, last bill link and activity shortcut.
- Version bumped to `4.11.69` / `Stage 11D-54 Digital Bill Campaign Audiences`.

## Next after v4.11.69

- Add campaign creation/scheduling model from selected audiences.
- Add campaign send logs and ROI tracking by audience segment.
- Add expired-link/admin retention cleanup tools.
- Add banner image upload/path support instead of URL-only banners.
- Add WhatsApp provider-specific implementations for Gupshup/Interakt/AiSensy/Wati if selected.
- Add automated tests for Digital Bill audiences, invalid/disabled/expired public token, ManualOnly auto-send, missing mobile, provider disabled, retry limit, webhook status update, register actions, receipt modal digital bill actions, and banner click tracking.

## Done in v4.11.70 Campaigns slice

- Added `DigitalBillCampaign` and `DigitalBillCampaignRecipient` entities.
- Added EF migration and startup schema repair for campaign and recipient storage.
- Added `Marketing & CRM → Campaigns` page.
- Added campaign preview from Digital Bill audience filters.
- Added campaign creation from segments like review pending, not opened, WhatsApp failed and low feedback.
- Added recipient preparation with rendered message body variables:
  - `{{customerName}}`
  - `{{storeName}}`
  - `{{invoiceNumber}}`
  - `{{publicUrl}}`
  - `{{amount}}`
  - `{{offerUrl}}`
- Added campaign lifecycle:
  - Draft
  - Queued / ManualPending
  - Completed / Sent
  - Cancelled
- Added copy campaign messages and export recipients CSV.
- Added Campaigns link in Marketing & CRM menu and access control.
- Version bumped to `4.11.70` / `Stage 11D-55 Digital Bill Campaigns`.

## Next after v4.11.70

- Add campaign ROI tracking based on purchases after campaign date.
- Add approved WhatsApp marketing-template send flow after provider setup is verified.
- Add campaign performance dashboard: sent, opened after campaign, review clicks after campaign, repeat purchase, revenue generated.

## Done in v4.11.71 Campaign ROI Tracking slice

- Added `GET /api/digital-bill-campaigns/{id}/roi?days=30`.
- ROI is calculated without adding new tables:
  - attribution starts from campaign completed/queued/sent/created time
  - attribution window is configurable from 1 to 365 days
  - source invoices used for the campaign are excluded from repeat sales
  - repeat sales are matched by normalized customer mobile number
- ROI metrics added:
  - engaged recipient count and engagement rate
  - opens after campaign
  - PDF downloads after campaign
  - Google review clicks after campaign
  - feedback submitted after campaign
  - banner clicks after campaign
  - repeat customers
  - repeat invoices
  - repeat sales amount
  - average repeat bill amount
  - repeat purchase rate
- Campaign detail page now shows a ROI panel with attribution dates, engagement, repeat purchase and repeat sales.
- Campaign detail page now shows repeat sales table and per-recipient ROI summary.
- Version bumped to `4.11.71` / `Stage 11D-56 Digital Bill Campaign ROI`.

## Next after v4.11.71

- Add approved WhatsApp marketing-template send flow after provider setup is verified.
- Add campaign dashboard page for cross-campaign comparison.
- Add expired-link/admin retention cleanup tools.
- Add banner image upload/path support instead of URL-only banners.
- Add WhatsApp provider-specific implementations for Gupshup/Interakt/AiSensy/Wati if selected.
- Add automated tests for Digital Bill campaigns and ROI attribution.


## Done in v4.11.72/v4.11.73 WhatsApp Campaign Send + Acceptance slice

- Added approved WhatsApp marketing-template campaign send API.
- Added safe campaign sending behavior: API sending only runs with a campaign template name and MetaCloudApi store settings.
- Manual/SMS/manual-copy campaigns remain ManualPending and exportable.
- Added campaign batch result summary: attempted, sent, manual pending, failed, skipped.
- Added Marketing & CRM → Digital Bill Acceptance page.
- Added automated production readiness endpoint `/api/digital-bill-production-checks`.
- Added production acceptance document.
- Fixed duplicate CSV recipient row export in Campaigns page.

## Next after v4.11.73

- Run full backend `dotnet build` on server.
- Run full Nuxt SSR production build with Node 22.18+.
- Test a real Meta approved marketing template with six body variables.
- Perform full regression testing around sales invoice create/print/revise, mixed payments, sale returns, and day close.

## Done in v4.11.74 Permission Hardening + UI Polish slice

- Hardened backend Marketing access so Salesman/Biller no longer has general Marketing API access.
- Kept biller Digital Bill generation/resend available through Billing screen endpoints only.
- Restricted review settings, WhatsApp settings, ad banners, audiences, campaigns, and production acceptance APIs to Marketing + Edit-capable users.
- Kept store-manager scoped access for Digital Bills, WhatsApp Logs, Customer Feedback, and Digital Bill Analytics.
- Allowed store managers to resend Digital Bill WhatsApp links from the scoped Digital Bills page without global edit/delete rights.
- Updated frontend route access rules and sidebar visibility to match the backend role split.
- Polished the public customer invoice page with better mobile layout, sticky quick actions, improved loading/error states, branded header, cleaner totals, social/support actions, banner captions, and noindex metadata.
- Fixed duplicate accountant role assignment in frontend access-role normalization.

## Next after v4.11.74

- Run full backend `dotnet build` on server.
- Run full Nuxt SSR production build with Node 22.18+.
- Login-test the permission matrix using Owner/Admin, PowerUser, StoreManager, Salesman/Biller, Accounts, and Normal User.
- Begin full final regression testing only after build and permission smoke tests pass.


## Done in v4.11.75 Build Hotfix slice

- Restored missing `DigitalBillWhatsAppService` helper methods referenced by campaign template sending.
- Added campaign Meta template send helper, campaign result summary helper, absolute public URL helper, and recipient status count helper.
- This fixes API publish errors for `BuildCampaignSendResult`, `BuildAbsoluteUrl`, `SendMetaCampaignTemplateAsync`, and `CountCampaignRecipientsAsync`.
