# Stage 5F — AF/SS Default Seeder Module

## Purpose

This stage adds a separate AF/SS seeder module to the web application. It takes the two MAUI seed files (`Seeder.cs` and `seeder2.cs`), identifies the shared default data, preserves the extra Samrat Menswear profile from `seeder2.cs`, and adapts the seed data to the current web models and enum changes.

## Seeder comparison

### Common parts from both files

- Aadwika Fashion AF and AFS company/store/store-group profiles.
- Default banks:
  - State Bank Of India
  - ICICI Bank
  - HDFC Bank
  - Bank of Baroda
  - Kotka Bank
  - Punjab National Bank
- Default tax rows:
  - GST 5%, 12%, 18%
  - IGST 5%, 12%, 18%
  - CGST 2.5%
  - SGST 2.5%
- Default transactions:
  - Petty Cash Expenses
  - Home Expenses
  - Store Expenses
  - Dan & Donations
  - Snacks & Breakfast Expenses
  - Cash In
  - Cash Out
- Default ledger groups and ledgers.
- Owner, store manager, accountant employees.
- Default Manager salesman.
- Default users.

### Extra in `Seeder.cs`

- Old sample product and stock seed method.
- Synchronous `SaveChanges` flow with count/saved tracking.
- MAUI notification calls.

### Extra in `seeder2.cs`

- Async seeding flow and better exception messages.
- Samrat Menswear profile:
  - profile code `SM`
  - store group code `MBO-SM`
  - store code `SM01`
- Safer name splitting using `Skip(1).FirstOrDefault()`.
- Corrected `SGST 2.5%` label.

## Web implementation

New backend module:

- `backend/Garmetix.Api/Seeds/AfssSeederDtos.cs`
- `backend/Garmetix.Api/Seeds/AfssDefaultSeederService.cs`
- `backend/Garmetix.Api/Seeds/AfssSeederEndpoints.cs`

New endpoints:

- `GET /api/afss-seeder/options`
- `GET /api/afss-seeder/analysis`
- `POST /api/afss-seeder/seed`

New frontend page:

- `frontend/garmetix-web/pages/af-ss/index.vue`

New Admin menu item:

- `Admin → AF/SS`

## Model and enum changes accommodated

The old seeders were based on the MAUI model and older enum layout. Stage 5F updates the data to match the current web model:

- Uses `Garmetix.Core.Models.Inventory.ProductCategory` as the inventory model, not the obsolete enum.
- Seeds `ProductGroup` and current `ProductType` values:
  - `ProductType.Fabric`
  - `ProductType.Readymade`
  - `ProductType.Shoes`
  - `ProductGroup.Shirting`
  - `ProductGroup.Suiting`
  - `ProductGroup.KurtaPajama`
  - `ProductGroup.Nagra`
- Seeds product-level `HSNCode`.
- Seeds stock-level `HSNCode`, `TaxId`, `StockType.Opening`, `StoreGroupId`, and `StoreId`.
- Creates `StockMovement` opening ledger rows for seeded stock.
- Creates `ProductDetail` rows with style code, base color, brand, and vendor.
- Uses PBKDF2 password hashing through `PasswordHasher.Hash` instead of old plain-text password storage.
- Uses code-prefixed user names (`AFAdmin`, `AFOwner`, `AFStoreManager`, etc.) because the web app stores users in one database and `UserName` is globally unique.

## Seed profiles

Available profiles:

- `AF` — Aadwika Fashion / Amit Kumar
- `AFS` — Aadwika Fashion / Shalini Kumari
- `SM` — Samrat Menswear

The page asks which existing company should be seeded. It then creates or reuses the first store group/store for that company using the selected profile defaults.

## Safety behavior

- The seeder is idempotent.
- Existing rows are reused by code/name/barcode/mobile where possible.
- It does not delete existing data.
- It does not auto-create old MAUI per-company SQLite databases.
- It does not run MAUI notifications.
- It requires Admin authorization.
- The UI requires confirmation before applying the seed.
