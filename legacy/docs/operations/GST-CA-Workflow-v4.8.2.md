# GST CA Workflow v4.8.2

## Information required

- Accountant/CA email address
- Accountant/CA display name
- Accountant/CA WhatsApp mobile number with country code, for example `91XXXXXXXXXX`
- Optional review note

## Usage

1. Configure SMTP/Brevo in `.env.production`.
2. Open **GST Reports** or **GST Returns**.
3. Use **Review & Send** / **Send to CA**.
4. Fill CA details.
5. Use **Save as default CA contact**.
6. Confirm email send only after reviewing the GST data.

The app stores the default GST contact in browser local storage, so the same user/browser can reuse it across GST Returns and GST Reports.
