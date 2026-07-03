# Stage 11D-42 — Final PDF Print Acceptance Evidence — v4.11.57

## Goal

Convert the final PDF/print acceptance checklist from a browser-only checklist into a persistent, auditable handover record.

## Base

- Base ZIP: `v4.11.56 Stage 11D-41 Invoice Replacement Approval Audit`
- New ZIP: `v4.11.57 Stage 11D-42 Final PDF Print Acceptance Evidence`

## Implemented

### Backend

Updated `backend/Garmetix.Api/Production/PrintAcceptanceEndpoints.cs`.

Added:

- `GET /api/print-acceptance/evidence`
- `POST /api/print-acceptance/evidence`
- required scenario list in `GET /api/print-acceptance/status`
- recent evidence history in `GET /api/print-acceptance/status`

Evidence is saved into the existing `AuditLogEntries` table:

- `Module = Print Acceptance`
- `EntityName = PrintFinalAcceptanceEvidence`
- `Source = PrintFinalAcceptance`

No new migration/table is required.

### Frontend

Updated `frontend/garmetix-web/pages/print-final-acceptance/index.vue`.

The page now supports:

- sample source record readiness checks
- direct PDF links for sample documents
- required A4/A5 print evidence scenarios
- pass/fail/remarks per scenario
- operator/live-URL/browser/printer evidence fields
- backend evidence save
- saved evidence history

## Required evidence checklist

The backend requires these checks for full `Accepted` status:

1. Sale invoice A4
2. Sale invoice A5
3. Purchase inward A4
4. Purchase inward A5
5. Large invoice pagination
6. Amount box and totals
7. Footer and branding
8. Signature blocks
9. Page summary and final total

If any required check is pending or failed, evidence is still saved, but its status becomes `Needs Review`.

## Runtime testing still required

This stage provides the software workflow for real print testing. The actual print proof must be done on the live deployment:

1. Open `/print-final-acceptance` as owner/admin.
2. Confirm sample source records exist.
3. Open Sale A4 and A5 PDF from the page.
4. Open Purchase A4 and A5 PDF from the page.
5. Test one large invoice/purchase document for pagination.
6. Verify amount box, footer, signature, and page summary.
7. Fill operator/live URL/browser/printer fields.
8. Save backend evidence.
9. Confirm evidence history shows `Accepted`.
10. Confirm `/audit` includes module `Print Acceptance` evidence rows.

## Validation

Static validation script:

```bash
python3 scripts/validation/stage11d42-final-pdf-print-acceptance-evidence-check.py
```

## Notes

- No invoice replacement logic from Stage 11D-41 was removed.
- No payroll finalization logic from Stage 11D-40 was changed.
- No schema migration was added.
