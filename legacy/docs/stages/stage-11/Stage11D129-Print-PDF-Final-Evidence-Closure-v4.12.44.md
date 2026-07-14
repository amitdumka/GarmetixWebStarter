# Stage 11D-129 - Print/PDF Final Evidence Closure - v4.12.44

## Goal

Close the print/PDF acceptance work with a visible backend **Complete / Not Complete** status, exportable CSV evidence, and clear live-printer rules.

This stage does not change invoice, purchase, voucher, payroll, GST, or tailoring posting logic. It only strengthens final acceptance and handover evidence around existing print/PDF endpoints.

## Added backend endpoints

- `GET /api/print-acceptance/closure`
- `GET /api/print-acceptance/evidence.csv`

Existing endpoints remain:

- `GET /api/print-acceptance/status`
- `GET /api/print-acceptance/evidence`
- `POST /api/print-acceptance/evidence`

## Backend changes

`PrintAcceptanceEndpoints` now returns a closure block with:

- `status`: `Complete` or `Not Complete`
- core sale/purchase sample readiness
- latest evidence pass count
- last accepted evidence reference
- blocking issues
- final closeout checklist
- known limitations
- operator rules
- recommended next module candidates

The closure is **Complete** only when:

1. Required sale invoice and purchase inward samples exist.
2. A saved `Accepted` evidence record exists.
3. That evidence record has all required print checks marked Pass.

## CSV evidence export

The new CSV export includes:

- closure status
- sample document matrix
- print scenario matrix
- recent backend evidence records
- current blocking issues

This file is meant to be attached to production handover, backup, or acceptance records.

## Frontend changes

Page upgraded:

- **Print Final Acceptance**
- Route: `/print-final-acceptance`

Added:

- final closure status card
- core sample readiness metric
- latest evidence metric
- closure blocker alert
- final closeout checklist
- live-printer operator rules
- known limitations
- next module handoff guidance
- Export Evidence CSV button in header and save-evidence section

## Operator rules

- Do not mark Pass from a development URL when production acceptance is for the live hosted URL.
- Do not mark Pass if footer, signature, barcode, totals, or page summary are clipped or mismatched.
- Use real browser print preview or physical printer output before saving evidence.
- Re-run evidence after any print template change.

## Known limitations

- The system cannot physically verify printer margin, scale, paper tray, or ink/thermal quality.
- Large-invoice pagination still depends on the operator opening a real large invoice or inward sample.
- Optional samples such as vouchers, payslips, GST export, tailoring print and non-GST documents are displayed for wider QA but do not block the core sale/purchase print closure.

## Files changed

- `backend/Garmetix.Api/Production/PrintAcceptanceEndpoints.cs`
- `frontend/garmetix-web/pages/print-final-acceptance/index.vue`
- `backend/Garmetix.Api/AppInfo/AppInfoEndpoints.cs`
- `frontend/garmetix-web/utils/appVersion.ts`
- `backend/Garmetix.Api/Garmetix.Api.csproj`
- `scripts/linux/create-database-backup-now.sh`
- `scripts/linux/smoke-test.sh`

## Validation

Run static validation:

```bash
python3 scripts/validation/stage11d129-print-pdf-final-evidence-closure-check.py
```

Run full app build on host:

```bash
docker compose up --build
```

## Manual QA

1. Open **Print Final Acceptance**.
2. Confirm final closure card loads.
3. Confirm status is **Not Complete** until required samples and accepted evidence exist.
4. Open Sale A4 and A5 PDF from the scenario list.
5. Open Purchase A4 and A5 PDF from the scenario list.
6. Verify large invoice pagination, footer, signature, amount box and page summary.
7. Mark all required checks Pass.
8. Save Print Evidence.
9. Refresh page and confirm closure becomes **Complete**.
10. Export Evidence CSV and verify it includes closure status, document rows, scenario rows and evidence rows.

## Version

- Version: `4.12.44`
- Stage: `Stage 11D-129 Print PDF Final Evidence Closure`
- Build code: `GARMETIX-11D129-20260701-4244`
