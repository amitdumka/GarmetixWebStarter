# Configuration, Deployment and Rollback

## Manual configuration

- Create/verify Brevo account and transactional sender/domain.
- Add Brevo-provided account-specific DNS records manually.
- Maintain one valid SPF record; do not create conflicting SPF TXT records.
- Configure DKIM and a staged DMARC policy.
- Configure a real Reply-To mailbox if replies must be received.
- Create API/SMTP credentials and enter them only through secure configuration.
- Configure and verify the webhook URL using current Brevo guidance.
- Persist and back up encryption/Data Protection keys.

## Pre-deployment

Back up PostgreSQL and protection keys, record the current version, validate migrations on a database copy, run all builds/tests, check Docker configuration, verify existing invoice/payroll paths and prepare the prior release for rollback.

## Deployment

Provide repository-specific commands but do not execute production deployment without approval. Apply reviewed migrations, start services, verify health/queue/provider/webhook, send controlled tests and monitor failures. Enable business integrations gradually.

## Rollback

Document application rollback separately from database rollback. Prefer forward-compatible migrations and feature flags so the old application can run safely. Never issue destructive down-migrations against production without an approved backup/restore plan. Preserve queued/delivery audit data.

## Optional Postfix

Keep it disabled and isolated by default. Restrict ingress to the internal application network, relay to Brevo over TLS, restrict senders, protect credentials, rotate logs and document queue inspection. Never expose an unauthenticated relay.

