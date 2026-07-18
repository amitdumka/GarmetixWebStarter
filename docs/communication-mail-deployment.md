# Communication & Mail — Configuration, Deployment and Rollback

Branch `Mail-Com`, built stage by stage (CM-01–CM-12). This document replaces the generic
template shipped in the implementation pack with the concrete commands/paths this actual
build produced. **Not deployed as of this writing** — deployment requires Amit's explicit
approval per `START-HERE.md`.

## Manual configuration (do once, before any real send)

1. Create/verify a Brevo account and a verified transactional sending domain.
2. Add Brevo's account-specific SPF/DKIM DNS records (Brevo's dashboard shows the exact
   values once you add a sending domain — do not guess these). See
   `frontend/modular/deploy/postfix-relay/README.md`'s "DNS / deliverability setup" section
   for the SPF/DKIM/DMARC/Reply-To sequence — it applies whether or not you use the optional
   relay.
3. In Communication & Mail → Providers (`/communication/providers`, Admin-only), create either:
   - a **BrevoApi** provider (needs only an `ApiKey` credential), or
   - an **Smtp** provider with the **Brevo** preset (needs `SmtpUsername`/`SmtpPassword` —
     Brevo's SMTP key, not the account password).
   Enter credentials directly in that page — never in `appsettings.json` or an env var; they
   are encrypted at rest via `EmailCredentialProtector` (CM-03) the moment you save them.
4. Click **Test Connection**, then **Send Test Email** on the new provider before relying on
   it for anything real.
5. If you want delivery-event tracking (bounce/complaint/open/click), configure a webhook in
   Brevo's dashboard pointing at `https://<your-domain>/api/communication/webhooks/brevo`,
   with Basic Auth credentials or a custom header token — then set the matching
   `Communication:BrevoWebhook:Enabled=true` plus `BasicAuthUsername`/`BasicAuthPassword` or
   `HeaderName`/`HeaderToken` in production configuration (see CM-09's
   `BrevoWebhookOptions` — it fails closed with `Enabled=false` by default, so this step is
   required, not optional, for webhook events to ever be accepted).
6. `Communication:AttachmentStorage:StoragePath` and the reused GST Data Protection key path
   (`Gst:DataProtectionKeyPath`, shared by `EmailCredentialProtector` via a distinct purpose
   string — see CM-03) must both point at **persistent volumes**, not container-ephemeral
   paths, in any real deployment. Losing the Data Protection key ring makes every stored
   credential permanently undecryptable — back it up the same way any other production secret
   is backed up.

## Pre-deployment checklist

- [ ] Run `npm --prefix frontend/modular run deploy:srp:backup -- --stage=CM12CommunicationMailRelease`
      per `docs/database-stage-backup-protocol.md` before applying anything to a real database.
- [ ] `dotnet build backend/Garmetix.Api/Garmetix.Api.csproj -c Release` — 0 errors.
- [ ] `dotnet test backend/Garmetix.Api.Tests/Garmetix.Api.Tests.csproj` — full pass, note the
      Postgres-gated tests skip without `GARMETIX_TEST_POSTGRES` (expected in most CI setups).
- [ ] `node frontend/modular/scripts/validate-structure.mjs` — passes.
- [ ] `npm --prefix frontend/modular --workspace @garmetix/communication-web run build` — clean,
      all routes prerender.
- [ ] Confirm `Communication:EmailQueue:Enabled` is deliberately set for the target environment
      (default `true` — the worker is safe to run even with zero providers configured, since
      `LocalMasterOnlyEmailProviderClient` just dead-letters honestly, but confirm intent).
- [ ] Confirm no other running deploy currently owns `/opt/garmetix/backup/database/` on the
      target host (standard SRP deploy hygiene, unrelated to this module specifically).

## Deployment (commands only — do not run against SRP without Amit's explicit go-ahead)

This module ships entirely inside the existing `Garmetix.Api` project and the existing
modular frontend workspace — there is no separate service to stand up. The normal
whole-site deploy pipeline already covers it:

```bash
npm --prefix frontend/modular run deploy:srp:backup -- --stage=CM12CommunicationMailRelease
npm run modular:deploy:srp
npm run modular:deploy:srp -- --install-remote
```

After deploy, verify (matching this project's established "check real content, not just HTTP
status" discipline — see the 2026-07-07 flaky-build incident in `CLAUDE.md`):

```bash
curl -s -o /dev/null -w "%{http_code}\n" https://<domain>/communication/providers   # expect 200/redirect-to-login, real HTML
curl -s -o /dev/null -w "%{http_code}\n" https://<domain>/api/communication/queue    # expect 401 (route registered, auth-gated)
curl -s -o /dev/null -w "%{http_code}\n" https://<domain>/api/communication/webhooks/brevo -X POST  # expect 401 until BrevoWebhook is configured+enabled
```

Enable business integrations gradually: the five CM-08 `send-email` endpoints
(Sales/Purchase/Payroll/Inventory/Administration) are all opt-in actions with no frontend
button wired yet (documented gap) — nothing sends automatically after this deploy. Turn the
email queue worker on, confirm a manual test send via Providers works end to end, then decide
per-module whether/when to add UI buttons calling the new endpoints.

## Rollback

- **Application rollback**: standard SRP release-symlink rollback (`current` → previous
  release directory) — this module adds no new required configuration for the *previous*
  release to keep working, since every new table/policy/endpoint is additive.
- **Database rollback**: the 16 new Communication/Email tables (CM-02) are additive — rolling
  back the application code does not require rolling back the schema; the old release simply
  never queries the new tables. If a genuine schema rollback is ever needed, restore from the
  `--stage=CM12CommunicationMailRelease` backup taken above rather than hand-writing down-
  migrations.
- **Data preserved**: `EmailQueueItems`/`EmailDeliveryAttempts`/`EmailDeliveryEvents`/
  `EmailSuppressionEntries` are audit-relevant and must not be purged as part of any rollback.
- **Disabling the module live without a rollback**: set `Communication:EmailQueue:Enabled=false`
  (stops the worker from sending anything further) and/or disable every row in
  `EmailProviderConfigurations` from the Providers page — either is reversible with no deploy.

## Optional Postfix relay

See `frontend/modular/deploy/postfix-relay/README.md` for the full setup. Summary: isolated
`docker-compose.yml` in that directory, disabled by default, never referenced by any deploy
script in this repository, restricted to an internal-network sender allow-list, relays to
Brevo over mandatory TLS, and is entirely optional — most deployments send directly to Brevo's
API/SMTP without it.
