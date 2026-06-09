# EF Core Setup

The API now uses `GarmetixDbContext`, `EfGarmetixRepository`, EF Core, and the PostgreSQL provider.

Create the first migration:

```bash
dotnet tool install --global dotnet-ef
dotnet ef migrations add InitialCreate --project backend/Garmetix.Infrastructure --startup-project backend/Garmetix.Api
dotnet ef database update --project backend/Garmetix.Infrastructure --startup-project backend/Garmetix.Api
```

The starter already includes:

- `InitialCreate`
- `AddUserLoginIndexes`
- `AddSetupAndLocalBusinessTimestamps`

`AddSetupAndLocalBusinessTimestamps` normalizes business `DateTime` columns to PostgreSQL `timestamp without time zone` and renames product category tables to match the new `DbSet` names. Review this migration before applying it to an existing production database with real data.

For local migration generation without Docker, the design-time factory uses:

```bash
GARMETIX_CONNECTION_STRING="Host=localhost;Port=5432;Database=garmetix;Username=garmetix;Password=garmetix_dev"
```

Current connection string:

```json
"Default": "Host=postgres;Port=5432;Database=garmetix;Username=garmetix;Password=garmetix_dev"
```

Recommended database rules:

- Global query filters are enabled for `BaseEntity.Deleted == false`.
- Indexes are added for tenant scope, invoice number, voucher number, barcode, and employee mobile.
- Store enums as strings only if reports need readability; otherwise keep integers for compact storage.
- Keep calculated properties such as `NetSalary`, `TotalEarnings`, and `BillableDays` out of database columns unless you need historical snapshots.
