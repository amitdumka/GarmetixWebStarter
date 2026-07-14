# Stage 11D-51 — Digital Bill Ads, WhatsApp Webhook, Analytics — v4.11.66

Base: `v4.11.65 Digital Bill Invoice Number Hotfix`.

## Added

- Activated `Marketing & CRM → Ad Banners` with real create/update/delete/list UI.
- Added API endpoints for invoice ad banners:
  - `GET /api/invoice-ad-banners`
  - `POST /api/invoice-ad-banners`
  - `PUT /api/invoice-ad-banners/{id}`
  - `DELETE /api/invoice-ad-banners/{id}`
- Public digital invoice page now tracks banner clicks with banner id source.
- Banner `ClickCount` is incremented when public invoice banner is clicked.
- Added Meta WhatsApp webhook endpoints:
  - `GET /api/public/digital-bill-whatsapp/webhook/meta`
  - `POST /api/public/digital-bill-whatsapp/webhook/meta`
- Meta webhook updates WhatsApp message logs to `Sent`, `Delivered`, `Read`, or `Failed` using provider message id.
- Digital invoice WhatsApp status is updated from webhook delivery/read events.
- Activated `Marketing & CRM → Digital Bill Analytics` with date/store filters and daily trend table.
- Added config key `WhatsApp__MetaWebhookVerifyToken` to env examples.
- Added `DigitalBills__PublicBaseUrl` to env examples for public invoice links.

## Operational notes

Set these production env values before using real WhatsApp Cloud API callback delivery/read updates:

```bash
DigitalBills__PublicBaseUrl=https://garmetix.aadwikafashion.in
WhatsApp__MetaWebhookVerifyToken=REPLACE_WITH_RANDOM_WEBHOOK_VERIFY_TOKEN
```

Meta callback URL:

```text
https://garmetix.aadwikafashion.in/api/public/digital-bill-whatsapp/webhook/meta
```

## Validation

- `npm ci` completed.
- Nuxt client build completed successfully in the sandbox.
- Full Nuxt SSR build still stops in this sandbox with `[vite:define] write EPIPE` on Node `v22.16.0`; use Node `22.18+` or the project Docker builder on deployment.
- `dotnet build` could not be run in the sandbox because .NET SDK is not installed.
