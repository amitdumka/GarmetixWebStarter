# Stage 11D-30 — Production Import + GST Validation

Version: **4.11.45**

Purpose: before final data acceptance, verify Aadwika/Smart Menswear purchase import, sales numbering, GSTIN-based CGST/SGST/IGST split, invoice totals, inward numbers, and zero-stock mistakes.

Run on server:

```bash
cd /opt/garmetix/current
chmod +x scripts/production/*.sh
./scripts/production/stage11d30-import-validation-and-gst.sh
```

The report is saved under:

```txt
reports/stage11d30-import-gst-validation/
```

Fix all `FAIL` rows before running payroll validation.
