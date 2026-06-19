# HR Schema Repair Hotfix v4.9.23

Package 23A fixes existing production/development databases where Package 22/23 code is deployed before the new HR columns/table exist.

## Fixed missing objects

Employee columns:

- `EmployeeCode`
- `FatherOrHusbandName`
- `Department`
- `Designation`
- `SalaryType`
- `MonthlySalary`
- `DailyWage`
- `EmployeeStatus`
- `ExitReason`
- `BloodGroup`
- `PhotoDataUrl`
- `BankAccountName`
- `BankAccountNumber`
- `IFSC`
- `ESINumber`
- `PFNumber`
- `EmergencyContact`

HR benefits table:

- `EmployeePayrollAdjustments`

## Apply fix

Deploy the new ZIP and restart the backend/API container. The schema repair is idempotent and runs at startup.

Optional manual repair after login as Admin:

```bash
curl -X POST "$BASE_URL/api/database/repair" \
  -H "Authorization: Bearer $TOKEN"
```

Then verify:

```bash
curl "$BASE_URL/api/employees" -H "Authorization: Bearer $TOKEN"
curl "$BASE_URL/api/hr-payroll/adjustments" -H "Authorization: Bearer $TOKEN"
curl "$BASE_URL/api/hr-payroll/adjustments/summary" -H "Authorization: Bearer $TOKEN"
```
