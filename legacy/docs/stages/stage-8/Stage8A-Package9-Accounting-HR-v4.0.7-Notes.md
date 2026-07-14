# Stage 8A Package 9 - v4.0.7

Date: 2026-06-14

## Delivered

- Replaced direct Party and Bank Account entity binding with dedicated API requests.
- Kept Party and Bank Account ledger identifiers server-owned and synchronized by the accounting service.
- Removed the visible Party selector from bank transactions; the selected contra ledger now drives internal Party linkage.
- Moved accounting create/edit workflows into responsive wide modal workspaces.
- Standardized Accounting and HR registers with shared loading, retryable error, empty, search, and responsive table states.
- Preserved local calendar dates across employee, attendance, ledger, bank account, bank transaction, cheque, and vendor-bank workflows.
- Replaced the invalid empty HR store option with an explicit all-stores value.
- Marked Accounting and HR reviewed in the persistent UI audit queue.

## Validation

```powershell
dotnet build backend/Garmetix.Api/Garmetix.Api.csproj
cd frontend/garmetix-web
npm.cmd run build
python scripts/validation/current-release-checks.py
docker compose up -d --build api web
```
