# Stage 13C.3 HR Browser Acceptance

Version: 5.13.21
Branch: Version5

## Scope

This stage adds repeatable browser acceptance for the HR app screens that matter for attendance and payroll work on a 14 inch laptop viewport.

## Command

Dry run:

```powershell
npm.cmd run modular:hr:browser-acceptance
```

Live browser check:

```powershell
npm.cmd run modular:hr:browser-acceptance -- --live
```

## Covered Routes

- `/attendance/today`
- `/attendance/monthly`
- `/attendance/payroll-review`
- `/attendance/salary-draft`
- `/attendance/salary-payment`
- `/attendance/devices`

## Acceptance Rules

- Each route must render its expected heading.
- The page must remain usable at `1366x768`.
- Tables must use scrolling where needed rather than pushing the whole shell out of view.
- Console errors are warnings by default and failures with `--strict-console`.

## Safety

The live check seeds browser-local auth state only. It does not create attendance, payroll, payslip, salary payment, or device data.
