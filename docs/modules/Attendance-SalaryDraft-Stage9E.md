# Stage 9E Attendance Salary Slip Draft Preview

Version: **4.10.5**  
Stage: **Stage 9E Attendance Salary Slip Draft Preview**  
Build Code: **GARMETIX-9E-20260619-4105**

Stage 9E adds a preview-only bridge from reviewed attendance payroll rows into salary-slip draft rows.

## Included

- `AttendanceSalarySlipDraft` domain model.
- `/api/attendance/salary-slip-drafts` preview endpoint.
- `/api/attendance/salary-slip-drafts/rebuild` rebuild endpoint.
- `/api/attendance/salary-slip-drafts/{id}/mark-ready` status endpoint.
- `/attendance/salary-draft` Nuxt page.
- Schema repair and EF migration scaffolding for existing PostgreSQL volumes.
- Static and host-level acceptance drill.

## Safety rule

This package does **not** create real salary slips, salary payments, accounting vouchers, PF/gratuity postings, or payroll ledger rows. It only prepares a preview from rows already marked `Reviewed` or `ApprovedForPayroll` in Attendance Payroll Review.

## Later packages

- Confirmed salary-slip generation from ready draft rows.
- Attendance payroll lock enforcement.
- Accounting posting workflow.
- Payslip print/PDF from attendance-backed salary slips.
