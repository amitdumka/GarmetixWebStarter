# Stage 14B.7 HR Payroll Approval Evidence

Version: 6.0.15

## Scope

This stage tightens the HR payroll chain after attendance is reviewed:

1. Payroll review evidence.
2. Salary slip draft readiness.
3. Guarded payslip generation.
4. Guarded salary payment generation.

## Added

- Payroll Review now shows reviewer, review time and notes as evidence columns.
- Payroll Review now shows approval evidence counts for approved, reviewed, held and evidence-backed rows.
- Salary Draft now exposes a guarded final payslip generation action.
- Salary Payment now exposes a guarded final salary payment generation action.
- Added `hr-payroll-approval-evidence-readiness.mjs` to protect the source contracts.

## Guardrails

- Payslip generation requires:
  - ReadyForPayroll rows.
  - audit notes,
  - exact phrase `GENERATE PAYSLIPS YYYYMM`,
  - backend `confirm: true`.
- Salary payment generation requires:
  - generated salary slip rows still pending payment,
  - audit notes,
  - exact phrase `GENERATE SALARY PAYMENTS YYYYMM`,
  - backend `confirm: true`.
- Salary payment generation creates `SalaryPayment` rows and accounting posting through the existing backend workflow.
- No database schema change was made.

## Validation

Run:

```powershell
npm.cmd run modular:hr:payroll-approval-evidence-readiness
npm.cmd run modular:hr:payroll-preview-readiness
npm.cmd run modular:hr:parity-baseline
npm.cmd run modular:check
npm.cmd --prefix frontend\modular run build:hr
```

## Deployment Cadence

This is checkpoint 3 after the last `.127` deployment, so this stage should be deployed to `.127` after validation passes.

## Next

Stage 14B.8 should run live HR acceptance on `.127`: review pages, payslip generation gate, salary payment generation gate, then payroll report/export evidence.
