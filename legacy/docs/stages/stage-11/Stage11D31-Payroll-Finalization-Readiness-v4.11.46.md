# Stage 11D-31 — Payroll Finalization Readiness

Version: **4.11.46**

This stage adds payroll readiness checks before final salary generation and payment.

It validates active employees, salary structures, shift-rule readiness, punch-to-daily-attendance sync, and unpaid payslip/payment readiness.

Run:

```bash
cd /opt/garmetix/current
chmod +x scripts/production/*.sh
./scripts/production/stage11d31-payroll-readiness.sh
```

Reports are saved under:

```txt
reports/stage11d31-payroll-readiness/
```

After this report is clean, continue with monthly payroll generation, manual adjustments, salary payment, payslip PDF, and payroll lock testing in the UI.
