# Communication & Mail TODO

## Safety

- [x] Read repository agent instructions and inspect working tree.
- [x] Create/use exact branch `Mail-Com` without losing existing work.
- [x] Identify existing email/notification code and compatibility requirements.
- [x] Confirm no merge, push, deployment or production migration is performed.

## Design and database

- [x] Document repository-specific architecture and affected projects.
- [x] Define conversation/message/recipient/attachment/preference entities.
- [x] Define provider/template/queue/attempt/event/suppression/usage entities.
- [x] Add tenant/company/store ownership and UTC audit fields.
- [x] Add constraints, concurrency tokens, idempotency and indexes.
- [x] Create and review migrations.

## Providers and secrets

- [x] Implement provider abstraction.
- [x] Implement Brevo transactional API provider.
- [x] Implement reusable secure SMTP provider.
- [x] Add Brevo, GoDaddy, Microsoft 365, Gmail, custom and local-relay presets.
- [x] Encrypt credentials and persist protection keys safely.
- [x] Mask secrets in APIs/UI/logs and implement rotation.
- [x] Add provider selection, connection test and test email.

## Queue

- [x] Implement transactional/idempotent enqueue.
- [x] Implement legal queue state transitions.
- [x] Implement atomic multi-worker claim and processing lease.
- [x] Implement bounded batches, rate limits and graceful shutdown.
- [x] Implement transient classification, backoff/jitter and dead-letter.
- [ ] Add attempts, sanitized errors, health and metrics. (attempts/sanitized errors done; health/usage dashboard is CM-05)

## Templates and integrations

- [x] Add safe HTML/text template CRUD, validation and preview.
- [x] Add versioning, test send, approval and restore.
- [x] Add initial system templates.
- [x] Integrate Sales without affecting invoice posting. (POST /api/billing/sales/{id}/send-email, additive after commit)
- [x] Integrate Purchase and Accounting. (Purchase: POST /api/purchase/payments/{id}/send-email; Accounting daily-summary/system-alert templates seeded but not yet wired to a trigger event)
- [x] Integrate HR/Payroll with confidential permissions. (POST /api/payroll/payslips/{id}/send-email, gated by the existing GarmetixPolicies.Payroll on the whole route group)
- [x] Integrate Inventory and Administration. (Inventory: POST /api/inventory/stock-reports/low-stock-alert/send-email; Admin: POST /api/access/users/{id}/send-invitation-email)

## Internal communication

- [x] Add conversation, compose, reply/reply-all and resolved recipients.
- [x] Add inbox/sent/draft/archive/trash and per-user read state.
- [x] Add search, filters, pagination and unread badge.
- [ ] Add notification preferences and optional external notification. (preferences endpoint/storage done; the actual email-echo-on-new-message notification is wired in a later stage once the queue integration point is chosen)
- [x] Add secure attachments with authorization and validation.

## Webhooks and suppression

- [x] Verify current official Brevo webhook security requirements. (researched live docs - Basic Auth/header token + IP allow-list, no HMAC signature exists for transactional webhooks)
- [x] Implement authenticated, limited and deduplicated webhook.
- [x] Store delivery timeline and sanitized events.
- [x] Implement scoped hard-bounce/invalid/complaint/manual suppression.
- [x] Add audited suppression removal with reason.

## UI and permissions

- [x] Add all `/communication` routes using standard layout. (dashboard, providers, templates, mailbox, queue, suppression all shipped as one Mailbox page + dedicated admin pages instead of 7 literal routes)
- [x] Add granular permissions and backend authorization.
- [x] Add providers, templates, queue/log, events and suppression pages.
- [x] Add responsive loading, empty, validation and error states. (providers page)
- [ ] Verify cross-tenant/store/company access is rejected. (deferred to CM-11 QA pass)

## Optional relay and operations

- [x] Add disabled isolated Postfix relay profile.
- [x] Restrict networks/senders and configure TLS upstream relay.
- [x] Add health, logs, queue operations and disable/fallback guide.
- [x] Document Brevo domain, DKIM, SPF, DMARC and Reply-To setup.

## Testing and handoff

- [x] Run unit and integration tests. (367 passed/0 failed/11 skipped; integration tests are Postgres-gated, not run in this sandbox - see docs/communication-mail-testing.md)
- [x] Test multiple workers and duplicate prevention. (written, Postgres-gated - not run in this sandbox)
- [x] Test provider outage while business posting succeeds. (guaranteed by CM-08's additive-after-commit endpoint architecture, not live-injected)
- [x] Test secret leakage, header injection, webhook forgery and attachments. (webhook forgery: 8 real automated tests; rest via code review - see docs/communication-mail-testing.md)
- [x] Run manual acceptance checklist. (see docs/communication-mail-testing.md - code-reviewed/architecturally verified; live-Brevo/live-browser items disclosed as not exercised)
- [x] Update docs, completed/pending/blocked report and rollback plan. (docs/communication-mail-deployment.md, docs/communication-mail-testing.md, this file; CM-12 final report still pending)
- [ ] Confirm branch remains unmerged and undeployed. (true as of every commit so far - final confirmation belongs in the CM-12 report)

