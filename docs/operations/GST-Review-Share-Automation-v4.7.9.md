# GST Review & CA Share Automation v4.7.9

## GST Return flow

1. Open **GST Returns**.
2. Select company and return period.
3. Click **Load From Books**.
4. Click **Preview** and fix validation issues.
5. Save the GST draft.
6. Click **Send to CA** / **Review & Send**.
7. Enter Accountant/CA email and optional WhatsApp number.
8. Select attachments: JSON, Excel, HSN CSV, tax summary CSV, invoice register CSV.
9. Confirm send.
10. After email sends, use the WhatsApp button to share the prepared message.

## GST Reports flow

1. Open **GST Reports**.
2. Select period and direction.
3. Refresh reports.
4. Click **Send to CA**.
5. Select HSN summary, tax summary and invoice register CSV attachments.
6. Confirm send.

## Required setup

SMTP must be configured in `.env.production` / appsettings environment variables:

- `Email__Enabled=true`
- `Email__Host`
- `Email__Port`
- `Email__FromEmail`
- `Email__UserName` and `Email__Password`, if required by your provider

WhatsApp automatic background sending is not done without WhatsApp Business API credentials. The system prepares a WhatsApp share link/message after the email package is sent.
