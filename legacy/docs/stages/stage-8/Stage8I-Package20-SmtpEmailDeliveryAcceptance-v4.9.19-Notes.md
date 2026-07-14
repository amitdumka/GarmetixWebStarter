# Stage 8I Package 20 - SMTP Email Integration and Delivery Acceptance v4.9.19

This package upgrades SMTP from a production-readiness subsection into a dedicated admin workflow.

## Implemented

- Added `/email-delivery` admin page.
- Added Email Delivery to the Maintenance menu and route access rules.
- Extended `/api/email-diagnostics/status` with provider detection, authentication flag, timeout seconds and required environment keys.
- Hardened `SmtpEmailSender` validation for placeholder hosts, invalid ports and missing password when username is configured.
- Added `EMAIL_TIMEOUT_SECONDS` to Docker/env templates.
- Added `scripts/linux/smtp-acceptance-drill.sh` for host-level SMTP acceptance.
- Added `scripts/validation/smtp-delivery-acceptance-check.py`.
- Added `SMTP_DELIVERY_ACCEPTANCE` to the Test Automation manifest and smoke scripts.

## Validation

Run:

```bash
python3 scripts/validation/current-release-checks.py
python3 scripts/validation/smtp-delivery-acceptance-check.py
python3 scripts/validation/frontend-route-access-check.py
python3 scripts/validation/secret-hygiene-check.py
bash -n scripts/linux/smtp-acceptance-drill.sh scripts/linux/smoke-test.sh
```

Live send requires SMTP credentials and `GARMETIX_SMTP_SEND_TEST=true`.
