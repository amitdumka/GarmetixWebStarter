# Stage 8A Package 8 - v4.0.6

Date: 2026-06-13

## Delivered

- Replaced direct `SalaryPayment` entity binding with a dedicated create/edit request.
- Added server payroll preview for gross salary, base deductions, salary advance, prior company due, already-paid value, outstanding value, and round-off.
- Added current-month advance to deductions and carried prior unpaid salary into the current payable amount.
- Rounded the final paid amount to whole rupees while retaining editable actual values.
- Applied the same round-off to payable and carry-forward liability so paise-only balances close after full payment.
- Blocked duplicate or over-limit net-salary payments after the rounded outstanding amount is settled.
- Generated salary-payment vouchers server-side as `StoreCode/YYYYMM/SPAY/0001`, scoped by store and month.
- Preserved local payment dates and excluded deleted salary entries from due calculations.
- Updated the payroll form to show calculation components and support explicit recalculation.

## Validation

```powershell
dotnet build backend/Garmetix.Api/Garmetix.Api.csproj --no-restore
cd frontend/garmetix-web
npm.cmd run build
python scripts/validation/current-release-checks.py
docker compose up -d --build api web
```
