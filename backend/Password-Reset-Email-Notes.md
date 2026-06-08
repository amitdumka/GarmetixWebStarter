# Password Reset Email Delivery

This build sends forgot-password reset links through SMTP when email delivery is enabled.

## Backend endpoints

- `POST /api/auth/forgot-password`
  - Accepts `userNameOrEmail`.
  - Always returns a generic message so the login screen does not reveal whether an account exists.
  - If the account exists and SMTP is enabled, sends an email containing both:
    - reset link: `/?token=...`
    - reset token for manual paste into the Reset tab
- `POST /api/auth/reset-password`
  - Accepts `token` and `newPassword`.
- `POST /api/auth/change-password`
  - Requires login and accepts `currentPassword` and `newPassword`.

## Configuration

Set these in `.env` for `docker-compose.prod.yml`, or use matching ASP.NET Core environment variables.

```env
PASSWORD_RESET_FRONTEND_BASE_URL=https://your-domain.com

EMAIL_ENABLED=true
EMAIL_HOST=smtp.your-provider.com
EMAIL_PORT=587
EMAIL_ENABLE_SSL=true
EMAIL_USERNAME=your-smtp-username
EMAIL_PASSWORD=your-smtp-password-or-app-password
EMAIL_FROM_EMAIL=no-reply@your-domain.com
EMAIL_FROM_NAME=Garmetix
EMAIL_REPLY_TO_EMAIL=support@your-domain.com
```

ASP.NET Core environment variable names used by Docker:

```text
PasswordReset__FrontendBaseUrl
Email__Enabled
Email__Host
Email__Port
Email__EnableSsl
Email__UserName
Email__Password
Email__FromEmail
Email__FromName
Email__ReplyToEmail
```

## Provider examples

### Gmail / Google Workspace

Use an app password, not your normal Gmail password.

```env
EMAIL_HOST=smtp.gmail.com
EMAIL_PORT=587
EMAIL_ENABLE_SSL=true
EMAIL_USERNAME=youraddress@gmail.com
EMAIL_PASSWORD=your-google-app-password
EMAIL_FROM_EMAIL=youraddress@gmail.com
```

### SendGrid SMTP

```env
EMAIL_HOST=smtp.sendgrid.net
EMAIL_PORT=587
EMAIL_ENABLE_SSL=true
EMAIL_USERNAME=apikey
EMAIL_PASSWORD=your-sendgrid-api-key
EMAIL_FROM_EMAIL=no-reply@your-verified-domain.com
```

### Office 365 / Microsoft 365 SMTP

```env
EMAIL_HOST=smtp.office365.com
EMAIL_PORT=587
EMAIL_ENABLE_SSL=true
EMAIL_USERNAME=no-reply@your-domain.com
EMAIL_PASSWORD=mailbox-password-or-app-password
EMAIL_FROM_EMAIL=no-reply@your-domain.com
```

## Security behavior

- In Production, the reset token is never returned in the API response.
- In Development, if email is disabled, the token/link is returned so local testing still works.
- Reset tokens are signed with `Jwt:SigningKey` and expire after 30 minutes.
- The next stricter production improvement is storing reset tokens in a database table so tokens can be revoked after first use.
