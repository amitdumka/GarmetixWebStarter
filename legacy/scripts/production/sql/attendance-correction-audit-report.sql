\echo 'Recent attendance correction audit rows'
SELECT
  a."OccurredAtUtc",
  a."AuditSource",
  a."Action",
  e."EmployeeCode",
  concat_ws(' ', e."FirstName", e."LastName") AS "EmployeeName",
  a."OnDate",
  a."ChangedFields",
  a."Reason",
  a."Remarks"
FROM "AttendanceCorrectionAudits" a
LEFT JOIN "Employees" e ON e."Id" = a."EmployeeId"
ORDER BY a."OccurredAtUtc" DESC
LIMIT 200;

\echo 'Correction counts by date and source'
SELECT "OnDate", "AuditSource", count(*) AS "Changes"
FROM "AttendanceCorrectionAudits"
GROUP BY "OnDate", "AuditSource"
ORDER BY "OnDate" DESC, "AuditSource";
