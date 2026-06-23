# Stage 8H Package 2 - Access HR Payroll Purchase Stabilization (v4.7.1)

This package verifies and hardens the user-requested acceptance items.

## Implemented / verified

- Legacy Overview is visible only to admin/owner users.
- Direct access to `/` after login redirects non-admin users to `/dashboard`.
- Legacy shell navigation also filters routes through the central access map.
- Payroll route and backend Payroll policy now allow store managers and remote accountants for payslip/payment visibility.
- Salary Structures tab remains hidden unless the user is admin, owner, accountant, remote accountant, or payroll.
- HR Attendance keeps both the page-header primary action and explicit **Add Attendance** action.
- Login forgot-password returns a clear server message when SMTP is not configured.
- Purchase inward uses full page `/purchase/new`.
- Vendor Payments page supports invoice-linked payments and advance vendor payments plus a register.
