# GST Return Module Notes

This is the first standalone GST Returns module for Garmetix.

## Scope in this version

- The module is intentionally separate from Billing and Purchase.
- Data is entered manually in the GST Returns screen.
- No invoice/purchase records are read automatically yet.
- No database table or migration is required for this version.

## Backend endpoints

All endpoints require the Accounting policy.

- `GET /api/gst-returns/schema-review`
- `GET /api/gst-returns/schema-review/excel`
- `POST /api/gst-returns/gstr1/preview`
- `POST /api/gst-returns/gstr1/json`
- `POST /api/gst-returns/gstr1/excel`
- `POST /api/gst-returns/gstr3b/preview`
- `POST /api/gst-returns/gstr3b/json`
- `POST /api/gst-returns/gstr3b/excel`

## Frontend page

Open:

```text
http://localhost:3000/gst-returns
```

The page supports:

- GSTR-1 manual entry
- GSTR-1 JSON download
- GSTR-1 Excel download
- GSTR-3B manual entry
- GSTR-3B JSON download
- GSTR-3B Excel download
- GSTIN/period validation preview
- GST state/POS, GST rate, tax split, negative amount, HSN, document count, and net ITC checks
- Portal/offline-utility readiness panel
- Downloadable schema-review checklist workbook

## Next GST work

- Test generated files with the live GST portal/offline utility for your GSTIN/return period.
- Add import from existing Billing/Purchase after manual module is accepted.
- Add saved draft GST return table.
- Add GSTR-1 sections for CDN, export, B2CL, amendments, and e-commerce operator details.
- Add GSTR-3B auto-calculation from GSTR-1/Purchase when user approves linking.


## Schema review and production validation

This version adds a schema-review endpoint and checklist workbook. The generated JSON uses the common GST offline key groups used by GSTR-1 and GSTR-3B exports, and the Excel workbook now includes a `Portal Review Checklist` sheet.

Before production filing, still test the generated file through the GST portal/offline utility and get accountant/CA approval. This is important because GSTN may change live portal validation rules or auto-populated GSTR-3B behavior.
