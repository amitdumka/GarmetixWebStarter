# Stage 11D-19 Production Data Validation Toolkit - v4.11.34

This stage adds a server-side validation toolkit for the final production import rehearsal.

## What it validates

- Aadwika Fashion / Smart Menswear company-store context
- Vendor, brand, category, sub-category, product, stock master counts
- Purchase invoices and purchase invoice item totals
- Inward number format: `SM/YYYYMM/INW/0001`
- Duplicate inward numbers
- Discount and purchase item amount sanity
- Purchase invoice totals vs item totals/freight/round-off
- Zero or negative stock rows
- Product master quality: missing HSN/category/subcategory/stock
- Vendor ledger vs purchase invoice totals
- Active employees, attendance shifts, shift rules, punches, daily attendance rows
- Punch local-time vs UTC issue
- Punch-to-daily-attendance sync
- Active employees without direct employee-specific shift rule

## Run on server

```bash
cd /opt/garmetix/current
chmod +x scripts/production/validate-production-data.sh
./scripts/production/validate-production-data.sh
```

Optional custom company/store:

```bash
COMPANY_NAME="Aadwika Fashion" STORE_NAME="Smart Menswear" ./scripts/production/validate-production-data.sh
```

Reports are saved in:

```text
/opt/garmetix/current/reports/production-validation/
```

## Action rules

- Any `FAIL` must be fixed before final import acceptance.
- Any `WARN` should be reviewed. Some warnings may be acceptable during a dry run.
- Rerun validation after purchase v7 import and after employee/attendance import.

Build: `GARMETIX-11D19-20260625-4134`
