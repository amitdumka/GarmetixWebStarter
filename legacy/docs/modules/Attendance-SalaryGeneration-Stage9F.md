# Attendance Salary Slip Generation - Stage 9F

Stage 9F adds confirmed salary slip generation from attendance salary draft rows.

## User flow

1. Open Attendance Payroll Review and mark rows `Reviewed` or `ApprovedForPayroll`.
2. Open Attendance Salary Draft and rebuild draft rows.
3. Mark selected rows `ReadyForPayroll`.
4. Click `Generate Salary Slips`.
5. Confirm the browser prompt.
6. Review generated salary slips from the payroll payslip area.

## Safety behavior

Only `ReadyForPayroll` rows are used. Rows already marked `SalarySlipGenerated` are skipped. Salary payments and accounting vouchers are not created by this workflow.
