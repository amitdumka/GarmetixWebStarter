\echo 'Fix attendance punch LocalPunchTime from UTC-like value to India local time (Asia/Kolkata)'

-- This repair is intentionally conservative:
-- it only updates AttendancePunches where LocalPunchTime is almost equal to PunchTimeUtc.
-- That pattern means UTC was accidentally stored in the local-time column.

WITH candidates AS (
    SELECT "Id"
    FROM "AttendancePunches"
    WHERE NOT "Deleted"
      AND abs(extract(epoch FROM ("LocalPunchTime" - "PunchTimeUtc"))) <= 60
), updated AS (
    UPDATE "AttendancePunches" p
    SET "LocalPunchTime" = p."PunchTimeUtc" + interval '5 hours 30 minutes',
        "UpdatedAt" = now(),
        "Remarks" = left(coalesce(nullif(p."Remarks", ''), 'Attendance punch') || ' | IST local-time repair applied.', 300)
    FROM candidates c
    WHERE p."Id" = c."Id"
    RETURNING p."Id", p."EmployeeId", p."PunchType", p."PunchTimeUtc", p."LocalPunchTime"
)
SELECT count(*) AS punches_fixed FROM updated;

DROP TABLE IF EXISTS _attendance_ist_rollup;
CREATE TEMP TABLE _attendance_ist_rollup AS
SELECT
    p."CompanyId",
    p."StoreGroupId",
    p."StoreId",
    p."EmployeeId",
    p."LocalPunchTime"::date AS "OnDate",
    min(CASE WHEN lower(p."PunchType") = 'checkin' THEN p."LocalPunchTime"::time END) AS "CheckInClock",
    min(CASE WHEN lower(p."PunchType") = 'breakout' THEN p."LocalPunchTime"::time END) AS "BreakOutClock",
    max(CASE WHEN lower(p."PunchType") = 'breakin' THEN p."LocalPunchTime"::time END) AS "BreakInClock",
    max(CASE WHEN lower(p."PunchType") = 'checkout' THEN p."LocalPunchTime"::time END) AS "CheckOutClock"
FROM "AttendancePunches" p
WHERE NOT p."Deleted"
  AND p."LocalPunchTime" >= (now() - interval '60 days')
GROUP BY p."CompanyId", p."StoreGroupId", p."StoreId", p."EmployeeId", p."LocalPunchTime"::date;

DO $$
DECLARE
    has_break_out boolean;
    has_break_in boolean;
    updated_count integer := 0;
BEGIN
    SELECT EXISTS (
        SELECT 1 FROM information_schema.columns
        WHERE table_schema = 'public' AND table_name = 'Attendance' AND column_name = 'BreakOutTime'
    ) INTO has_break_out;

    SELECT EXISTS (
        SELECT 1 FROM information_schema.columns
        WHERE table_schema = 'public' AND table_name = 'Attendance' AND column_name = 'BreakInTime'
    ) INTO has_break_in;

    IF has_break_out AND has_break_in THEN
        UPDATE "Attendance" a
        SET "CheckInTime" = CASE WHEN r."CheckInClock" IS NULL THEN a."CheckInTime" ELSE r."CheckInClock" - time '00:00' END,
            "BreakOutTime" = CASE WHEN r."BreakOutClock" IS NULL THEN a."BreakOutTime" ELSE r."BreakOutClock" - time '00:00' END,
            "BreakInTime" = CASE WHEN r."BreakInClock" IS NULL THEN a."BreakInTime" ELSE r."BreakInClock" - time '00:00' END,
            "CheckOutTime" = CASE WHEN r."CheckOutClock" IS NULL THEN a."CheckOutTime" ELSE r."CheckOutClock" - time '00:00' END,
            "EntryTime" = CASE WHEN r."CheckInClock" IS NULL THEN a."EntryTime" ELSE to_char(r."CheckInClock", 'HH24:MI') END,
            "UpdatedAt" = now(),
            "Remarks" = left(coalesce(nullif(a."Remarks", ''), 'Attendance') || ' | IST local-time repair synced from punches.', 100)
        FROM _attendance_ist_rollup r
        WHERE a."CompanyId" = r."CompanyId"
          AND a."StoreId" = r."StoreId"
          AND a."EmployeeId" = r."EmployeeId"
          AND a."OnDate"::date = r."OnDate";
    ELSE
        UPDATE "Attendance" a
        SET "CheckInTime" = CASE WHEN r."CheckInClock" IS NULL THEN a."CheckInTime" ELSE r."CheckInClock" - time '00:00' END,
            "CheckOutTime" = CASE WHEN r."CheckOutClock" IS NULL THEN a."CheckOutTime" ELSE r."CheckOutClock" - time '00:00' END,
            "EntryTime" = CASE WHEN r."CheckInClock" IS NULL THEN a."EntryTime" ELSE to_char(r."CheckInClock", 'HH24:MI') END,
            "UpdatedAt" = now(),
            "Remarks" = left(coalesce(nullif(a."Remarks", ''), 'Attendance') || ' | IST local-time repair synced from punches.', 100)
        FROM _attendance_ist_rollup r
        WHERE a."CompanyId" = r."CompanyId"
          AND a."StoreId" = r."StoreId"
          AND a."EmployeeId" = r."EmployeeId"
          AND a."OnDate"::date = r."OnDate";
    END IF;

    GET DIAGNOSTICS updated_count = ROW_COUNT;
    RAISE NOTICE 'Daily Attendance rows synced from repaired punches: %', updated_count;
END $$;

\echo 'Recent repaired/IST punch sample'
SELECT "EmployeeId", "PunchType", "PunchTimeUtc", "LocalPunchTime", "Source"
FROM "AttendancePunches"
WHERE NOT "Deleted"
ORDER BY "UpdatedAt" DESC NULLS LAST, "LocalPunchTime" DESC
LIMIT 20;
