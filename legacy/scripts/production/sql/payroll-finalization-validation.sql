
\echo 'Payroll finalization validation report'
\echo 'Generated at:' :generated_at
\echo 'Target year:' :target_year ' month:' :target_month

WITH params AS (
  SELECT :'target_year'::int AS y, :'target_month'::int AS m, (:'target_year'::int * 100 + :'target_month'::int) AS salary_month,
         make_date(:'target_year'::int, :'target_month'::int, 1) AS month_start
), active_employees AS (
  SELECT e."Id", e."StaffName", e."EmployeeCode", e."MonthlySalary", e."Working", e."JoiningDate", e."LeavingDate", e."CompanyId", e."StoreGroupId", e."StoreId"
  FROM "Employees" e, params p
  WHERE NOT e."Deleted" AND e."Working" = true
    AND e."JoiningDate" <= (p.month_start + interval '1 month - 1 day')::date
    AND (e."LeavingDate" IS NULL OR e."LeavingDate" >= p.month_start)
), attendance_summary AS (
  SELECT s.* FROM "AttendanceMonthlySummaries" s, params p WHERE s."Year" = p.y AND s."Month" = p.m AND NOT s."Deleted"
), review_rows AS (
  SELECT r.* FROM "AttendancePayrollReviews" r, params p WHERE r."Year" = p.y AND r."Month" = p.m AND NOT r."Deleted"
), draft_rows AS (
  SELECT d.* FROM "AttendanceSalarySlipDrafts" d, params p WHERE d."Year" = p.y AND d."Month" = p.m AND NOT d."Deleted"
), payslips AS (
  SELECT ps.* FROM "SalaryPaySlips" ps, params p WHERE ps."PayPeriodStart" = p.month_start AND NOT ps."Deleted"
), payments AS (
  SELECT sp.* FROM "SalaryPayments" sp, params p WHERE sp."SalaryMonth" = p.salary_month AND NOT sp."Deleted"
)
SELECT 'SUMMARY' AS section,
       (SELECT count(*) FROM active_employees) AS active_employees,
       (SELECT count(*) FROM attendance_summary) AS monthly_attendance_rows,
       (SELECT count(*) FROM review_rows) AS payroll_review_rows,
       (SELECT count(*) FROM draft_rows) AS salary_draft_rows,
       (SELECT count(*) FROM payslips) AS payslips,
       (SELECT count(*) FROM payments) AS salary_payments;

\echo 'FAIL rows: active employees missing attendance monthly summary'
SELECT e."EmployeeCode", e."StaffName", e."MonthlySalary"
FROM active_employees e
LEFT JOIN attendance_summary s ON s."EmployeeId" = e."Id"
WHERE s."Id" IS NULL
ORDER BY e."StaffName";

\echo 'WARN rows: active employees with zero monthly salary'
SELECT e."EmployeeCode", e."StaffName", e."MonthlySalary"
FROM active_employees e
WHERE coalesce(e."MonthlySalary",0) <= 0
ORDER BY e."StaffName";

\echo 'FAIL rows: payroll review not approved/reviewed'
SELECT e."EmployeeCode", e."StaffName", r."ReviewStatus", r."PayableDays", r."EstimatedGrossPay"
FROM review_rows r
JOIN active_employees e ON e."Id" = r."EmployeeId"
WHERE r."ReviewStatus" NOT IN ('Reviewed','ApprovedForPayroll')
ORDER BY e."StaffName";

\echo 'FAIL rows: salary drafts not ready/generated'
SELECT e."EmployeeCode", e."StaffName", d."DraftStatus", d."PayrollPostStatus", d."NetPayPreview"
FROM draft_rows d
JOIN active_employees e ON e."Id" = d."EmployeeId"
WHERE d."DraftStatus" NOT IN ('ReadyForPayroll') AND d."PayrollPostStatus" <> 'SalarySlipGenerated'
ORDER BY e."StaffName";

\echo 'FAIL rows: ready drafts without generated payslip'
SELECT e."EmployeeCode", e."StaffName", d."DraftStatus", d."PayrollPostStatus", d."NetPayPreview"
FROM draft_rows d
JOIN active_employees e ON e."Id" = d."EmployeeId"
WHERE d."DraftStatus" = 'ReadyForPayroll' AND coalesce(d."PayrollPostStatus", '') <> 'SalarySlipGenerated'
ORDER BY e."StaffName";

\echo 'FAIL rows: generated payslips without salary payment generated'
SELECT e."EmployeeCode", e."StaffName", d."NetPayPreview", d."PaymentPostStatus", d."GeneratedSalaryPaySlipId", d."GeneratedSalaryPaymentId"
FROM draft_rows d
JOIN active_employees e ON e."Id" = d."EmployeeId"
WHERE d."PayrollPostStatus" = 'SalarySlipGenerated' AND coalesce(d."PaymentPostStatus", 'NotPaid') <> 'SalaryPaymentGenerated'
ORDER BY e."StaffName";

\echo 'CHECK: salary totals by employee'
SELECT e."EmployeeCode", e."StaffName",
       coalesce(ps."NetSalary", 0) AS payslip_net,
       coalesce(sum(sp."Amount"), 0) AS paid_amount,
       greatest(coalesce(ps."NetSalary", 0) - coalesce(sum(sp."Amount"), 0), 0) AS balance
FROM active_employees e
LEFT JOIN payslips ps ON ps."EmployeeId" = e."Id"
LEFT JOIN payments sp ON sp."EmployeeId" = e."Id"
GROUP BY e."EmployeeCode", e."StaffName", ps."NetSalary"
ORDER BY e."StaffName";
