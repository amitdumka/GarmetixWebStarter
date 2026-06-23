# Stage 3A Product Master Implementation Notes

Date: 2026-06-08
Package target: `Garmetix-Stage3A-ProductMasterUI-v0.5.zip`

## User-provided context applied

The Stage 3A work uses the updated inventory/product model direction supplied in chat:

- `GarmentCategory` remains only as an obsolete legacy enum and should be replaced by `ProductGroup` in new code.
- The old enum `ProductCategory` remains only as obsolete legacy code; production grouping should use the `ProductCategory` model plus `ProductGroup` and `ProductType`.
- `ProductType` now uses the corrected `Readymade` spelling and includes `Tailoring`, `Trims`, `PromoItems`, and `Shoes`.
- New `ProductGroup` enum added for garment-specific merchandising groups such as Shirting, Suiting, Sherwani, Kurta, Jodhpuri, Shoes, Nagra, Accessories, etc.
- Product master now includes `HSNCode`, `ProductGroup`, `ProductType`, category, subcategory, tax, unit, style, color, brand, vendor, and stock defaults.
- Stock now includes `StockType`, while ProductDetail captures barcode-level style/color/brand/vendor metadata.

## Backward-compatibility decision

The new canonical product type spelling is `Readymade`. A deprecated alias `Readmade = Readymade` is kept so older source code and old serialized enum references do not fail immediately.

`ProductType` now uses explicit numeric values because these enum values are stored as integers in the database. This reduces accidental data drift when new enum members are added.

## Backend changes

Added/updated:

- `ProductGroup` enum.
- `StockType` enum.
- Obsolete markers on legacy `GarmentCategory` and enum `ProductCategory`.
- `Product.ProductGroup`.
- `Stock.StockType`.
- `ProductCategory.ProductGroup` and `ProductCategory.IsActive`.
- `ProductSubCategory.CategoryId`.
- `ProductAttribute`, `ProductAttributeValue`, `ProductTag`, and `ProductTagMapping` model classes for future attribute/tag expansion.
- `ProductDetails` DbSet.
- Product attribute/tag DbSets and composite keys.
- Runtime schema repair for the new Stage 3A product fields/tables.
- Idempotent EF migration `20260608093000_AddStage3AProductMaster.cs` for fresh/auto-migrated databases.
- CRUD routes for product categories, subcategories, product details, brands, and taxes.
- New product master API group: `/api/inventory/product-master`.

Product master API endpoints:

- `GET /api/inventory/product-master`
- `GET /api/inventory/product-master/options`
- `POST /api/inventory/product-master`
- `PUT /api/inventory/product-master/{id}`

The new product-master create/update endpoint keeps product, stock, and product detail updates together instead of making the UI coordinate multiple generic CRUD saves.

## Frontend changes

Reworked `frontend/garmetix-web/pages/inventory/index.vue` into Stage 3A Product Master UI.

The Product Master form now captures:

- Product name
- Barcode
- Description
- Product group
- Product type
- Stock type
- Category
- Subcategory
- Unit
- MRP
- Cost price
- Opening quantity on create
- Current stock on edit
- Tax
- Tax type
- HSN code
- Brand
- Vendor/supplier
- Style code
- Base color

The inventory table now shows HSN, category, group/type, MRP, cost, stock, and stock value.

## Remaining Stage 3A follow-up

Recommended next step after installing this package:

1. Run backend build and migration check in your Windows/Docker dev environment.
2. Start frontend and verify Product Master create/edit forms.
3. Add category/subcategory management modal/page if you want users to create product categories without going to raw CRUD endpoints.
4. Add Product image/SKU variant support later; the current model does not yet include image or full SKU variant table.

## Validation note

This sandbox does not have the .NET SDK installed, and `node_modules` is not present in the uploaded package. I performed static source updates and packaging, but final `dotnet build`, `npm install`, and `npm run build` must be run in your development environment.
