# Stage 9D - Attendance Payroll Review v4.10.4

Version: `4.10.4`  
Stage: `Stage 9D Attendance Payroll Integration Foundation`  
Release: `Attendance Payroll Review Foundation`  
Build code: `GARMETIX-9D-20260619-4104`

## Purpose

Stage 9D connects attendance monthly summaries to a payroll review layer without changing salary slips or posting payroll deductions automatically.

## Added

- `AttendancePayrollReview` domain model and storage.
- `GET /api/attendance/payroll-review` for review rows.
- `POST /api/attendance/payroll-review/rebuild` to rebuild rows from attendance monthly summaries.
- `POST /api/attendance/payroll-review/{id}/mark-reviewed` to mark rows as Reviewed, ApprovedForPayroll, or OnHold.
- `/attendance/payroll-review` Nuxt page for HR/Payroll review.
- Schema repair and EF migration: `20260619123000_AddAttendancePayrollReviewStage9D`.
- Host acceptance drill: `scripts/linux/attendance-payroll-review-drill.sh`.

## Payroll safety rule

This package does **not** create salary slips, salary payments, PF/gratuity rows, deductions, or accounting vouchers. It only prepares review numbers:

- present days
- absent days
- late days
- half days
- leave days
- payable days
- deduction days
- overtime minutes
- estimated gross pay reference

Automatic payroll posting remains a later package.

## Later list

- Attendance payroll posting and salary-slip draft creation.
- Attendance lock before salary generation.
- Overtime payout rules.
- Late mark deduction policy.
- Leave balance integration.
