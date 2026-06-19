# Stage 8I Package 23 - HR Benefits and Payroll Adjustments v4.9.22

## Scope

Package 23 adds HR Benefits and Payroll Adjustment workflow after the employee master upgrade.

## Added

- New page: `/hr-benefits`.
- New model/table: `EmployeePayrollAdjustments`.
- Supported adjustment types:
  - Salary Advance
  - Advance Recovery
  - Leave
  - Bonus
  - Leave Encashment
  - PF
  - Gratuity
  - Other
- API endpoints:
  - `GET /api/hr-payroll/adjustments`
  - `GET /api/hr-payroll/adjustments/summary`
  - `POST /api/hr-payroll/adjustments`
  - `PUT /api/hr-payroll/adjustments/{id}`
  - `DELETE /api/hr-payroll/adjustments/{id}`
- Payroll service now reads HR Benefits rows when generating/paying payroll:
  - bonus and leave encashment add to other earnings
  - PF employee and gratuity values add to deductions/provisions
  - salary advance and advance recovery reduce payable salary when recoverable

## Validation

Run:

```bash
python3 scripts/validation/hr-benefits-payroll-check.py
python3 scripts/validation/current-release-checks.py
```

Host acceptance:

```bash
export GARMETIX_SMOKE_USER='admin'
export GARMETIX_SMOKE_PASSWORD='your-admin-password'
./scripts/linux/hr-benefits-payroll-acceptance-drill.sh .env.production
```
