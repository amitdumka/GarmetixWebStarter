# SMTP Email Delivery Acceptance v4.9.19

Stage 8I Package 20 adds a dedicated admin acceptance workflow for SMTP email delivery.

## Admin page

Open:

```text
/email-delivery
```

Use this page to:

- View masked SMTP status.
- Confirm the detected provider name.
- Check the required `.env.production` keys.
- Send a real test email.
- Review Brevo, Gmail and Outlook setup hints.

Secrets are not exposed in the browser. SMTP passwords/API keys must stay in the private host `.env.production` file.

## Recommended Brevo SMTP settings

```env
EMAIL_ENABLED=true
EMAIL_HOST=smtp-relay.brevo.com
EMAIL_PORT=587
EMAIL_ENABLE_SSL=true
EMAIL_USERNAME=your-brevo-smtp-login
EMAIL_PASSWORD=your-brevo-smtp-key
EMAIL_FROM_EMAIL=no-reply@yourdomain.in
EMAIL_FROM_NAME=Garmetix
EMAIL_REPLY_TO_EMAIL=owner@yourdomain.in
EMAIL_TIMEOUT_SECONDS=30
PASSWORD_RESET_FRONTEND_BASE_URL=https://yourdomain.in
```

Restart the API container after editing `.env.production`.

## Host acceptance drill

Status-only contract check:

```bash
export GARMETIX_SMOKE_USER='admin'
export GARMETIX_SMOKE_PASSWORD='your-admin-password'
./scripts/linux/smtp-acceptance-drill.sh .env.production
```

Real send test:

```bash
export GARMETIX_SMOKE_USER='admin'
export GARMETIX_SMOKE_PASSWORD='your-admin-password'
export GARMETIX_SMTP_SEND_TEST=true
export GARMETIX_SMTP_TEST_TO='owner@example.com'
./scripts/linux/smtp-acceptance-drill.sh .env.production
```

Acceptance is complete only when the recipient confirms the email arrived in Inbox and not Spam.
