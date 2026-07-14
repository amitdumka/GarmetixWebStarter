# Stage 14G Books GST Full Legacy Parity

Version: 6.0.53
Branch: version6

## Closure Summary

Stage 14C.5 closed export/financial-year-lock parity for the Books GST lane, but that was narrower than "the GST menu." This stage ports every page under legacy's GST navigation group (`frontend/legacy/garmetix-web/components/AppShell.vue:203-212`) to modular with no feature left behind: the full manual GSTR-1/GSTR-3B builder with draft lifecycle and CA review/share, the Accounting/GST post-import validation report, and the GST Final Acceptance checklist. All backend support already existed (`GstReturns/`, `Gstin/`, `Validation/PostImportLiveValidationEndpoints.cs`, `Backup/BackupEndpoints.cs`, `Production/EmailDeliveryDiagnosticsEndpoints.cs`, `Validation/DataConsistencyEndpoints.cs`) — this is a frontend-only port with zero backend/DB changes.

## Completed Parts

- 14G.1 Ported `useGstReviewContact` (CA contact + share log, localStorage-backed) to `apps/books/composables/`.
- 14G.2 Rewrote `gst-returns.vue` into the full builder: header form, GSTR-1 dynamic B2B/B2C/HSN/document/nil rows, GSTR-3B supplies/ITC/interstate/inward/interest sections, Preview, Load From Books, Save/Update/Delete/Mark-Filed draft lifecycle, ad-hoc JSON/Excel export, draft audit trail, GST accounting-posting bridge (typed `POST GST ACCOUNTING` confirmation phrase - stronger than legacy's plain confirm, matching the modular Books convention for ledger-posting actions), and the Review & Send to CA modal (email/WhatsApp share).
- 14G.3 Added the CA share modal to `gst-reports.vue` (`gst-returns/reports/send-review`).
- 14G.4 Added `accounting-gst-validation.vue` (Books app), registered as a new `gst` module route, porting the post-import validation report field-for-field.
- 14G.5 Added `gst-final-acceptance.vue` (Admin app) for the route that was previously registered with no page file — 8-item localStorage checklist, live HSN/tax/invoice-register/data-consistency/email/backup status.
- 14G.6 Extended `books-accounting-contract-check.mjs`, `books-browser-acceptance.mjs`, `admin-browser-acceptance.mjs` with the new pages/tokens, and added the non-mutating `books-stage14g-closure.mjs` gate.

## Validation

```powershell
npm.cmd run modular:books:accounting-contract
npm.cmd run modular:books:browser-acceptance
npm.cmd run modular:admin:browser-acceptance
npm.cmd run modular:books:stage14c5-closure
npm.cmd run modular:books:stage14g-closure
npm.cmd run modular:validate -- --skip-builds
```

## Discretionary differences from legacy (not omissions)

- Row add/remove in the GSTR-1 builder allows any row count including zero (legacy always kept at least one row per section via a reset-in-place quirk); serial numbers are recomputed at save/preview time instead of at add-time, so ordering stays correct regardless of add/remove.
- GST accounting-posting actions require a typed `POST GST ACCOUNTING` confirmation phrase; legacy only used a plain `confirm()` dialog. This matches the stronger guarded-write convention already used elsewhere in modular Books (financial-year locks, bank transactions).

## Remaining Risks

- Live acceptance of GSTR-1/3B filing, accounting posting and CA email/WhatsApp share still needs a real accountant/admin-capable token and a manual browser pass (`GARMETIX_BOOKS_GST_FULL_PARITY_MANUAL_ACCEPTANCE=YES`).
- Composition scheme, GSTR-2A/2B reconciliation, e-way bill and real e-invoice/IRN generation were never built in legacy either and remain out of scope here.
- `/non-gst-goods` (Off Book group) and `/financial-year-closeout` (Accounting group) are different legacy nav groups, not "under GST," and were intentionally not touched in this stage.

## Next Stage

Books GST menu parity is complete. Next priority depends on Amit: Assistant/MCP wrapper (14F.7), the cross-module live-evidence backlog, or the AntiGravity port review.
