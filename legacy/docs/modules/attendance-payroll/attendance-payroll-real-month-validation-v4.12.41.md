# Stage 11D-126 - Attendance/Payroll Real-Month Validation Closure - v4.12.41

## Purpose

This stage adds a final real-month QA layer for Attendance and Payroll after purchase import and Vyapar sale import closure work. It does not change attendance calculation or payroll posting logic. It gives the operator a visible **Complete / Not Complete** status for the selected salary month and lists the exact blockers before payroll can be signed off.

## Added backend endpoints

### `GET /api/payroll/real-month-validation`

Query parameters:

- `year`
- `month`

Returns:

- selected month and generated timestamp
- `Complete` / `Not Complete` status
- active employee count
- monthly attendance summary coverage
- pending regularization count
- pending photo proof count
- payroll review approval coverage
- salary draft readiness coverage
- payslip count
- salary payment count
- locked monthly rows
- gross, deduction, net, paid and outstanding totals
- blocker/warning issue list
- employee-wise evidence rows
- final closeout checklist
- known limitations
- next outside-module candidates

### `GET /api/payroll/real-month-validation.csv`

Exports the same evidence as a CSV file for payroll month records.

## Added frontend changes

Page upgraded:

- `Payroll → Finalization`

New card:

- **Attendance/Payroll real-month validation**

The card shows:

- `Complete` / `Not Complete`
- counts for active employees, monthly rows, approved reviews, ready drafts, payslips and salary payments
- net salary and outstanding amount
- closeout checks
- blocker/warning issues
- employee-wise evidence table
- final closeout checklist
- known limitations
- next outside-module candidates
- CSV export button

## Completion rules

The month is marked **Complete** only when:

1. Active employees exist.
2. Every active employee has monthly attendance summary evidence.
3. Pending regularization requests are cleared.
4. Pending photo proof reviews are cleared.
5. Payroll review rows are Reviewed or ApprovedForPayroll.
6. Salary drafts are ready, posted to payslip, or paid.
7. Payslips are generated without duplicate employee-month payslips.
8. Salary payments cover generated net salary.
9. Monthly attendance rows are locked.

If any blocker remains, the status is **Not Complete**.

## Operator workflow

1. Recalculate Attendance Monthly Summary.
2. Clear photo proof and regularization queues.
3. Rebuild and approve Attendance Payroll Review.
4. Rebuild salary drafts and mark them ready.
5. Generate payslips.
6. Post salary payments.
7. Lock the month.
8. Export validation CSV and keep it with payroll evidence.

## Known limitations

- This validates database evidence only. External bank debit screenshots or cash signatures still need manual checking.
- If salary was paid outside the salary-payment module, enter/correct Salary Payment records first.
- Biometric hardware identity quality depends on kiosk/bridge setup. This stage only checks pending review/correction queues.
- Off-cycle advances, bonuses and deductions should be represented through Employee Payroll Adjustments or Salary Payment remarks.

## Files changed

- `backend/Garmetix.Api/Payroll/PayrollEndpoints.cs`
- `backend/Garmetix.Api/Payroll/PayrollDtos.cs`
- `frontend/garmetix-web/pages/payroll/finalization.vue`
- `backend/Garmetix.Api/AppInfo/AppInfoEndpoints.cs`
- `frontend/garmetix-web/utils/appVersion.ts`
- `backend/Garmetix.Api/Garmetix.Api.csproj`
- `scripts/linux/create-database-backup-now.sh`
- `scripts/linux/smoke-test.sh`

## Version

- Version: `4.12.41`
- Stage: `Stage 11D-126 Attendance Payroll Real-Month Validation`
- Build code: `GARMETIX-11D126-20260701-4241`

## Next recommended module

After this, move to **Accounting/GST post-import live validation** so purchase import, Vyapar sale import, salary payments, day book and GST/accounting reports can be cross-checked together.
