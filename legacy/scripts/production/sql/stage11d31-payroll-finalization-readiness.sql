\echo 'Stage 11D-31 payroll readiness validation started'

CREATE TEMP TABLE _stage11d31_checks (
  severity text NOT NULL,
  check_name text NOT NULL,
  issue_count integer NOT NULL,
  details text
);

INSERT INTO _stage11d31_checks
SELECT 'FAIL', 'Active employees without current salary structure', count(*)::int,
  'Salary cannot be generated accurately without current SalaryStructure.'
FROM "Employees" e
WHERE COALESCE(e."Deleted", false) = false
  AND COALESCE(e."Working", true) = true
  AND NOT EXISTS (
    SELECT 1 FROM "SalaryStructures" ss
    WHERE ss."EmployeeId" = e."Id"
      AND COALESCE(ss."Deleted", false) = false
      AND ss."ToDate" IS NULL
  );

INSERT INTO _stage11d31_checks
SELECT 'WARN', 'Active employees without shift rule', count(*)::int,
  'Employee may fall back to store default; verify if gender/category/department rule is enough.'
FROM "Employees" e
WHERE COALESCE(e."Deleted", false) = false
  AND COALESCE(e."Working", true) = true
  AND NOT EXISTS (
    SELECT 1 FROM "EmployeeAttendanceShiftRules" r
    WHERE COALESCE(r."Deleted", false) = false
      AND r."EmployeeId" = e."Id"
      AND COALESCE(r."Active", true) = true
  );

INSERT INTO _stage11d31_checks
SELECT 'FAIL', 'Attendance rows without synced check-in/out for present/half-day', count(*)::int,
  'Present/HalfDay rows should have timing evidence or correction remarks.'
FROM "Attendance" a
JOIN "Employees" e ON e."Id" = a."EmployeeId"
WHERE COALESCE(a."Deleted", false) = false
  AND a."OnDate" >= date_trunc('month', current_date) - interval '2 months'
  AND a."Status" IN (1, 2)
  AND a."CheckInTime" IS NULL;

INSERT INTO _stage11d31_checks
SELECT 'WARN', 'Punches not reflected in Attendance daily rows', count(*)::int,
  'Punch exists but daily Attendance row missing for same employee/date.'
FROM "AttendancePunches" p
WHERE COALESCE(p."Deleted", false) = false
  AND p."LocalPunchTime" >= date_trunc('month', current_date) - interval '2 months'
  AND NOT EXISTS (
    SELECT 1 FROM "Attendance" a
    WHERE a."EmployeeId" = p."EmployeeId"
      AND a."CompanyId" = p."CompanyId"
      AND a."StoreId" = p."StoreId"
      AND a."OnDate"::date = p."LocalPunchTime"::date
      AND COALESCE(a."Deleted", false) = false
  );

INSERT INTO _stage11d31_checks
SELECT 'WARN', 'Approved/ready salary slips not paid', count(*)::int,
  'Check payroll payment stage before final lock.'
FROM "SalaryPaySlips" ps
WHERE COALESCE(ps."Deleted", false) = false
  AND NOT EXISTS (
    SELECT 1 FROM "SalaryPayments" sp
    WHERE sp."SalaryPaySlipId" = ps."Id" AND COALESCE(sp."Deleted", false) = false
  );

INSERT INTO _stage11d31_checks
SELECT 'INFO', 'Current month attendance count', count(*)::int,
  'Attendance rows in current month.'
FROM "Attendance"
WHERE COALESCE("Deleted", false) = false
  AND "OnDate" >= date_trunc('month', current_date)
  AND "OnDate" < date_trunc('month', current_date) + interval '1 month';

\echo 'Stage 11D-31 payroll readiness result'
SELECT * FROM _stage11d31_checks ORDER BY CASE severity WHEN 'FAIL' THEN 1 WHEN 'WARN' THEN 2 ELSE 3 END, check_name;
