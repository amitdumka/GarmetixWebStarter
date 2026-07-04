# Stage 14B.8 HR Live Payroll Acceptance

Version: `6.0.16`

This stage closes the next HR payroll evidence slice without writing salary data by default.

## Added

- Payroll Summary now has a report snapshot area and CSV export.
- Payslips now have CSV export for recent salary slip review.
- HR CSV export helper was added in the HR API utility module.
- A repeatable acceptance gate was added:

```powershell
npm.cmd run modular:hr:live-payroll-acceptance
```

## Live Check

After deployment, run:

```powershell
$env:NODE_OPTIONS='--use-system-ca'
npm.cmd run modular:hr:live-payroll-acceptance -- --live --strict
```

The live check verifies these SRP HR pages over public and LAN URLs:

- `/hr/attendance/payroll-summary`
- `/hr/payroll`
- `/hr/attendance/salary-draft`
- `/hr/attendance/salary-payment`

## Safety

- The acceptance gate does not generate payslips.
- The acceptance gate does not generate salary payments.
- Salary writes remain behind exact confirmation phrases and audit notes in the UI.
- No database schema change is included.

## Next

Stage 14B.9 should finish HR modular closure: run browser acceptance on a 14 inch layout, confirm manual operator checklist, and decide whether HR is ready for final live-token evidence or whether Books parity should start next.
