# Stage 11D-20 Inventory Advanced Filters - v4.11.35

Build: `GARMETIX-11D20-20260625-4135`

## Scope

This stage improves the Inventory product master listing so the page can be used for production stock audit after purchase import.

## Added filters

- Brand
- Vendor / supplier
- Base color
- Age bucket
- Stock mode: in-stock / out-of-stock / all
- Stock health: low stock, dead stock, high value, missing HSN, missing category, missing brand, missing vendor, missing color
- Minimum stock
- Maximum stock

## Backend

Updated `GET /api/inventory/product-master/paged` with new query parameters:

```http
GET /api/inventory/product-master/paged?page=1&pageSize=50&stockMode=in-stock&brand=RoyalWood&vendorId=<id>&color=Blue&health=low-stock&minStock=1&maxStock=2
```

Updated `GET /api/inventory/product-master/options` to return distinct `brands` and `baseColors`.

## Frontend

Updated `Inventory → Product Master` to show the advanced filters above while keeping pagination server-side.
