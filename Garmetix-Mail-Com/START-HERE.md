# Garmetix Communication & Mail — Start Here

You are working inside the existing GarmetixWebStarter repository. Read every Markdown file in this package before changing code.

## Mandatory startup sequence

1. Inspect `git status`, the current branch, repository structure, `AGENTS.md`/`CLAUDE.md`, authentication, permissions, tenant resolution, database conventions, background workers, file storage, logging, testing, Docker and existing email code.
2. Preserve all existing and uncommitted work. Never reset, discard or overwrite unrelated changes.
3. Create and switch to the exact new branch:

   ```bash
   git switch -c Mail-Com
   ```

   If `Mail-Com` already exists, inspect it and continue safely; do not recreate or delete it.
4. Copy `todo-communication-mail.md` and `roadmap-communication-mail.md` to the repository root if they are not already there.
5. Reconcile this package with actual repository conventions. Record discoveries and necessary adaptations in `docs/communication-mail-architecture.md` before major implementation.
6. Implement stages in order. After each stage, build, test, update the TODO, and commit only related changes.
7. Do not merge, push, deploy, modify production DNS, or run production migrations without explicit user approval.

## Target outcome

Build an isolated `Communication & Mail` module at `/communication` providing:

- Secure internal Garmetix conversations and attachments.
- Provider-independent transactional email.
- Brevo API and SMTP, standard SMTP presets, and optional local Postfix relay.
- Persistent asynchronous queue, retry, dead-letter, delivery events and suppression.
- Templates and integrations with Sales, Purchase, Accounting, HR/Payroll, Inventory and Administration.
- Encrypted credentials, tenant isolation, permissions, auditing and operational health.

## Explicit exclusions

Do not build or install a public mailbox platform, Dovecot, Roundcube, Mailcow, an open SMTP relay, bulk-marketing engine, or public port-25 service. Do not make business posting depend on successful email delivery.

## Required final report

Return: branch, commits, completed/pending/blocked stages, files, migrations, endpoints, routes, permissions, tests/results, security checks, manual configuration, known limitations, deployment and rollback instructions.

