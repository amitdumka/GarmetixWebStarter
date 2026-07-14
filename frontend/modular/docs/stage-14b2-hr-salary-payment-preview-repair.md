# Stage 14B.2 HR Salary Payment Preview Repair

Version: 6.0.10

## Purpose

Repair and verify the modular HR salary payment preview screen against the current backend contract.

## Changes

- Salary payment preview no longer requires a generated payslip id.
- When a candidate has no payslip id, the frontend sends `salaryPaySlipId: null`.
- Backend preview already supports this by falling back to the active salary structure for the selected employee and salary month.
- Preview display now includes total deductions and net payable along with advance deduction, previous due, outstanding amount, rounded paid amount, round-off and already paid.
- Added a contract gate to keep the frontend, backend DTO, backend service and readiness script aligned.

## Commands

```powershell
npm.cmd run modular:hr:salary-payment-preview-contract
npm.cmd run modular:hr:payroll-preview-readiness
npm.cmd --prefix frontend/modular run build:hr
```

## Safety

This stage remains non-mutating by default. The salary payment page calls only the preview endpoint and does not create salary payment vouchers.

## Deployment

No `.127` deployment is required for this checkpoint because Stage 14B.1 was just deployed and this is checkpoint 1 after that deployment.

## Next Stage

Stage 14B.3 should continue HR parity by tightening attendance today/monthly UI behavior and monthly attendance generation readiness, still keeping real attendance writes behind explicit gates.
