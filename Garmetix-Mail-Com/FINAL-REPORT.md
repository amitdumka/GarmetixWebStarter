# Final Implementation Report — Garmetix Communication & Mail

```text
Branch: Mail-Com
Latest commit: b182ccd feat(communication-mail): CM-11 QA and hardening
(branched from version7 at 55ced28; 11 commits, one per completed stage)

Completed stages:
- CM-01 Discovery and design
- CM-02 Domain and database
- CM-03 Provider framework
- CM-04 Queue and worker
- CM-05 Provider administration
- CM-06 Templates
- CM-07 Internal inbox
- CM-08 Business integration
- CM-09 Webhooks and suppression
- CM-10 Optional relay
- CM-11 QA and hardening
- CM-12 Release package (this report)
All 12 roadmap stages are complete. 121 files changed, 10,437 insertions across the branch.

Files created:
- Backend (52 new files), all under backend/Garmetix.Api/Communication/ unless noted:
  EmailModels.cs / CommunicationModels.cs (backend/Garmetix.Domain/Generated/Models/Communication/),
  EmailCredentialProtector.cs, EmailQueueStateMachine.cs, EmailIdempotencyKeyBuilder.cs,
  EmailSendModels.cs, ITransactionalEmailProviderClient.cs, SmtpPresetCatalog.cs,
  BrevoApiEmailProviderClient.cs, SmtpEmailProviderClient.cs, LocalMasterOnlyEmailProviderClient.cs,
  EmailProviderClientFactory.cs, EmailProviderResolutionService.cs, EmailProviderTestService.cs,
  EmailQueueOptions.cs, EmailRetryBackoffCalculator.cs, EmailEnqueueService.cs,
  EmailQueueClaimService.cs, EmailRateLimitService.cs, EmailQueueItemProcessor.cs,
  EmailQueueWorker.cs, EmailProviderDtos.cs, EmailProviderEndpoints.cs,
  EmailTemplateRenderer.cs, EmailTemplateSeedService.cs, EmailTemplateDtos.cs,
  EmailTemplateEndpoints.cs, CommunicationRecipientResolver.cs,
  CommunicationAttachmentStorageService.cs, CommunicationDtos.cs, CommunicationEndpoints.cs,
  BusinessNotificationService.cs, BrevoWebhookOptions.cs, BrevoWebhookAuthenticator.cs,
  BrevoWebhookDtos.cs, BrevoWebhookEndpoints.cs, EmailSuppressionDtos.cs,
  EmailSuppressionEndpoints.cs, EmailQueueDtos.cs, EmailQueueEndpoints.cs
  + migration 20260718100000_AddCommunicationMailModule.cs
  + 15 test files under backend/Garmetix.Api.Tests/Communication/
- Frontend (26 new files): frontend/modular/apps/communication/ (a full new Nuxt app -
  nuxt.config.ts, package.json, app.vue, middleware/auth.global.ts,
  components/CommunicationMasterTable.vue, utils/communication-api.ts, 6 public assets,
  and pages/ index.vue, login.vue, access-denied.vue, providers.vue, templates.vue,
  mailbox.vue, queue.vue, suppression.vue)
- Deploy/docs: frontend/modular/deploy/postfix-relay/ (docker-compose.yml, .env.example,
  postfix/main.cf.template, healthcheck.sh, README.md), docs/communication-mail-architecture.md,
  docs/communication-mail-deployment.md, docs/communication-mail-testing.md,
  Garmetix-Mail-Com/ (the implementation pack itself, extracted), todo-communication-mail.md,
  roadmap-communication-mail.md (both copied to repo root per START-HERE.md)

Files modified:
- Backend: Auth/AccessPermissionMatrix.cs, Auth/GarmetixPolicies.cs (6 new Communication
  policies), Auth/UserManagementEndpoints.cs (+send-invitation-email), Billing/BillingEndpoints.cs
  (+send-email, extracted BuildInvoicePdfModelAsync), Database/DatabaseSchemaRepairService.cs
  (+RepairCommunicationStorageAsync), Garmetix.Api.csproj (+MailKit, +HtmlSanitizer),
  Inventory/StockReportDtos.cs + StockReportEndpoints.cs (+low-stock-alert/send-email),
  Payroll/PayrollEndpoints.cs (+send-email), Program.cs (all DI/endpoint/middleware
  registrations), Purchase/PurchaseEndpoints.cs (+send-email), appsettings.json
  (+Communication:EmailQueue, +Communication:BrevoWebhook)
- Infrastructure: Data/GarmetixDbContext.cs (16 new DbSets + OnModelCreating config)
- Frontend: config/apps.ts, config/routes.ts, config/version.ts, packages/shared-types/src/index.ts,
  packages/shared-ui/src/index.ts, packages/shared-ui/components/ModularAppShell.vue
  (all updated to register the new 'communication' app), and all 9 existing apps'
  nuxt.config.ts (added the NUXT_PUBLIC_GARMETIX_COMMUNICATION_URL appUrls key)

Database migrations:
- 20260718100000_AddCommunicationMailModule.cs (hand-written, for local/dev MigrateAsync path)
- DatabaseSchemaRepairService.RepairCommunicationStorageAsync (the mechanism that actually
  provisions tables on the SRP host's FreshBaseline bootstrap mode - wired into the existing
  RepairKnownSchemaDriftAsync startup chain)
- 16 new tables: CommunicationConversations, CommunicationMessages, CommunicationRecipients,
  CommunicationAttachments, CommunicationPreferences, EmailProviderConfigurations,
  EmailProviderCredentials, EmailTemplates, EmailTemplateVersions, EmailQueueItems,
  EmailRecipients, EmailAttachments, EmailDeliveryAttempts, EmailDeliveryEvents,
  EmailSuppressionEntries, EmailUsageCounters
- Model snapshot deliberately NOT hand-updated (disclosed in docs/communication-mail-architecture.md
  CM-02 section - PendingModelChangesWarning is suppressed by design in this repo, and the
  repair-service mechanism above is what actually matters in production)

API endpoints (all under /api/communication/* unless noted; base policy Communication,
mutation-tier policies CommunicationProviders/Templates/Queue/Suppression/Broadcast):
- Providers (/api/communication/providers): GET /, GET /catalog, GET /{id}, POST /,
  PUT /{id}, DELETE /{id}, POST /{id}/enable, POST /{id}/disable, POST /{id}/set-default,
  GET /{id}/credentials, PUT /{id}/credentials, POST /{id}/test-connection, POST /{id}/send-test
- Templates (/api/communication/templates): GET /, GET /{id}, POST /, PUT /{id},
  GET /{id}/versions/{versionId}, POST /{id}/versions, POST /{id}/versions/{versionId}/approve,
  POST /{id}/versions/{versionId}/restore, POST /{id}/preview, POST /{id}/test-send
- Messages: GET /mailbox, GET /mailbox/unread-count, GET /conversations/{id},
  POST /conversations, PUT /messages/{id}/draft, POST /messages/{id}/send,
  POST /conversations/{id}/reply, POST /conversations/{id}/archive|trash|restore,
  POST /messages/{id}/attachments, GET /attachments/{id}/download, GET+PUT /preferences
- Queue (/api/communication/queue): GET /, GET /{id}, POST /{id}/retry|cancel|reschedule|restore
- Suppression (/api/communication/suppression): GET /, POST /manual, POST /{id}/remove
- Webhook: POST /api/communication/webhooks/brevo (unauthenticated by JWT; own Basic
  Auth/header-token check, fails closed; rate-limited 120 req/min)
- Business integration (added to existing route groups, additive/opt-in, called after the
  source transaction already committed): POST /api/billing/sales/{id}/send-email,
  POST /api/payroll/payslips/{id}/send-email, POST /api/purchase/payments/{id}/send-email,
  POST /api/inventory/stock-reports/low-stock-alert/send-email,
  POST /api/access/users/{id}/send-invitation-email
Total: 45 new endpoints.

Frontend routes (all under the new 'communication' app, registered in config/routes.ts):
- / (dashboard), /providers, /templates, /mailbox, /queue, /suppression, /login, /access-denied
Scope note: the spec's inbox/sent/drafts/archive/trash/compose/conversation are all covered
functionally inside the single /mailbox page (folder tabs + compose modal + conversation
slideover) rather than as 7 separate URL routes - disclosed in the CM-07 commit/version notes.

Permissions:
- GarmetixPolicies.Communication (base - Admin/PowerUser/Accountant/RemoteAccountant/
  StoreManager/Salesman/HR/Payroll roles)
- GarmetixPolicies.CommunicationBroadcast, .CommunicationTemplates, .CommunicationProviders
  (Admin-only), .CommunicationQueue, .CommunicationSuppression (all Admin/PowerUser tier)
- All 6 registered in AccessPermissionMatrix.AllModules/ModuleRoles/Profiles and wired via
  AddMatrixPolicy in Program.cs, matching every other module's policy pattern in this repo

Tests executed:
- dotnet build backend/Garmetix.Api/Garmetix.Api.csproj -c Debug (every stage)
- dotnet test backend/Garmetix.Api.Tests/Garmetix.Api.Tests.csproj (every stage)
- node frontend/modular/scripts/validate-structure.mjs (every frontend-touching stage)
- npm --workspace @garmetix/communication-web run build (every frontend-touching stage,
  confirmed via compiled-bundle content, not just exit code)
- One rebuild of an existing app (books-web) after the CM-05 appUrls edits, to confirm no
  regression in an app this branch did not otherwise touch

Test results:
- Final full suite: 367 passed, 0 failed, 11 skipped, 378 total
  (backend/Garmetix.Api.Tests/Garmetix.Api.Tests.csproj)
- 11 skips are all [PostgresFact]-gated (require GARMETIX_TEST_POSTGRES, not set in this
  sandbox) - 3 pre-existing (PostgresConcurrencyTests) + 8 new this branch
  (EmailQueueConcurrencyTests x2, EmailQueueClaimConcurrencyTests x3,
  EmailProviderResolutionServiceTests x3)
- 0 regressions in any pre-existing test across all 11 stages
- Final dotnet build: 0 errors, 9 warnings (7 pre-existing + unrelated to this branch,
  2 pre-existing NU1902 AngleSharp advisory notices from the new HtmlSanitizer dependency,
  disclosed and accepted - see Security checks below)
- communication-web: clean production build, all 8 routes prerender with real content
  (confirmed via compiled-bundle text grep, not just HTTP status)

Security checks:
- Full manual review pass documented in docs/communication-mail-testing.md "Security review"
  section against every item in docs/communication-mail-security.md's checklist, with
  concrete file/method references (not just checked boxes): credential exposure, header
  injection, webhook forgery (8 automated tests), webhook rate/size limits, tenant/cross-
  tenant isolation, HR/Payroll confidentiality inheritance, ID enumeration, attachment abuse,
  SQL injection, log secret-leakage
- Automated: EmailCredentialProtectorTests (round-trip, tamper detection, masking, purpose-
  string isolation), CommunicationAttachmentStorageServiceTests (extension/size validation),
  BrevoWebhookAuthenticatorTests (fail-closed, Basic Auth, header token, CIDR allow-list),
  EmailTemplateRendererTests (HTML-escaping, script/handler sanitization)
- One real gap found and closed during CM-11: the Brevo webhook had a body-size cap but no
  request-rate limit - added a route-scoped ASP.NET Core fixed-window limiter (120 req/min)
  that never affects normal API traffic
- Known, accepted advisory: HtmlSanitizer 9.0.892 pins AngleSharp 0.17.1, which carries a
  disclosed NU1902 moderate-severity advisory. Overriding the pin to a patched AngleSharp
  breaks HtmlSanitizer's own compiled API surface across a major version jump (0.17.x -> 1.5.x
  is a breaking rewrite) - the advisory is accepted rather than silently forced, and is a
  transitive dependency of the sanitizer library, not of Communication & Mail's own code.

Manual configuration still required (before any real send):
- Create a Brevo account, verify a sending domain, add its SPF/DKIM DNS records
- Configure a Provider (BrevoApi or Smtp) at /communication/providers with real credentials,
  Test Connection, Send Test Email
- If webhook event tracking is wanted: configure a webhook in Brevo's dashboard, then set
  Communication:BrevoWebhook:Enabled=true plus real BasicAuthUsername/Password or
  HeaderName/HeaderToken in production configuration (fails closed otherwise)
- Point Communication:AttachmentStorage:StoragePath and the shared Gst:DataProtectionKeyPath
  at persistent volumes in any real deployment, and back up the Data Protection key ring
- Full sequence: docs/communication-mail-deployment.md "Manual configuration"

Pending items:
- No frontend "Email" buttons wired into the 5 existing business pages (Sales/Purchase/
  Payroll/Inventory/Admin) to call the new CM-08 send-email endpoints - the endpoints exist
  and work, but nothing in the existing UI calls them yet
- Accounting's daily-summary/system-alert seeded templates (CM-06) are not wired to any
  trigger event - no natural "the day just closed" hook was identified in this pass
- Department-based recipient resolution (the master prompt's "departments" audience) - this
  codebase has no Department entity, so CM-07 scoped this down to Role-based resolution only
- Postgres-gated integration/concurrency tests have not been run against a real database in
  this sandbox (written and reviewed, execution pending a real Postgres connection string)

Blocked items:
- None. Every stage in the original 12-stage roadmap was completed.

Known limitations:
- Live Brevo send/webhook round-trip not exercised (no Brevo account/credentials in this
  sandbox) - BrevoApiEmailProviderClient/SmtpEmailProviderClient are built directly from
  Brevo's documented API shape but have never actually called Brevo
- Live browser click-through not exercised (no Garmetix login credentials in this sandbox,
  consistent with every other module built in this project's history) - verification relied
  on clean production builds with compiled-bundle content checks instead
- The legacy IEmailSender/SmtpEmailSender/PasswordResetEmailService (password-reset flow)
  was deliberately left completely untouched - Communication & Mail is a parallel, additive
  system, not a replacement, per the CM-03 architecture decision
- docker compose config validation for the CM-10 Postfix relay could not be run (no Docker/
  PyYAML in this sandbox) - hand-reviewed for YAML correctness instead

Deployment steps:
See docs/communication-mail-deployment.md in full. Summary:
1. npm --prefix frontend/modular run deploy:srp:backup -- --stage=CM12CommunicationMailRelease
2. npm run modular:deploy:srp
3. npm run modular:deploy:srp -- --install-remote
4. Verify real content (not just HTTP status) on /communication/providers and
   /api/communication/queue (expect 401, route registered)
5. Configure a real Provider and test-send before relying on the module for anything

Rollback steps:
- Application: standard SRP release-symlink rollback to the previous release - safe, since
  every new table/policy/endpoint this branch adds is purely additive
- Database: no rollback required for a code rollback (old release code never queries the 16
  new tables); if a genuine schema rollback is ever needed, restore from the
  --stage=CM12CommunicationMailRelease backup rather than hand-writing down-migrations
- Live disable without a redeploy: Communication:EmailQueue:Enabled=false and/or disable
  every row in EmailProviderConfigurations from the Providers page

Recommended next stage:
- Wire "Email" buttons into the 5 existing business pages this stage's backend endpoints
  already support (Sales invoice, Purchase vendor payment, Payslip, Inventory low-stock
  digest, Admin user invitation)
- Get a real Brevo account provisioned and run the full manual acceptance checklist in
  docs/communication-mail-testing.md end to end
- Run the Postgres-gated integration/concurrency test suite against a real database
- When ready: merge Mail-Com into version7 and deploy, following
  docs/communication-mail-deployment.md - both explicitly Amit's call, not done as part of
  this pass
```
