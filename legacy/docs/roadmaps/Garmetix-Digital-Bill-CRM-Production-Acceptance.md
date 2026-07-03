# Garmetix Digital Bill CRM — Production Acceptance

Version target: `4.11.74`

This document covers final acceptance for the Digital Bill CRM module after Digital Bills, WhatsApp, Reviews, Feedback, Ads, Analytics, Activity History, Campaign Audiences, Campaigns, and Campaign ROI.

## Automated readiness page

Open:

```text
Marketing & CRM → Digital Bill Acceptance
```

The page calls:

```text
GET /api/digital-bill-production-checks
```

It checks:

- Digital bill records exist.
- Public links are active.
- `DigitalBills:PublicBaseUrl` is configured.
- Review/private feedback settings exist.
- WhatsApp provider settings exist.
- Meta webhook verify token is configured.
- Active invoice banners exist.
- Campaigns exist.
- Customer feedback exists.

## Manual acceptance checklist

Complete these checks in production/staging:

- Create a sale invoice and generate a digital bill link.
- Open `/i/{token}` without login on mobile.
- Download PDF from the public invoice page.
- Click Google review / Instagram / WhatsApp support buttons.
- Submit private feedback.
- Confirm event/activity history updates.
- Create an active ad banner and click it from public invoice.
- Configure WhatsApp settings in ManualOnly first.
- Configure MetaCloudApi only with approved templates.
- Send invoice WhatsApp from sale register.
- Create campaign audience.
- Create campaign using approved marketing template.
- Send a campaign batch or mark manual campaign as sent.
- Create repeat sale for a recipient and verify Campaign ROI.
- Verify biller/store-manager/admin permissions.
- Verify Salesman/Biller cannot open Marketing & CRM pages, but can generate/copy/send Digital Bills from Billing screens.
- Verify Store Manager can view scoped Digital Bills, WhatsApp Logs, Customer Feedback, and Analytics, but cannot manage settings, banners, campaigns, or acceptance checks.
- Run server builds:

```bash
dotnet build backend/Garmetix.Api/Garmetix.Api.csproj
cd frontend/garmetix-web
npm ci
npm run build
```

## WhatsApp campaign safety

Marketing campaigns must use approved WhatsApp marketing templates. The provider-send endpoint intentionally does not send free-form promotional text. If template name or provider settings are missing, the system keeps recipients as `ManualPending` and allows copy/export.

Expected approved template body parameter order:

```text
{{1}} customer name
{{2}} store name
{{3}} invoice number
{{4}} amount
{{5}} public bill URL
{{6}} offer URL or public bill URL
```

## Definition of done

Digital Bill CRM is production accepted only when:

- Automated readiness page has no failed checks.
- Manual acceptance checklist is completed.
- Sale invoice, PDF, GST, customer, payment and sale register flows still pass regression testing.
- WhatsApp invoice utility template is tested.
- WhatsApp campaign marketing template is tested, or campaign manual mode is accepted.
- Public invoice links are accessible through Cloudflare production domain.
- Public customer invoice page is mobile-friendly, branded, and has clean expired/disabled-link handling.
- No sensitive raw invoice IDs are exposed in public URLs.
