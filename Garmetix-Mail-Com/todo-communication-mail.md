# Communication & Mail TODO

## Safety

- [ ] Read repository agent instructions and inspect working tree.
- [ ] Create/use exact branch `Mail-Com` without losing existing work.
- [ ] Identify existing email/notification code and compatibility requirements.
- [ ] Confirm no merge, push, deployment or production migration is performed.

## Design and database

- [ ] Document repository-specific architecture and affected projects.
- [ ] Define conversation/message/recipient/attachment/preference entities.
- [ ] Define provider/template/queue/attempt/event/suppression/usage entities.
- [ ] Add tenant/company/store ownership and UTC audit fields.
- [ ] Add constraints, concurrency tokens, idempotency and indexes.
- [ ] Create and review migrations.

## Providers and secrets

- [ ] Implement provider abstraction.
- [ ] Implement Brevo transactional API provider.
- [ ] Implement reusable secure SMTP provider.
- [ ] Add Brevo, GoDaddy, Microsoft 365, Gmail, custom and local-relay presets.
- [ ] Encrypt credentials and persist protection keys safely.
- [ ] Mask secrets in APIs/UI/logs and implement rotation.
- [ ] Add provider selection, connection test and test email.

## Queue

- [ ] Implement transactional/idempotent enqueue.
- [ ] Implement legal queue state transitions.
- [ ] Implement atomic multi-worker claim and processing lease.
- [ ] Implement bounded batches, rate limits and graceful shutdown.
- [ ] Implement transient classification, backoff/jitter and dead-letter.
- [ ] Add attempts, sanitized errors, health and metrics.

## Templates and integrations

- [ ] Add safe HTML/text template CRUD, validation and preview.
- [ ] Add versioning, test send, approval and restore.
- [ ] Add initial system templates.
- [ ] Integrate Sales without affecting invoice posting.
- [ ] Integrate Purchase and Accounting.
- [ ] Integrate HR/Payroll with confidential permissions.
- [ ] Integrate Inventory and Administration.

## Internal communication

- [ ] Add conversation, compose, reply/reply-all and resolved recipients.
- [ ] Add inbox/sent/draft/archive/trash and per-user read state.
- [ ] Add search, filters, pagination and unread badge.
- [ ] Add notification preferences and optional external notification.
- [ ] Add secure attachments with authorization and validation.

## Webhooks and suppression

- [ ] Verify current official Brevo webhook security requirements.
- [ ] Implement authenticated, limited and deduplicated webhook.
- [ ] Store delivery timeline and sanitized events.
- [ ] Implement scoped hard-bounce/invalid/complaint/manual suppression.
- [ ] Add audited suppression removal with reason.

## UI and permissions

- [ ] Add all `/communication` routes using standard layout.
- [ ] Add granular permissions and backend authorization.
- [ ] Add providers, templates, queue/log, events and suppression pages.
- [ ] Add responsive loading, empty, validation and error states.
- [ ] Verify cross-tenant/store/company access is rejected.

## Optional relay and operations

- [ ] Add disabled isolated Postfix relay profile.
- [ ] Restrict networks/senders and configure TLS upstream relay.
- [ ] Add health, logs, queue operations and disable/fallback guide.
- [ ] Document Brevo domain, DKIM, SPF, DMARC and Reply-To setup.

## Testing and handoff

- [ ] Run unit and integration tests.
- [ ] Test multiple workers and duplicate prevention.
- [ ] Test provider outage while business posting succeeds.
- [ ] Test secret leakage, header injection, webhook forgery and attachments.
- [ ] Run manual acceptance checklist.
- [ ] Update docs, completed/pending/blocked report and rollback plan.
- [ ] Confirm branch remains unmerged and undeployed.

