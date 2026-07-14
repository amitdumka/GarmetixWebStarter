# Stage 14D.6 CRM Feedback And WhatsApp Operations

Version: `6.0.36`

## What Changed

- Replaced Customer Feedback placeholder with a real feedback review page, search, rating filters, low-feedback audience handoff and invoice lookup handoff.
- Replaced Review Settings placeholder with store-scoped settings for Google review, private feedback, social links and WhatsApp support.
- Replaced WhatsApp Settings placeholder with store-scoped provider setup, template fields, token-preserving saves and test-send.
- Replaced WhatsApp Logs placeholder with server-side filters, pagination, status metrics and retry action for failed/skipped/queued logs.

## Validation

- `npm.cmd --prefix frontend\modular --workspace @garmetix/crm-web run build`

## Next CRM Work

- Campaign create, preview, queue, send, mark-sent, cancel and ROI review.
- Invoice ad banners create/edit/delete.
- Public `/i/:token` digital bill customer page with PDF, review, feedback and ad banner behavior.
