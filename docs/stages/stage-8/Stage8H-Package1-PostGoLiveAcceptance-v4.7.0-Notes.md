# Stage 8H Package 1 - Post-Go-Live Acceptance Stabilization (v4.7.0)

This package starts Stage 8H after Stage 8G go-live completion.

## Focus

- Keep the v4.6.8 npm registry hotfix as the deployment baseline.
- Add a post-go-live acceptance center for the recent access, HR/payroll, attendance, purchase and vendor-payment fixes.
- Add a Mac mini acceptance script to verify health/readiness endpoints and guide manual role checks.

## Manual acceptance items

- Legacy Overview must be visible only to Admin/Owner.
- Salary Structures must be hidden from normal/store users.
- Payslip and Salary Payment must remain visible to Store Manager, Accountant and other authorized power users.
- Attendance must expose Add/New Attendance for authorized users.
- Password reset must show clear SMTP-not-configured errors.
- Purchase New Inward must use the full-page workflow.
- Vendor Payments must support invoice-linked and advance payment entries.
