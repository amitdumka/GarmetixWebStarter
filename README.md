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
- Frontend: Nuxt 4 with Nuxt UI v4 dashboard and module screens.
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

Billing now has its own Nuxt route at `/billing`. The page lists sales invoices, opens a new invoice/POS form, shows printable receipts, and cancels invoices with stock reversal. It calls `POST /api/billing/sales`, saves invoice/items/payment in one backend transaction, and updates stock sold quantity. More details are in `backend/Billing-Notes.md`.

## Purchase Inward

Purchase now has its own Nuxt route at `/purchase`. The page lists purchase invoices and opens a purchase inward form. It calls `POST /api/purchase/inward`, creates or reuses a vendor, creates missing products when name/barcode are supplied, saves the purchase invoice/items, and increases store stock purchase quantity. More details are in `backend/Purchase-Notes.md`.

## Frontend Routing

The dashboard is now an overview page. Module navigation uses separate Nuxt pages, starting with `/setup`, `/billing`, `/purchase`, `/inventory`, `/vouchers`, `/petty-cash`, `/hr`, `/payroll`, and `/access`. Other module links have placeholder pages ready for their own list/form workflows.

## Nuxt UI Direction

The frontend is staged for Nuxt 4 + Nuxt UI v4. Stage 1 adds the Nuxt UI module, wraps the app in `UApp`, and uses Nuxt UI dashboard layout components for the shared shell. Stage 2 adds reusable CRUD building blocks for page headers, toolbars, empty states, delete confirmation, form slideovers, and toast feedback. The full staged migration list is in `Nuxt-UI-Implementation-Stages.md`.

## Company Setup

Company Setup now has its own Nuxt route at `/setup`. The page manages companies, store groups, and stores with list, add, edit, and soft-delete actions.

## Inventory

Inventory now has its own Nuxt route at `/inventory`. The page lists products with purchase quantity, sold quantity, current stock, and stock value. It also includes add, edit, and soft-delete actions for products.

## Vouchers

Vouchers now have their own Nuxt route at `/vouchers`. The page lists payment, receipt, and expense vouchers with add, edit, and soft-delete actions.

## Petty Cash

Petty Cash now has its own Nuxt route at `/petty-cash`. The page manages daily cash sheets with opening balance, sales, receipts, expenses, payments, deposits, and calculated cash in hand.

## HR

HR now has its own Nuxt route at `/hr`. The page lists employees and includes add, edit, and soft-delete actions using the generated employee model. It also manages daily attendance and monthly attendance. Monthly attendance can be generated from daily attendance rows, and the HR page auto-runs generation once when opened on the last day of a month.

## Payroll

Payroll now has its own Nuxt route at `/payroll`. The page manages salary structures and salary payments with list, add, edit, and soft-delete actions.

## Access

Access now has its own Nuxt route at `/access`. The page manages users, roles, user types, admin access, password reset, and company/store scope. It uses safe `/api/access/users` endpoints that hash passwords and do not return password hashes.

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

Date/time values are normalized before saving so PostgreSQL `timestamp without time zone` columns can accept values submitted from browsers and backend audit stamps.
