# Developer Validation Checklist

Run these commands on your Windows/Linux/Mac development machine after extracting the ZIP.

## 1. Backend restore/build

```bash
dotnet restore backend/Garmetix.Api/Garmetix.Api.csproj
dotnet build backend/Garmetix.Api/Garmetix.Api.csproj
```

## 2. Database migration

```bash
dotnet ef database update --project backend/Garmetix.Infrastructure --startup-project backend/Garmetix.Api
```

## 3. Frontend install/build

```bash
cd frontend/garmetix-web
npm ci
npm run build
```

## 4. Docker clean install

```bash
docker compose down -v
docker compose up --build
```

## 5. Manual functional tests

- First admin / login / forgot reset link / change password
- Company / StoreGroup / Store permission selector
- Billing invoice, print/PDF, cancel, return, exchange
- Purchase inward, print/PDF, cancel, vendor payment voucher
- GST Returns manual entry, Load From Books, draft, audit, JSON, Excel
- GSTIN verification using configured provider credentials
- Backup/restore local and Google Drive
- Audit filters, field details, and CSV export
- Permissions for Admin, Owner, Manager, Cashier, Inventory, HR, Payroll, Accountant
