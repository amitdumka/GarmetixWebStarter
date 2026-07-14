# HR Benefits and Payroll Adjustments Operation Guide v4.9.22

Open `/hr-benefits` to record salary adjustments before final payroll.

## Common workflows

### Salary advance

Create a `SalaryAdvance` row with the amount and keep **Recover from salary** enabled. It will reduce payable salary until closed or recovered.

### Advance recovery

Create an `AdvanceRecovery` row for a specific salary month if recovery is planned separately from the original advance.

### Leave and leave encashment

Use `Leave` to track leave days and `LeaveEncashment` to add payable encashment amount to payroll earnings.

### Bonus

Use `Bonus` to add performance/festival/yearly bonus amount to the payroll month.

### PF and gratuity

Use `PF` rows for employee/employer PF values and `Gratuity` rows for gratuity provision/settlement amount.

## Payroll integration

Payroll generation and payment preview now read HR Benefits rows for the selected employee and salary month. Review open advances before salary payment.
