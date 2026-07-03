# Stage 11D-154 Stock Operation Product Autocomplete - v4.12.69

## Goal

Make Stock Operations faster and safer for store staff by replacing long product dropdowns with searchable autocomplete inputs on every stock-operation tab.

## Changed page

- `frontend/garmetix-web/pages/stock-operations/index.vue`
- Route: `/stock-operations`

## Implemented

- Adjustment tab stock item now uses `USelectMenu` searchable autocomplete.
- Transfer tab source stock now uses `USelectMenu` searchable autocomplete.
- Physical Count tab stock item now uses `USelectMenu` searchable autocomplete.
- Write-off tab stock item now uses `USelectMenu` searchable autocomplete.
- Search placeholder guides staff to type product name, barcode, HSN or store.
- Product options carry extra searchable metadata: product name, barcode, HSN, store, label and unit.
- Selected stock hint shows barcode, store, available quantity and MRP below the picker.

## Not changed

- Posting endpoints are unchanged.
- Stock movement/accounting logic is unchanged.
- Destination store remains a normal store selector because it is usually a short list.

## Version

- Version: `4.12.69`
- Stage: `Stage 11D-154 Stock Operation Product Autocomplete`
- Build code: `GARMETIX-11D154-20260703-4269`
