# Stage 4A Validation Log

## Performed here

- Unzipped Stage 3D package and applied Stage 4A backend changes.
- Added `DocumentSequence` model and `DocumentSequences` DbSet.
- Added centralized `DocumentNumberService` and transaction-scoped advisory lock helper.
- Replaced active billing/purchase/commercial count-based document numbering paths.
- Added stock-key locks to product master, sale, return, exchange, purchase inward, partial purchase return, and full purchase cancel stock mutations.
- Added runtime schema repair SQL for `DocumentSequences`.
- Performed script-based brace-balance checks across backend C# files.
- Confirmed there are no remaining active references to the old billing/purchase count-based number helper calls.

## Could not perform here

- `dotnet build` / `dotnet publish`: .NET SDK is not installed in this sandbox.
- Docker build: Docker is not installed in this sandbox.
- Database integration/concurrency test: requires PostgreSQL runtime and application build.

## Required local validation

Run after extracting:

```bash
cd backend
dotnet build

cd ..
docker compose up --build
```

Recommended manual concurrency checks:

1. Open two billing browsers on the same store and sell the same low-stock barcode at the same time.
2. Confirm only one sale succeeds if quantity is insufficient.
3. Create two invoices at the same time and confirm invoice numbers are unique and sequential.
4. Run purchase inward and purchase return for the same barcode concurrently and confirm stock remains consistent.
