# Product Master Size Filters - v4.12.02

Stage: Stage 11D-87 Product Master Size Filters  
Build code: GARMETIX-11D87-20260627-4202

## Scope

This package adds size-wise filtering and display support to Product Master so products created by purchase-import size split can be searched and managed quickly.

## Backend changes

- `GET /api/inventory/product-master/paged` accepts a new optional query parameter:
  - `size`
- `GET /api/inventory/product-master/options` now returns detected `sizes`.
- Product master rows now return `sizeLabel`.
- Size is detected from product name, description, and style code without requiring a database schema change.

## Size detection examples

- `S.S SETH SAAB - RINGGIT - STD - 38` → `38`
- `SHIRT BLUE XL` → `XL`
- `FREE SIZE KURTA` → `Free Size`
- `STD` or `STANDARD` → `STD`

## Frontend changes

- Product Master list now shows a `Size` column.
- Product Master filters now include `All sizes` / specific size dropdown.
- Search placeholder now includes size.

## Notes

This is a derived-size implementation. It does not add a permanent database `Size` column yet. It is designed to support the current purchase-import flow where size is usually stored in product names after splitting imported invoice lines.
