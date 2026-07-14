# SMTP Email Delivery Validation v4.6.2

Configure SMTP in `.env.production`:

```env
EMAIL_ENABLED=true
EMAIL_HOST=smtp.example.com
EMAIL_PORT=587
EMAIL_ENABLE_SSL=true
EMAIL_USERNAME=your-user
EMAIL_PASSWORD=your-app-password
EMAIL_FROM_EMAIL=no-reply@your-domain.com
EMAIL_FROM_NAME=Garmetix
EMAIL_REPLY_TO_EMAIL=support@your-domain.com
```

Then open **Production Readiness** and send a test email.

API endpoints:

- `GET /api/email-diagnostics/status`
- `POST /api/email-diagnostics/send-test`

Both endpoints require admin login.
