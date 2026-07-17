# Architecture Requirements

## Boundaries

The module owns conversations, templates, outbound orchestration, queue state, provider configuration and delivery history. Existing modules own their business data and PDFs. Providers own delivery; webhook observations are recorded but are not financial or business truth.

## Core rules

1. Internal messages are database records, not SMTP mailboxes.
2. All outbound sends pass through a single orchestrator and durable queue.
3. Business transactions never wait for provider delivery.
4. Provider selection is scoped and runtime configurable.
5. Secrets never leave the backend after storage.
6. Tenant authorization is backend-enforced on every query and command.
7. Delivery events are append-only and deduplicated.
8. Resending creates linked history; it does not rewrite a prior send.

## Suggested backend components

- Communication application service and recipient resolver.
- Email orchestration and template-rendering services.
- Provider registry/factory and scoped configuration resolver.
- Queue repository, claim/lease service and hosted worker.
- Delivery-event and suppression services.
- Attachment validation/storage service.
- Health, usage and audit integrations.

## Required repository discovery

Before finalizing names, locate existing equivalents for tenants, stores, users, permissions, timestamps, soft deletion, files, data protection, outbox/background jobs, audit/message logs, PDFs and API error handling. Reuse them and document deviations.

## Queue consistency

Prefer the repository's outbox mechanism. Otherwise create the queue record in the same database transaction as the event-producing change where feasible. Claim using a PostgreSQL-safe atomic operation such as `FOR UPDATE SKIP LOCKED` or a repository-equivalent lease. Test two workers against the same records.

## Provider resolution

Resolve in documented priority order, for example store override, company override, tenant default, system fallback. Reject ambiguity and disabled/invalid configurations. Never allow a client to choose a provider outside its authorized scope.

## Recipient resolution

Resolve role/department/store audiences at send time and persist individual recipient rows. Apply broadcast limits and permissions. Do not let later membership changes alter historical recipients.

## Data retention

Make retention configurable for message bodies, attachments, provider payloads and delivery logs. Soft deletion must not defeat required audit retention. Define purge jobs separately and do not introduce destructive cleanup without approval.

