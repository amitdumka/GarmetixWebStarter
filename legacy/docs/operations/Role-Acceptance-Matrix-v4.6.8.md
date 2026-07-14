# Role Acceptance Matrix - v4.6.8

Use this checklist after deployment with separate real sessions for each role.

| Role | Expected access | Must be hidden |
| --- | --- | --- |
| Admin / Owner | Legacy Overview, setup, audit, backups, users, production readiness, Salary Structures | None unless intentionally restricted |
| Store Manager | Store operations, HR Attendance with Add Attendance when permitted, Payslip and Salary Payment where permitted, Purchase Inward, Vendor Payments | Salary Structures, admin-only maintenance |
| Accountant | Accounting, purchase/vendor payment workflow, Salary Payment and Payslip where permitted | Salary Structures unless explicitly granted |
| Power User | Assigned operational modules, payslip/payment where permitted | Salary Structures by default, admin-only pages |
| Normal User | Assigned daily workflow only | Legacy Overview, Salary Structures, setup, audit, backups, production readiness |

Record pass/fail evidence before final go-live sign-off.
