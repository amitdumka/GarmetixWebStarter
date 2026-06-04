# Garmetix Web Starter

This starter converts the supplied MAUI model ZIP into a web-first structure:

```text
backend/
  Garmetix.Domain/          # Sanitized model and enum files from Models.zip
  Garmetix.Infrastructure/  # Repository/storage layer
  Garmetix.Api/             # ASP.NET Core API
frontend/
  garmetix-web/             # Nuxt/Vue app shell
docker-compose.yml
```

## Suggested Architecture

- Backend: ASP.NET Core API.
- Domain: your existing C# model and enum classes, with MAUI-only form attributes removed.
- Database: PostgreSQL with EF Core/Npgsql.
- Frontend: Nuxt/Vue dashboard and module screens.
- Deployment: Docker Compose for Linux or Mac mini.

## Covered Modules

- Company, StoreGroup, Store
- Billing and purchase invoices
- Inventory, products, stock, brands, UOM, tax
- Accounting vouchers, ledgers, bank, petty cash
- HR, attendance, salary structure, salary payment, payslip
- User access and role-wise permissions
- Import/export placeholder for CSV/Excel workflows

## Run Backend Locally

```bash
cd backend/Garmetix.Api
dotnet run
```

API starts on the ASP.NET default development URL. The Nuxt config expects `http://localhost:5080/api`; set `ASPNETCORE_URLS=http://localhost:5080` if needed.

## Database

The backend now uses `GarmetixDbContext` and `EfGarmetixRepository`.

Create/update the PostgreSQL database:

```bash
dotnet ef database update --project backend/Garmetix.Infrastructure --startup-project backend/Garmetix.Api
```

The initial migration is already generated under:

```text
backend/Garmetix.Infrastructure/Data/Migrations
```

## Authentication

The API uses JWT bearer authentication and role policies for each module. Create the first admin through the Nuxt login screen's `First Admin` tab or call:

```bash
curl -X POST http://localhost:5080/api/auth/bootstrap-admin -H "Content-Type: application/json" -d "{\"name\":\"Garmetix Admin\",\"userName\":\"admin\",\"email\":\"admin@garmetix.local\",\"password\":\"change-me\"}"
```

More details are in `backend/Auth-Access-Notes.md`.

## First Run Setup

After login, the Nuxt dashboard checks `/api/setup/status`. If company/store defaults are missing, use the `Quick Setup` panel to create the first company, store group, store, product category, product subcategory, and GST tax.

## Billing POS

The Nuxt dashboard includes a compact POS panel. It calls `POST /api/billing/sales`, saves invoice/items/payment in one backend transaction, and updates stock sold quantity. More details are in `backend/Billing-Notes.md`.

## Purchase Inward

The dashboard also includes a purchase inward panel. It calls `POST /api/purchase/inward`, creates or reuses a vendor, creates missing products when name/barcode are supplied, saves the purchase invoice/items, and increases store stock purchase quantity. More details are in `backend/Purchase-Notes.md`.

## Run Frontend Locally

```bash
cd frontend/garmetix-web
npm install
npm run dev
```

## Run With Docker

```bash
docker compose up --build
```

Then open:

- Web: `http://localhost:3000`
- API: `http://localhost:5080`

## Migration Notes

EF Core setup details are in `backend/EF-Core-Next-Step.md`.

The model conversion made one bridge change: `AppUser` now implements `IEntity` because it already has an `Id` and should participate in the same API/storage pattern.
