# Garmetix Digital Bill CRM Roadmap

## Goal

Build a Billfree-like module natively inside GarmetixWebStarter so every sale invoice can become a secure customer-facing digital bill link with PDF download, WhatsApp delivery, review/social actions, feedback, banners, and analytics.

Target flow:

```text
Sale Invoice Finalized
→ Digital invoice link generated
→ PDF copy available
→ WhatsApp message sent to customer
→ Customer opens mobile invoice web page
→ Customer downloads PDF / gives Google review / follows Instagram / sends feedback
→ Store owner tracks views, review clicks, WhatsApp delivery, feedback, and campaign performance
```

## Menu

```text
Marketing & CRM
  - Digital Bills
  - WhatsApp Settings
  - WhatsApp Logs
  - Review Settings
  - Ad Banners
  - Customer Feedback
  - Digital Bill Analytics
```

## Important compliance rule

Do not incentivize Google reviews. The invoice page can ask for an honest review and can show a Google review link, but must not give a discount, cashback, points, gift, or reward for posting/changing/removing a review.

## Stage 1 — Digital Invoice Link MVP

- [x] Add roadmap file into project docs.
- [x] Add backend domain models for DigitalInvoices, DigitalInvoiceEvents, StoreReviewSettings, CustomerFeedback, WhatsAppMessageLogs, and InvoiceAdBanners.
- [x] Add EF DbSets and indexes.
- [x] Add idempotent startup schema repair for Digital Bill CRM tables.
- [x] Add manual EF migration `20260626172000_AddDigitalBillCrm`.
- [x] Add API endpoints to generate, list, disable, and regenerate digital bill links.
- [x] Add public digital bill API by secure token.
- [x] Add public PDF download by secure token.
- [x] Add public event tracking and feedback submission APIs.
- [x] Add Nuxt public invoice page `/i/[token]`.
- [x] Add admin page `/marketing/digital-bills`.
- [x] Add review settings page `/marketing/review-settings`.
- [x] Add customer feedback page `/marketing/customer-feedback`.
- [x] Add menu links and route access rules.
- [ ] Run `dotnet build` on a machine with .NET 10 SDK.
- [ ] Run Nuxt production build.
- [ ] Test against a real sale invoice in Docker/Postgres.

Acceptance:

- A user can generate a secure public link from a sale invoice ID.
- Customer can open `/i/{token}` without login.
- Customer can download invoice PDF.
- Admin can copy, disable, or regenerate link.
- Customer can submit feedback.
- Review/Instagram/WhatsApp support buttons are controlled store-wise.

## Stage 2 — Auto Generate After Sale Finalization

- [x] Hook DigitalBillCrmService into sale invoice finalization with safe try/catch after sale commit.
- [x] Return digital bill public path/token in sale invoice response.
- [x] Add “Copy Digital Bill Link” action on sale invoice success screen.
- [ ] Add “Send WhatsApp” action on sale invoice success screen after provider setup.
- [ ] Ensure invoice creation never fails only because digital bill generation fails.
- [ ] Add dedicated audit/event log for automatic generation failures.

## Stage 3 — WhatsApp Provider Setup

- [ ] Add WhatsApp provider settings table/page.
- [ ] Add provider enum: MetaCloudApi, Gupshup, Interakt, AiSensy, Wati, Twilio, ManualOnly.
- [ ] Add `IWhatsAppSender` abstraction.
- [ ] Add template preview and test-send endpoint.
- [ ] Save provider secrets securely through deployment env/config.

## Stage 4 — Auto WhatsApp Send

- [ ] Add message queue/background worker.
- [ ] Send digital bill utility template after invoice finalization.
- [ ] Log queued/sent/delivered/failed/read statuses.
- [ ] Add retry/resend actions.
- [ ] Handle missing/invalid mobile without breaking billing.

## Stage 5 — Review, Feedback, and Tracking

- [x] Add Google review/Instagram/WhatsApp/private feedback settings model.
- [x] Add public review/social buttons.
- [x] Add private feedback capture.
- [x] Add open/PDF/review/social/feedback event tracking base.
- [ ] Add events drawer on Digital Bills admin page.
- [ ] Add spam/rate-limit protection for public feedback.
- [ ] Add store-level report filters.

## Stage 6 — Marketing Banners

- [x] Add `InvoiceAdBanner` model and database storage.
- [x] Add public invoice banner rendering support.
- [ ] Add admin create/edit/delete UI for banners.
- [ ] Add image upload/selection.
- [ ] Add date-based and store-based campaign targeting.
- [ ] Track banner clicks in analytics.

## Stage 7 — Analytics

- [ ] Add Digital Bill Analytics backend endpoint.
- [ ] Add cards for generated/opened/PDF/review/feedback/WhatsApp status.
- [ ] Add trend charts by date.
- [ ] Add top customer/top store tables.
- [ ] Add export/share report for owner.

## Stage 8 — Production Hardening

- [ ] Add public endpoint rate limiting.
- [ ] Keep `X-Robots-Tag: noindex,nofollow` and frontend meta robots.
- [ ] Ensure all public tokens are unguessable and revocable.
- [ ] Mask customer mobile number on public page.
- [x] Add retention configuration in env examples: `DigitalBills__RetentionDays=180`.
- [ ] Add full Docker deployment test.
- [ ] Add test cases for invalid, disabled, expired tokens.
- [ ] Add permissions review for owner/admin/power user/store manager/salesman.

## API Summary

Authenticated:

```text
GET  /api/digital-bills
POST /api/digital-bills/sales/{invoiceId}/generate
POST /api/digital-bills/{id}/disable
POST /api/digital-bills/{id}/regenerate-token
GET  /api/digital-bills/{id}/events
GET  /api/digital-bills/feedback
GET  /api/digital-bill-review-settings
PUT  /api/digital-bill-review-settings/store/{storeId}
```

Public:

```text
GET  /api/public/digital-bills/{token}
GET  /api/public/digital-bills/{token}/pdf
POST /api/public/digital-bills/{token}/events
POST /api/public/digital-bills/{token}/feedback
```

## Current implementation note

This package starts the module with a safe MVP and a protected sale-finalization hook. WhatsApp provider sending is intentionally left for the next iteration so existing billing cannot be broken by provider/API configuration errors.
