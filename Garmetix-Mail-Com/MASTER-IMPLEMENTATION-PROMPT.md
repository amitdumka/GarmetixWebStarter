# Master Implementation Prompt

Implement the Garmetix `Communication & Mail` module on the exact branch `Mail-Com`.

## System context

- Frontend: Nuxt 4, Vue 3, TypeScript.
- Backend: ASP.NET Core / .NET 10.
- Database: PostgreSQL 16.
- Deployment: Ubuntu and Docker Compose.
- Application: multi-tenant, multi-company, multi-store ERP with role permissions.

Actual repository code is authoritative for naming and patterns. Do not invent a parallel framework where a project abstraction already exists.

## Architecture

Separate internal messages from outbound email. Business modules call one orchestration service, which transactionally creates an idempotent queue/outbox record. A bounded background worker claims records atomically and calls the configured provider. Provider failure must never roll back Sales, Purchase, Payroll, Accounting or Inventory posting.

Create an `ITransactionalEmailProvider`-style abstraction with implementations for Brevo API and secure SMTP. SMTP presets must cover Brevo, GoDaddy Professional Email, Microsoft 365, Gmail, custom SMTP and local Postfix. Presets provide defaults only; use one SMTP implementation.

## Internal communication

Support direct and multi-recipient messages, conversations, replies/reply-all, drafts, sent, archive, trash, soft deletion, priority, read status per recipient, search, pagination, filters and safe attachments. Recipients may be users, authorized roles, departments or stores. Resolve and snapshot recipients when sending so later role changes do not rewrite history.

Every authorization decision must be enforced server-side. Users may see only messages covered by their identity, tenant, company, store, department, role and confidential-data permissions. General communication access must not grant payroll or other sensitive-record access.

## Provider configuration and secrets

Provide admin UI and APIs to add/edit/enable/disable/default/test providers, assign scope, send a test email, inspect health and rotate credentials. Never return a stored secret to the frontend. Blank secret during edit retains it; replacement explicitly rotates it.

Encrypt secrets at rest with the repository's established facility, or ASP.NET Core Data Protection if none exists. Persist production key material outside ephemeral containers, exclude it from Git, and document backup/recovery. Never log credentials, authorization headers, complete provider responses containing sensitive data, or decrypted/encrypted secret values.

## Queue

Statuses must cover Draft, Pending, Scheduled, Processing, Sent, Delivered, Deferred, Bounced, Complained, Rejected, Cancelled, Failed and DeadLetter. Enforce legal transitions.

Use PostgreSQL-safe atomic claiming for multiple workers. Recover stale Processing leases. Use bounded configurable batches, cancellation tokens, graceful shutdown, provider/tenant rate limits, exponential backoff with jitter and sanitized attempt logs. Retry transient timeouts, network errors, 429 and suitable 5xx errors. Do not endlessly retry invalid recipient, sender, authentication or configuration failures.

Use deterministic idempotency such as tenant + event + entity + template + recipient + revision. A resend creates a new linked queue item rather than mutating history.

## Data model

Add repository-conformant equivalents of:

- CommunicationConversation, CommunicationMessage, CommunicationRecipient, CommunicationAttachment, CommunicationPreference.
- EmailProviderConfiguration, EmailTemplate, EmailTemplateVersion, EmailQueueItem, EmailRecipient, EmailAttachment, EmailDeliveryAttempt, EmailDeliveryEvent, EmailSuppressionEntry, EmailUsageCounter.

Include appropriate tenant/company/store ownership, source module/type/id, correlation ID, idempotency key, provider message ID, timestamps in UTC, concurrency token and audit fields. Add indexes for scope, status, schedule, provider, recipient, source and provider message ID.

## Templates

Implement safe HTML and plain-text templates, preview, sample data, test send, version history, approval where appropriate and restore. Escape untrusted values and block scripts/unsafe HTML. Seed or define initial templates for sale invoice, receipt, credit note, purchase inward, vendor payment, payment reminder, payslip, attendance notice, password reset, invitation, daily summary, low stock, system alert and internal-message notification.

Reuse existing document/PDF and calculation services. Do not copy invoice, tax, payroll or report calculations into this module.

## Webhooks and suppression

Implement a Brevo delivery webhook using current official Brevo documentation. Validate its current authentication/signature mechanism, apply size/rate controls, deduplicate events, preserve provider event time and store only sanitized payloads. Map supported sent/delivered/deferred/bounce/blocked/invalid/complaint/unsubscribe/open/click events without treating opens as proof of human reading.

Suppress scoped addresses after confirmed hard bounce, invalid address, complaint or manual action. Provide audited admin removal with a reason. Never leak suppression data across unrelated tenants.

## Attachments

Use existing storage abstraction. Enforce authorization on download, size/count limits, allow-listed extensions, MIME/content validation where practical, non-guessable names, path traversal protection, checksums and executable rejection. Do not return attachment bytes in list endpoints.

## UI

Add authenticated standard-layout routes for dashboard, inbox, sent, drafts, archive, trash, compose, conversation, queue, log, templates, providers, suppression and settings under `/communication`. Include unread badge, pagination, responsive forms, loading/empty/error states and accessible controls.

Queue/log filters must cover date, status, provider, scope, module, recipient, source ID, correlation ID and provider message ID. Permissioned actions: view timeline, retry, cancel pending, reschedule and restore dead-letter. Provide secure links back to source records.

## Integrations

Integrate through orchestration events, never direct provider calls:

- Sales: invoice, receipt, credit note, resend and history.
- Purchase: inward and vendor payment.
- HR/Payroll: payslip, payment and attendance notice.
- Accounting: receipt, payment, approval and daily summary.
- Inventory: low-stock and adjustment approval.
- Administration: invitation, reset and security/system alerts.

Start with low-risk integration points supported by existing event patterns; do not destabilize posting flows.

## Security and audit

Add granular permissions for personal inbox, compose, external sending, store/company broadcast, templates, provider management, queue, retry/cancel, events, suppression and audit. Validate tenant scope from authenticated context, not client input. Prevent email header injection, SSRF through configurable hosts where applicable, insecure TLS, forged webhooks, unsafe templates, ID enumeration and attachment abuse.

Audit provider changes/tests, credential rotation, templates, retries/cancellation, suppression, internal message lifecycle and sensitive attachment access without secrets.

## Optional Postfix relay

Keep it in isolated deployment files. It may accept mail only from approved internal networks/authenticated clients, relay upstream through Brevo with TLS, restrict senders, rotate logs and expose a health check. Never configure an open relay or automatically alter production Compose.

## Delivery discipline

Follow `roadmap-communication-mail.md` and update `todo-communication-mail.md`. Build and test after every stage. Commit coherent stages. Do not merge, push or deploy. Finish with the exact report requested in `START-HERE.md`.

