# Communication & Mail Roadmap

## CM-01 — Discovery and design

Inspect repository rules and existing authentication, tenancy, permissions, logging, background work, storage, notification/email and Docker patterns. Produce a repository-specific architecture and impact list. No major code before resolving conflicting conventions.

## CM-02 — Domain and database

Add scoped entities, enums, transition rules, migrations, constraints and indexes. Test tenant filtering, legal transitions, concurrency and idempotency.

## CM-03 — Provider framework

Add provider abstraction, Brevo API, reusable SMTP implementation/presets, secret protection, provider selection, connection test and test-send service.

## CM-04 — Queue and worker

Implement durable enqueue/outbox, atomic claim, processing lease, bounded concurrency, retries with jitter, rate limits, attempts, dead-letter, metrics and health.

## CM-05 — Provider administration

Add permissioned provider UI/APIs, masked credentials, secret rotation, defaults/scope, tests, health and usage.

## CM-06 — Templates

Add safe HTML/text templates, validation, preview, test send, versioning, approval/restore and initial templates.

## CM-07 — Internal inbox

Add conversations, resolved recipients, drafts, replies, archive/trash, read state, preferences, secure attachments, search and unread navigation badge.

## CM-08 — Business integration

Integrate Sales first, followed by Purchase/Accounting, HR/Payroll, Inventory and Administration. Reuse existing PDFs and links. Email outages must not affect posting.

## CM-09 — Webhooks and suppression

Add verified/deduplicated Brevo events, delivery timeline, bounce/complaint suppression and audited management.

## CM-10 — Optional relay

Add isolated, disabled-by-default Postfix-to-Brevo deployment profile and operations guide. Never expose an open relay.

## CM-11 — QA and hardening

Run unit, integration, authorization, multi-worker, failure, secret-leak, template, attachment and webhook security tests. Perform manual acceptance.

## CM-12 — Release package

Prepare migration, configuration, DNS, deployment, health and rollback instructions. Do not merge or deploy without approval.

