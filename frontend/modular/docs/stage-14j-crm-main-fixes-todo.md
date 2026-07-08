# Stage 14J: CRM Digital Bills fixes + Main Sale Invoices page (in-progress todo)

Amit's request, verbatim scope (all counted as **one iteration** — do not deploy until 3 iterations of work have accumulated since the last deploy, and this is iteration 1 of that count):

1. ~~Split "Notes & GST" menu into two separate menus~~ **Already done and already live** — confirmed `frontend/modular/config/routes.ts` lines 119-130 already has `moduleKey: 'notes'`/`moduleLabel: 'Notes'` for Debit/Credit/Commercial Notes, separate from `moduleKey: 'gst'`/`moduleLabel: 'GST'`. This shipped in commit `b3266ba` and was included in the `--apps=hr,books` deploy that already went live (version 6.0.57, verified via `srp-public-acceptance.mjs --live`). **No further action needed on this item** — just confirming for the record since Amit re-asked.
2. CRM Digital Bills page: filter + pagination is inadequate, and the table visually clips at the end.
3. CRM Digital Bills page: deleted/invalid digital bills are showing when they shouldn't.
4. Main app (Back Office) "Sale Invoices" menu item renders effectively blank/stub — implement it properly.

## Research already done (don't re-research, just implement)

### Item 2+3: `frontend/modular/apps/crm/pages/marketing/digital-bills.vue` (386 lines)

**Current state:**
- Has a search box + a `pageSize` select + a **manual page-number `UInput`** (user has to type the page number themselves) + a "Search" button. No Prev/Next controls, no "page X of Y" display. This is what "pagination... clipped" likely means — it's not real pagination UI.
- Table wrapper: `<div class="garmetix-table-panel overflow-x-auto">` around a `min-w-[1200px]` table.
- **Root cause of "clipped at the end" (high-confidence, not yet visually verified in a browser):** `frontend/modular/packages/shared-ui/assets/modular-shell.css:231-233` defines:
  ```css
  .garmetix-table-panel {
    padding: 0;
  }
  ```
  and earlier at `modular-shell.css:224-229`:
  ```css
  .garmetix-table-panel,
  .garmetix-detail-panel {
    min-width: 0;
    overflow: hidden;
    padding: 16px;
  }
  ```
  `overflow: hidden` is a **shorthand** that sets both `overflow-x` and `overflow-y` to `hidden`. The page's own `overflow-x-auto` Tailwind class only sets `overflow-x: auto` (longhand) — depending on CSS load order this may or may not win over the shorthand for the x-axis, and it does **nothing** for the y-axis. If this panel ever ends up height-constrained by an ancestor flex/grid row (or if Nuxt UI's own components add a subtle height), the **last row(s) of the table can be silently clipped with no scrollbar to reveal them**. This is a shared class used by `.garmetix-table-panel` across every app (Books/HR/POS/CRM), so the safest fix is scoped to this rule, not a one-off page hack.
  - **Recommended fix (verify visually before shipping):** change `modular-shell.css:227` from `overflow: hidden;` to something that only clips horizontally where actually needed, e.g. split into `overflow-x: hidden; overflow-y: visible;` as the base, and let pages that need horizontal scroll opt in via their own `overflow-x-auto` wrapper (which digital-bills.vue and others already do on an *inner* div in some pages — check whether `overflow-x-auto` is on the `.garmetix-table-panel` element itself or a nested div consistently across pages before changing the shared rule, to avoid regressing horizontal scroll elsewhere).
  - **Must verify with the dev server + preview tools before considering this fixed** — this is a CSS cascade hypothesis, not a confirmed root cause from live rendering.
- **Pagination fix:** replace the manual page-number `UInput` with proper Prev/Next buttons + "Page X of Y" text, matching the pattern already used everywhere else this session (Books/HR pages, e.g. `frontend/modular/apps/hr/pages/attendance/salary-draft.vue`'s pagination block is a good copy-paste template). The backend `GET /api/digital-bills` (`backend/Garmetix.Api/Marketing/DigitalBillCrmEndpoints.cs:107-156`, `ListDigitalBillsAsync`) already returns `total`/`page`/`pageSize` via `DigitalBillListResponseDto` — the frontend already reads `total` via `readNumber(response.value, ['total'])` at digital-bills.vue:204, it just needs real Prev/Next UI wired to `filters.page`.

**Item 3 root cause (deleted-bills-still-showing):**
- `ListDigitalBillsAsync` (`DigitalBillCrmEndpoints.cs:107-156`) already filters `.Where(item => !item.Deleted)` on the `DigitalInvoice` entity itself (line 118) — so a digital-bill row that was itself soft-deleted is correctly excluded already.
- BUT: `DigitalInvoice` (`backend/Garmetix.Domain/Generated/Models/Marketing/DigitalBillCrm.cs:6-31`) only stores a raw `InvoiceId` (Guid) + `InvoiceType` (string, default `"Sale"`) with **no EF navigation property and no join** back to the actual sale invoice. There is no "delete" endpoint for a digital bill at all — only `/{id}/disable` (soft-disable, sets `IsActive = false`), which correctly shows as a "Disabled" badge and is a deliberate, visible state (not what should be hidden).
- **Working theory (needs confirmation before implementing):** when the underlying sale invoice itself gets deleted/cancelled (e.g. a POS return/exchange voids the original invoice, or an admin data-consistency operation removes it), its `DigitalInvoice` row has no cross-check and keeps showing as if the invoice still exists, with a live-looking "Active" badge and a Copy/Open link that now points to a dead invoice. This is what "deleted [items] shown" most likely refers to — **not** the disable flow, which already works.
- **Next step:** find the `Invoice` entity (used by `backend/Garmetix.Api/Billing/BillingEndpoints.cs`) and confirm it has a `Deleted` bool. If so, the fix is: in `ListDigitalBillsAsync`, either (a) add a `LEFT JOIN` against `db.Invoices` filtering out rows where the matched invoice is `Deleted` or missing, or (b) simpler/safer: when an invoice is deleted/cancelled elsewhere in the codebase, also soft-delete (`Deleted = true`) any `DigitalInvoice` rows referencing it at that point (search for where `Invoice.Deleted` gets set to `true` — likely in `BillingEndpoints.cs`'s cancel/delete/void handlers — and add a small follow-up step there). Prefer option (b) if it's a single well-contained call site, since it keeps `ListDigitalBillsAsync` simple and doesn't add a per-request join. Check both `InvoiceType` values used in practice (may only ever be `"Sale"` today per `GenerateForSaleAsync`/`GenerateForSaleByKeyAsync`/`GenerateForSaleFromRequestAsync` — confirm no other invoice types generate digital bills before scoping the join/cleanup to just the sales `Invoice` table).
- This is a **backend change** (new field usage or a new cleanup call site) — per the standing rule, this is small/additive and low-risk (mirrors existing soft-delete patterns already used throughout this codebase), so proceed without asking, consistent with how the Attendance Policies DELETE endpoint was added this session. But actually verify the `Invoice` entity's `Deleted` semantics and the invoice-cancel code path before writing anything — don't guess the exact call site.

### Item 4: `frontend/modular/apps/main/pages/billing/index.vue` (20 lines) + `frontend/modular/apps/main/components/MainReadOnlyTable.vue` (131 lines)

**Current state:** the page and component both exist (not missing files) and are functionally wired — `MainReadOnlyTable` fetches `billing/sales/recent?take=50` via `useMainApiClient()` and renders a bare list (title/subtitle/4 columns: Date, Bill, Paid, Balance). It is **not literally blank** in the source — but it is a minimal stub: no pagination, no filters, no per-row detail, no click-through, and critically:

- **The `main` app has not been redeployed since these files were added** (file mtime July 5; the last several deploys used `--apps=hr,books` only). **The live blank-page report may simply be a stale build** that predates this route/page/component existing at all. This must be checked first — it's entirely possible zero further code changes are needed here, just a `main` app redeploy, once the 3-iteration deploy gate is satisfied.
- Independent of the stale-build question, this page falls well short of the "global standard" established this session (modal/slideover forms, filters, pagination, detail view) and should be brought up to parity with POS's `history.vue`, which already does exactly this against the same underlying data:
  - Reuse `GET /api/billing/sales` (paged; supports `datePreset`/`status`/`from`/`to`/`page`/`pageSize`/`q` — see `backend/Garmetix.Api/Billing/BillingEndpoints.cs`'s `SearchSalesAsync`, already documented from earlier research this session) instead of the unpaged `billing/sales/recent?take=50`.
  - Add date-range + status filters, proper Prev/Next pagination, and a slideover/modal showing invoice detail per row (Back Office is read-only per the page's own description "Back Office sale invoice list and review route... Fast counter sale entry remains owned by the POS app" — so this stays view-only, no create/edit forms, just a proper readable list+detail).
  - Check `frontend/modular/apps/main/utils/main-api.ts` for what helpers already exist (`useMainApiClient`, `readNumber`/`readText`/`toRows` etc. per the import in `MainReadOnlyTable.vue:56`) before adding new ones.
  - Decide: extend `MainReadOnlyTable` generically to support pagination/filters (it's reused by other main-app pages — check `Glob frontend/modular/apps/main/pages/**/*.vue` for other `<MainReadOnlyTable ...>` usages first so a shared-component change doesn't regress them), or build `billing/index.vue` as its own bespoke page like POS's `history.vue` instead of going through the shared component. Bespoke is probably safer/faster given the shared component is used elsewhere with different assumptions.

## Sequencing for next session / auto-resume

1. Start dev server preview for `crm-web` and `main-web`, visually confirm the digital-bills clipping bug and get an accessibility snapshot of the current pagination controls before changing anything.
2. Fix digital-bills.vue pagination UI (Prev/Next + page count), matching the salary-draft.vue pattern.
3. Investigate and fix the CSS clipping (`modular-shell.css` `.garmetix-table-panel` `overflow: hidden` rule) — verify visually in preview after the change, on at least 2 different pages (digital-bills.vue + one Books/HR page) to confirm no regression.
4. Find the `Invoice` entity + invoice-delete/cancel code path in `BillingEndpoints.cs`, confirm the "deleted invoice's digital bill still shows" theory, implement the smallest-footprint fix.
5. Rebuild `billing/index.vue` (main app) as a proper filtered+paginated+detail read-only page against `GET /api/billing/sales`, matching POS `history.vue`'s pattern.
6. Build `crm-web` and `main-web`, run `node frontend/modular/scripts/validate-structure.mjs`, manual browser check via preview tools.
7. Commit each of steps 2-5 separately (per the commit-every-iteration rule) as they complete.
8. **Do not deploy** until this is iteration 3 since the last deploy (last deploy was the `--apps=hr,books` one that shipped version 6.0.57 — this whole batch is iteration 1; wait for 2 more iterations of *other* work, or confirm with Amit, before deploying) — per Amit's explicit instruction this turn.
9. Report back what's done + anything still pending, same format as the HR-stage closure report.

## Note on the earlier session's unresolved deploy-tooling bug

The Git-Bash `dotnet publish` doubled-drive-letter bug (`C:\c\AIArea\...`) is **still unresolved** — a first fix attempt (gating `dotnet_path_arg()`'s `wslpath` conversion on `WSL_DISTRO_NAME`/`WSL_INTEROP`) was committed but did **not** actually solve it; an isolated repro of the same `dotnet publish` command succeeded cleanly outside the full script, meaning something else in the full script's execution environment triggers it. The Attendance Policies `DELETE` endpoint (backend-only change from the HR stage) is therefore still not live on SRP. If a future deploy needs the API published, either debug this further or fall back to WSL for the API step specifically (accepting its separate missing-SSH-key limitation, i.e. build via WSL then manually scp/rsync just the `api/` folder using the Git-Bash-trusted SSH session).
