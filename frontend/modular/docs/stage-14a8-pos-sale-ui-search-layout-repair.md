# Stage 14A.8 POS Sale UI Search And Layout Repair

Version: 6.0.8

## Purpose

Fix two cashier-facing issues on the modular POS New Sale screen:

- Product search should work when the barcode/product field contains a selected datalist label such as `barcode | item name | qty | mrp`.
- Payment and customer adjustment controls should not stay in a narrow right-side column on 14 inch laptops.

## Changes

- Added internal product input resolution so a selected datalist label maps back to the matching product barcode.
- Added barcode extraction from display labels before API lookup.
- Kept scanner and product-name search in one cashier field.
- Moved Payment and Customer adjustments below the item list.
- Promoted Totals next to the barcode/product entry area above the item list.

## Validation

Run:

```powershell
npm.cmd run modular:pos:cashier-workflow
npm.cmd run modular:pos:final-closure
npm.cmd --prefix frontend/modular run build:pos
```

## Deployment

No `.127` deployment is required for this checkpoint unless the user wants immediate live UI verification. This is checkpoint 2 after the last live deployment, so it follows the cadence in `docs/deployment-validation-cadence.md`.
