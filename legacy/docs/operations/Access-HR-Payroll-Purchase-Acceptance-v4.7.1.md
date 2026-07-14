# Access, HR/Payroll and Purchase Acceptance v4.7.1

## Manual checks

1. Login as admin/owner and confirm **Legacy Overview** is visible.
2. Login as store manager and confirm **Legacy Overview** is hidden and direct `/` goes to `/dashboard`.
3. Login as store manager / power user / accountant and open `/payroll`.
4. Confirm **Payslips** and **Salary Payments** tabs are visible.
5. Confirm **Salary Structures** appears for accountant/payroll/admin/owner, and is hidden for store manager/normal user.
6. Open `/hr`, switch to Attendance, confirm **Add Attendance** is visible and opens the attendance form.
7. Use login forgot-password with SMTP disabled and confirm the message explains SMTP/admin reset clearly.
8. Open `/purchase/new` and confirm inward is a full page.
9. Open `/vendor-payments` and confirm invoice-linked payment and advance payment workflows are available.
