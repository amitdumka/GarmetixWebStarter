# Stage 11D-30 / v4.11.45 - Payroll Finalization

Build: `GARMETIX-11D30-20260626-4145`

## Completed

This stage completes the operational payroll workflow by adding a single Payroll Finalization page and validation script.

### Payroll Finalization page

New page:

`Payroll → Payroll Finalization`

The page runs the payroll lifecycle in order:

1. Recalculate monthly attendance summary.
2. Rebuild attendance payroll review.
3. Approve payroll review rows.
4. Rebuild salary slip drafts.
5. Mark salary drafts ReadyForPayroll.
6. Generate final salary payslips.
7. Generate salary payments with accounting posting.
8. Lock the attendance month after payment verification.

### Existing audited backend workflows reused

The page intentionally uses existing APIs instead of bypassing business logic:

- `/api/attendance/recalculate`
- `/api/attendance/payroll-review/rebuild`
- `/api/attendance/payroll-review/{id}/mark-reviewed`
- `/api/attendance/salary-slip-drafts/rebuild`
- `/api/attendance/salary-slip-drafts/{id}/mark-ready`
- `/api/attendance/salary-slip-drafts/generate-payslips`
- `/api/attendance/salary-payments/generate`
- `/api/attendance/lock-month`

Salary payment generation still posts accounting through the existing `SalaryPayment` posting workflow.

### Validation script

New script:

`./scripts/production/payroll-finalization-validation.sh`

It reports:

- active employees missing monthly attendance summary
- employees with zero salary
- payroll review rows not approved/reviewed
- salary drafts not ready/generated
- generated payslips without salary payment
- payslip net vs salary payment balance

## TODO status

Payroll finalization is now marked complete in `docs/planning/TODO-NEXT-PRODUCTION-STAGES.md`.

## Run validation

```bash
cd /opt/garmetix/current
PAYROLL_YEAR=2026 PAYROLL_MONTH=6 ./scripts/production/payroll-finalization-validation.sh
```

## Notes

Final production payroll still depends on correct attendance import, employee shift rules, and salary values in employee/salary structure records.
