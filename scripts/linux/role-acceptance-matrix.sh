#!/usr/bin/env bash
set -euo pipefail
cat <<'MATRIX'
Garmetix Role Acceptance Matrix

1. Admin / Owner
   - Legacy Overview visible
   - Setup, audit, backups, users, production readiness visible
   - Salary Structures visible

2. Store Manager
   - Store operations visible
   - Attendance Add action visible where HR access is allowed
   - Payslip and Salary Payment visible where payroll access is allowed
   - Salary Structures hidden

3. Accountant
   - Accounting and Vendor Payments visible
   - Payslip and Salary Payment visible where payroll access is allowed
   - Salary Structures hidden unless intentionally granted

4. Power User
   - Assigned operational modules visible
   - Payslip/Salary Payment visible where allowed
   - Salary Structures hidden by default

5. Normal User
   - Only assigned daily modules visible
   - Legacy Overview, Salary Structures, setup/audit/backups hidden
MATRIX
