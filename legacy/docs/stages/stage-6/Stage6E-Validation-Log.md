# Stage 6E Validation Log

## Static checks completed

- Confirmed multi-item request DTO is preserved.
- Confirmed `NonGstGoodsItem` has GrossAmount, TaxableAmount, TaxRate, TaxAmount, CostRate and CostAmount fields.
- Confirmed runtime schema repair adds the same Non-GST item columns.
- Confirmed migration `20260610125000_EnhanceNonGstGoodsMemoReports.cs` exists.
- Confirmed print endpoint exists: `/api/non-gst-goods/documents/{id}/print`.
- Confirmed report DTO includes gross profit and current stock rows.
- Confirmed frontend has Sale Cash Memo, Purchase Memo, Reports, printable memo modal and current stock table.
- Confirmed app version updated to 2.4.0 / Stage 6E / GARMETIX-6E-20260610-240.
- Confirmed ZIP integrity check passed.

## Not run in sandbox

- `dotnet build` was not run because .NET SDK is not installed in this sandbox.
- Docker build was not run because Docker is unavailable in this sandbox.
- `npm run build` was not run because dependencies are not installed in this sandbox.

## Local validation commands

```bash
cd backend
dotnet build

cd ../frontend/garmetix-web
npm ci
npm run build

cd ../..
docker compose up --build
```
