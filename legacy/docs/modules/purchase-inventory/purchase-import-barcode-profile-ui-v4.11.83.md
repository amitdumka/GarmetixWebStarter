# Garmetix v4.11.83 - Purchase Import Barcode + Vendor Profile UI

## Scope

This stage continues the supplier invoice import workflow from v4.11.82.

## Added

### 1. Auto barcode generation for imported supplier invoice lines

When a supplier invoice does not contain a barcode, the backend now generates a barcode before draft posting.

Format:

```text
YYMM + import-month block + item sequence
```

Examples for June 2026:

```text
26061001 = first import in the month, first item
26061002 = first import in the month, second item
26062001 = second import in the month, first item
```

The backend generates the barcode during:

- upload draft creation,
- draft save,
- manual Generate Missing Barcodes action,
- final post to Purchase Inward.

Legacy temporary import barcodes starting with `IMP-` are also replaced by the new YYMM format when the draft is saved/generated.

### 2. Supplier invoice learning profile management

New page:

```text
Purchase → Import Learning
/purchase/import-profiles
```

This page shows vendor-wise learning created from corrected supplier invoice drafts:

- ignored non-item row patterns,
- product alias mappings,
- successful posted draft count,
- last learned time.

Actions:

- reset product aliases,
- reset ignored row patterns,
- delete vendor profile.

### 3. Backend endpoints

```text
GET    /api/purchase-import/vendor-profiles
GET    /api/purchase-import/vendor-profiles/{id}
POST   /api/purchase-import/vendor-profiles/{id}/reset
DELETE /api/purchase-import/vendor-profiles/{id}
POST   /api/purchase-import/batches/{id}/generate-missing-barcodes
```

## Why this matters

Supplier invoices often do not contain store barcode values. Earlier, the import draft required manual barcode entry or generated temporary `IMP-...` barcodes. This stage makes missing barcodes predictable and store-friendly while keeping vendor-learning data manageable if the parser learns a wrong row or product mapping.

## Test checklist

1. Upload supplier invoice without barcodes.
2. Save draft.
3. Verify item barcodes look like `26061001`, `26061002`, etc.
4. Upload second invoice in same month.
5. Verify first item starts like `26062001`.
6. Mark a wrong header/footer row as Ignore and save.
7. Open Purchase → Import Learning.
8. Verify ignored pattern appears under the vendor profile.
9. Reset ignored rows and verify profile updates.
10. Post draft and confirm stock uses generated barcode.
