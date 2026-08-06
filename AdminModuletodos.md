# Admin Module — Antigravity vs Current (`version7`) — Analysis, Roadmap & TODO

Date: 2026-08-06
Source analyzed: `C:\AIArea\Codex\admin` (an extracted snapshot of the Antigravity-AI-built admin app)
Compared against: `frontend/modular/apps/admin` on the current `version7` branch (HEAD `106d52c3`)

## 1. What `C:\AIArea\Codex\admin` actually is

It is **not** a separate product — it's a standalone folder holding one snapshot of `frontend/modular/apps/admin` as it existed on the `origin/Version6.A` branch, after commit `77aa2353` ("Admin UI fixes and cleanups"). That branch is real git history in this same repo (remote `amitdumka/GarmetixWebStarter`), authored under the label "Antigravity AI" (see `AntigravityAIChanges.md`, committed at `4ff8b72f` "Anitgaravity Internation Part").

Key facts about that branch, confirmed from git:

- It diverged from mainline at commit `79fafeed` (Stage 14C.2, Books voucher ledger parity) — a very old point.
- Mainline (`version7`) has **222 commits** since that divergence point (the entire GST module, Swalekha, Purchase Import, Tailoring, Stock Audit, Mail-Com, round-off corrections, etc.).
- `Version6.A` itself only has **9 commits** since the same point.
- Later commits on `Version6.A` (`de486ce8`, `9167765a`, `8c005653`) **deleted the entire `pos`, `hr`, and `books` modular apps** and stripped their build/deploy/route/validation wiring, per explicit note in `AntigravityAIChanges.md` section 12 ("Codebase Cleanup (POS, HR, Books Removal)").
- The branch also carries a pile of one-off Python DB-patching scripts at repo root (`fix_down.py`, `safe_migrate.py`, `patch_vue.py`, `inject_migration.py`, `justincase.md`, etc.) and an accidental full **duplicate copy of the entire admin app nested inside `frontend/modular/apps/books/admin/`** (from the `4ff8b72f` commit) — clear signs of exploratory, not production-hardened, work.
- A prior review pass already happened once: `AntiGarvity2CodeTODO.md` (repo root) is an earlier Codex-authored review of this exact branch, dated for an older path (`D:\AIArea\GarmetixWebStarter`). Its Priority 3–7 items (Inventory/Books/HR/POS) are now **moot** — every one of those modules has since been built out far beyond `Version6.A` on mainline. Its Priority 1–2 items (Admin/SaaS tenant infrastructure) were **never actioned** — that's exactly the gap this document now closes out in detail.

**Bottom line: `Version6.A` as a whole must never be merged or fast-forwarded into `version7`.** It is stale everywhere except one area it explored that mainline never built: a multi-tenant SaaS client/plan/license/subscription manager, plus two frontend forms (Setup CRUD, Client Onboarding wizard) that happen to target endpoints our backend already has. Those specific, isolated pieces are what's worth reviewing — nothing else from that branch.

## 2. Page-by-page inventory diff

| Page | In Antigravity snapshot | In current `version7` admin | Notes |
|---|---|---|---|
| `index.vue` (dashboard) | Old plain-Tailwind card grid, static links | Rebuilt on our `garmetix-dashboard-hero`/metric-card design system, live counts from `/companies`, `/stores`, `/access/users`, `/runtime-diagnostics`, `/license/status` | Current is strictly better; nothing to pull back. |
| `access.vue` (Users & Roles) | Full CRUD (add/edit/delete users), static role-matrix table | Full CRUD (add/edit/delete/deactivate users) on our design system, static role-matrix table | **Correction**: on a full content read (not just line-count diffing), current `access.vue` already has delete (`askDelete`/`performDelete`) and deactivate (`toggleStatus`) wired to real endpoints, plus a reset-password and send-invitation action Antigravity's doesn't have. No gap here — skip. |
| `setup.vue` (Company/Store Group/Store) | **620 lines** — full tabbed CRUD (Add/Edit/Delete Company, Store Group, Store; all fields incl. GSTIN/PAN/CIN/company type/store category) | **170 lines** — explicitly **read-only**: *"Add/edit remains in the legacy flow until owner-only write rules are reviewed"* | **Real, deliberate gap.** Backend already fully supports this: `MapCrud<Company>("/api/companies")`, `MapCrud<StoreGroup>("/api/store-groups")`, `MapCrud<Store>("/api/stores")` all exist today, gated on `GarmetixPolicies.CompanySetup`. Porting is a pure frontend job restyled to our design system — the only open question is the "owner-only write rules" review our own code comment flags, which should be resolved (or explicitly waived) before shipping write access here. |
| `client-onboarding.vue` | **564 lines** — full 6-step wizard (Owner details → Company → Address → Config/codes → Key people → Review & submit), posts to `/client-onboarding/submit` | **83 lines** — explicitly **read-only**: *"Read-only onboarding summary and setup options... Foundation stage"* | **Real gap, and the cheapest win in this whole review.** The backend (`backend/Garmetix.Api/Onboarding/ClientOnboardingEndpoints.cs` + `ClientOnboardingService.cs`, 559 lines) **already implements `GET /options`, `GET /summary`, and `POST /submit` in full** — our own backend has had a complete "spin up a new client/company/store/users in one go" capability for a while, with zero frontend ever wired to actually drive it. Antigravity's wizard UI is a ready-made frontend for an endpoint we already built and never exposed. |
| `saas.vue` | **589 lines** — full tabbed console: Clients, License Plans, License Tokens (generate offline tokens), Tenant Subscriptions (activate token → company), all backed by a real `SaaSManagerEndpoints.cs` | **Does not exist** | See §3 — this is a genuinely new capability area, not a port of something we half-have. |
| `sales.vue` | Admin-side Sales History browser (filter by store/date, view/print invoice) hitting `/billing/sales` | **Does not exist** | Overlaps heavily with capability we already have elsewhere (POS `history.vue`, Books Day Book, Main Sale Invoices register). Low incremental value — see §4. |
| `subscription.vue` | Single-tenant "Current Plan" + "Activate/Renew Offline" (token paste) view, hitting `/license/status` + `/license/activate` | **Does not exist as a page**, but `license-activation.vue` (identical in both snapshots) already covers this exact `/license/status`+`/license/activate` flow | Functionally redundant with our existing `license-activation.vue`. Only becomes non-redundant if we build the multi-tenant SaaS layer in §3, where "subscription" would mean something distinct from "license." |
| `backup-maintenance.vue`, `data-consistency.vue`, `google-drive-backup.vue`, `import-export.vue`, `license-activation.vue`, `message-logs.vue`, `production-readiness.vue`, `production-rehearsal.vue`, `production-support.vue`, `runtime-diagnostics.vue`, `system-health.vue`, `access-denied.vue`, `login.vue` | Identical | Identical | Byte-identical in both snapshots — Antigravity never touched these. Nothing to review. |
| `configuration.vue`, `dot-matrix-print.vue`, `gst-final-acceptance.vue`, `settings.vue` | **Does not exist** | Exist | Built on mainline after the branch point (env-var config, dot matrix printer setup, GST final-acceptance evidence, general settings). Nothing to pull — these are ours, newer. |

Shared infra (`AdminMasterTable.vue`, `AdminPlaceholder.vue`, `SupportDrillPage.vue`, `admin-api.ts`'s actual HTTP verbs, `auth.global.ts`) is either byte-identical or a pure internal refactor (current `admin-api.ts` factors out a shared `client()` helper — no behavior change). Nothing there to port either way.

## 3. The SaaS Manager (`saas.vue` + `SaaSManagerEndpoints.cs`) — new territory, not a port

This is the one piece that's a genuinely new feature, not something we already have in a plainer form. What it actually is: a console for someone running Garmetix **as a hosted product for multiple outside client businesses** to:

- maintain a `SaaSClient` register (the outside businesses paying for Garmetix),
- define `SaaSPlan`s (Basic/Pro/Ultimate — max companies/stores/users/modules-included),
- generate offline `SaaSToken`s per client+plan (a token string + expiry, meant to be sent via WhatsApp/email),
- have that client "activate" the token against one of their `Company` rows, which creates/refreshes a `TenantSubscription` row and (per the branch's `SaaSValidationService`) starts enforcing `MaxCompanies`/`MaxStores`/`MaxUsers` limits against that plan instead of a single global limit.

**None of this exists in `version7` today.** Our current `Licensing/` module (`LicenseActivationService.cs`, `LicenseEndpoints.cs`, `LicenseEnforcementMiddleware.cs`) is a **single-tenant** offline-license-token activator — one license per install, no concept of "client," "plan," or per-tenant limits. Multi-tenant SaaS management is architecturally a different, larger thing sitting on top of that.

**This should not be treated as a code-porting task.** It's a business/product decision first: does Amit actually want to operate Garmetix as a multi-tenant SaaS platform serving multiple outside businesses from one shared deployment, with plan-based limits and offline token activation as the licensing mechanic? If yes, the Antigravity code is a reasonable *starting sketch* (real endpoints, real CRUD, a working token generate/activate loop) but would need:

- A fresh look at whether `TenantId`/global-query-filter isolation is the right mechanism given this codebase already isolates by `Company`/`Store`/`WorkspaceScope` in several other modules (Swalekha's per-Owner isolation, multi-store scoping elsewhere) — risk of two competing isolation mechanisms if copied as-is.
- Re-verifying every backend model/endpoint against the *current* schema (222 commits have moved a lot of things since this was written) rather than copy-pasting.
- Restyling the frontend onto our current design system (it's still on the old plain-Tailwind look, like the rest of the snapshot).
- A decision on where "SaaS Owner/Developer" sits in our existing role model (`SuperAdmin`/`Admin`/`Owner`) — the branch invents its own separate concept of client ownership on top of it.

## 4. Lower-value pieces — recommend skipping

- **`sales.vue`**: an admin-side sales browser hitting `/billing/sales`. We already have equivalent or better coverage: POS's own sales `history.vue`, the Books Day Book, and Main's Sale Invoices register (Stage 14J). Adding a fourth sales-browsing surface in Admin adds maintenance cost without a capability gain. Skip unless Amit specifically wants sales visibility *inside the Admin app* for some workflow reason the other three don't cover.
- **`subscription.vue`** (single-tenant "current plan" view): functionally redundant with our existing `license-activation.vue`, which already shows status and accepts an offline token. Only worth reviving as a distinct page if §3 (multi-tenant SaaS) gets built, where "my subscription" (tenant-facing) and "license activation" (install-facing) would genuinely be two different concepts.

## 5. Recommended action items (ranked by effort vs. value)

1. **Client Onboarding wizard** — port Antigravity's 6-step wizard UI, restyled to our `garmetix-page-stack`/design system, against our *already-fully-built* `/api/client-onboarding/options`, `/summary`, `/submit` endpoints. No backend work. **Status: done, 2026-08-06.**
2. **Setup page — enable real CRUD** — port Antigravity's Company/Store Group/Store add/edit/delete modals, restyled to our design system, against our *already-fully-built* `MapCrud` endpoints. No backend work. **Status: done, 2026-08-06.**
3. **SaaS Manager (multi-tenant Clients/Plans/Tokens/Subscriptions)** — Amit greenlit this directly (no longer gated on a separate product-decision step). Built from scratch against the current schema — new `SaaSClient`/`SaaSPlan`/`SaaSToken`/`TenantSubscription` tables, `SaaSManagerEndpoints.cs`, opt-in quota enforcement (`SaaSValidationService`, only activates once a company has an actual subscription row — never blocks an unsubscribed install), plus `saas.vue` (SuperAdmin-only console) and `subscription.vue` (tenant self-service activate/renew). Deliberately **not** built: a global per-tenant data-isolation query filter (`TenantId` on every entity) — that stays a separate, much larger decision, exactly as flagged below. **Status: done, 2026-08-06** — see `CLAUDE.md`'s dated stage entry for full detail.
4. **Skip**: `sales.vue`, `subscription.vue`-as-a-standalone-license-view (superseded by the real subscription.vue built above), and `access.vue`'s Delete/Deactivate (already existed — see the correction in §2) — redundant with existing surfaces.

## 6. Hard "do not port" list (from the branch, regardless of which item above is chosen)

- Do not port the deletion of `pos`/`hr`/`books` modular apps or any of their build/route/validation wiring changes.
- Do not port the HR `nuxt.config.ts` hardcoded `https://srp.aadwikafashion.in/api/**` proxy or its SSL-verification bypass.
- Do not copy the duplicated `frontend/modular/apps/books/admin/` folder structure.
- Do not run any of the branch's root-level one-off Python DB-patch scripts (`fix_down.py`, `safe_migrate.py`, `patch_vue.py`, `inject_migration.py`, etc.) against any real database.
- Do not adopt a second, competing tenant-isolation mechanism without reconciling it against this codebase's existing `WorkspaceScope`/per-Owner isolation patterns already in production.

---
*Next step: confirm with Amit which of the ranked items in §5 to actually build, then track execution the normal way (stage entry in `CLAUDE.md` + `.claude/todo.md`), not by merging any part of the `Version6.A` branch directly.*
