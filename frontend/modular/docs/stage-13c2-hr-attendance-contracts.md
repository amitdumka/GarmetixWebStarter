# Stage 13C.2 HR Attendance Contracts

Version: 5.13.20
Branch: Version5

## Scope

This stage adds contract parity checks for the HR/payroll read models used by the modular HR app. It compares the backend ASP.NET DTO records with the expected frontend contract fields and confirms the HR pages reference the correct endpoints and field names.

## Command

```powershell
npm.cmd run modular:hr:attendance-contract
```

## Covered Contracts

- `AttendanceMonthlyDto`
- `AttendancePayrollSummaryDto`
- `AttendancePayrollReviewDto`
- `AttendancePayrollReviewRowDto`
- `AttendanceSalarySlipDraftDto`
- `AttendanceSalarySlipDraftRowDto`
- `AttendanceSalaryPaymentCandidateDto`
- `SalaryPaymentPreviewRequest`
- `SalaryPaymentPreviewDto`

## Safety

This command reads source files only. It does not call the API and cannot mutate the database.
