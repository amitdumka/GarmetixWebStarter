# Stage 8I Package 23A - HR Schema Repair Hotfix v4.9.23

## Purpose

This hotfix repairs existing PostgreSQL volumes that were upgraded to Package 22/23 code but did not receive the new HR schema changes.

The reported runtime failures were:

- `GET /api/employees` failed because column `Employees.BankAccountName` was missing.
- `GET /api/hr-payroll/adjustments` and `/summary` failed because relation `EmployeePayrollAdjustments` was missing.

## Changes

- Added `RepairHrEmployeeMasterAndBenefitsAsync` to the database schema repair service.
- Startup schema drift repair now creates missing HR employee master columns.
- Startup schema drift repair now creates/repairs the `EmployeePayrollAdjustments` table.
- The manual admin repair endpoint `/api/database/repair` also runs the HR repair.

## Operational note

After deploying this package, restart the API container. The repair runs during startup. If the old container is still running, an Admin can also call `/api/database/repair` after login.

## Version

- Version: `4.9.23`
- Build code: `GARMETIX-8I-20260619-49230`
