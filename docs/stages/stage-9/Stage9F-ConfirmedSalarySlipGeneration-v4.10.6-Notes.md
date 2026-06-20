# Stage 9F - Confirmed Salary Slip Generation v4.10.6

Version: `4.10.6`  
Stage: `Stage 9F Confirmed Salary Slip Generation`  
Build code: `GARMETIX-9F-20260619-4106`

## Scope

Stage 9F converts selected attendance salary draft rows into final `SalaryPaySlip` records only after explicit confirmation.

## Included

- `POST /api/attendance/salary-slip-drafts/generate-payslips`
- ReadyForPayroll-only generation guard
- Explicit `confirm: true` requirement
- Final `SalaryPaySlip` create/update from attendance salary draft rows
- Draft status update to `PostedToPayslip`
- Generated payslip reference fields on attendance salary draft rows
- Frontend action on `/attendance/salary-draft`
- Host acceptance drill: `scripts/linux/attendance-salary-generation-drill.sh`

## Not included

- Salary payment creation
- Accounting voucher posting
- Bank/cash payment posting
- PF/gratuity ledger posting
- Automatic payroll without confirmation

## Test

```bash
python3 scripts/validation/current-release-checks.py
export GARMETIX_SMOKE_USER='admin'
export GARMETIX_SMOKE_PASSWORD='your-admin-password'
./scripts/linux/attendance-salary-generation-drill.sh .env.production
```

Set `GARMETIX_ATTENDANCE_GENERATE_TEST=true` only when you intentionally want the drill to POST final salary slip generation on a live test month.
