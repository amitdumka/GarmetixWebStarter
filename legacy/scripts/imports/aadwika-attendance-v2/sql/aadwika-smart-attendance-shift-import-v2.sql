-- Stage 11D-24 Aadwika/Smart Menswear shift-aware employee + attendance import
-- Loads employees_shift_final.csv, shift_templates.csv and attendance_status_final.csv from /tmp.
-- Creates employees, attendance shifts, employee-specific shift rules, Attendance rows and AttendancePunches.
-- Safe to re-run: deletes only punches created by this import batch and upserts Attendance rows by employee/date.

CREATE EXTENSION IF NOT EXISTS pgcrypto;

\echo 'Loading staging tables for Stage 11D-24 attendance import'
DROP TABLE IF EXISTS _aadwika_emp_import;
DROP TABLE IF EXISTS _aadwika_shift_import;
DROP TABLE IF EXISTS _aadwika_att_import;

CREATE TEMP TABLE _aadwika_emp_import (
    source text,
    emp_id int,
    employee_code text,
    first_name text,
    last_name text,
    full_name text,
    father_or_husband_name text,
    dob date,
    mobile text,
    aadhar text,
    joining_date date,
    leaving_date date,
    status text,
    working boolean,
    department text,
    designation text,
    category int,
    seed_id uuid,
    gender text,
    gender_code int,
    shift_code text,
    shift_name text,
    import_notes text
);

CREATE TEMP TABLE _aadwika_shift_import (
    shift_code text PRIMARY KEY,
    shift_name text NOT NULL,
    start_min int NOT NULL,
    end_min int NOT NULL,
    has_break boolean NOT NULL,
    break_start_min int NULL,
    break_end_min int NULL,
    required_full_sessions int NOT NULL,
    required_half_sessions int NOT NULL,
    notes text
);

CREATE TEMP TABLE _aadwika_att_import (
    employee_name text,
    employee_code text,
    on_date date,
    status text,
    status_code int,
    remarks text,
    seed_id uuid
);

\copy _aadwika_emp_import FROM '/tmp/employees_shift_final.csv' WITH (FORMAT csv, HEADER true)
\copy _aadwika_shift_import FROM '/tmp/shift_templates.csv' WITH (FORMAT csv, HEADER true)
\copy _aadwika_att_import FROM '/tmp/attendance_status_final.csv' WITH (FORMAT csv, HEADER true)

\echo 'Importing employees, shifts, shift rules, attendance and punches'
DO $$
DECLARE
    v_company_id uuid;
    v_store_id uuid;
    v_store_group_id uuid;
    v_inserted_employees int := 0;
    v_updated_employees int := 0;
    v_shift_count int := 0;
    v_rule_count int := 0;
    v_deleted_old_punches int := 0;
    v_inserted_punches int := 0;
    v_upserted_attendance int := 0;
    v_absent_rows int := 0;
BEGIN
    SELECT c."Id" INTO v_company_id
    FROM "Companies" c
    WHERE NOT c."Deleted" AND lower(trim(c."Name")) = lower('Aadwika Fashion')
    ORDER BY c."CreatedAt" DESC
    LIMIT 1;

    IF v_company_id IS NULL THEN
        SELECT c."Id" INTO v_company_id
        FROM "Companies" c
        WHERE NOT c."Deleted" AND c."Name" ILIKE '%Aadwika%Fashion%'
        ORDER BY c."CreatedAt" DESC
        LIMIT 1;
    END IF;

    IF v_company_id IS NULL THEN
        RAISE EXCEPTION 'Company Aadwika Fashion not found. Create/seed company before import.';
    END IF;

    SELECT s."Id", s."StoreGroupId" INTO v_store_id, v_store_group_id
    FROM "Stores" s
    WHERE NOT s."Deleted" AND s."CompanyId" = v_company_id AND lower(trim(s."Name")) = lower('Smart Menswear')
    ORDER BY s."CreatedAt" DESC
    LIMIT 1;

    IF v_store_id IS NULL THEN
        SELECT s."Id", s."StoreGroupId" INTO v_store_id, v_store_group_id
        FROM "Stores" s
        WHERE NOT s."Deleted" AND s."CompanyId" = v_company_id AND s."Name" ILIKE '%Smart%Menswear%'
        ORDER BY s."CreatedAt" DESC
        LIMIT 1;
    END IF;

    IF v_store_id IS NULL THEN
        RAISE EXCEPTION 'Store Smart Menswear not found under Aadwika Fashion.';
    END IF;

    -- Upsert employees using employee code first, name second.
    INSERT INTO "Employees" (
        "Id", "Title", "FirstName", "LastName", "Gender", "DateOfBirth", "EmpId", "EmployeeCode",
        "FatherOrHusbandName", "Department", "Designation", "SalaryType", "MonthlySalary", "DailyWage",
        "EmployeeStatus", "ExitReason", "BloodGroup", "PhotoDataUrl", "JoiningDate", "LeavingDate",
        "Working", "Category", "PAN", "Aadhar", "Email", "Mobile", "BankAccountName", "BankAccountNumber",
        "IFSC", "ESINumber", "PFNumber", "EmergencyContact", "CreatedAt", "UpdatedAt", "Synced", "Deleted",
        "CompanyId", "CreatedBy", "StoreGroupId", "StoreId"
    )
    SELECT
        e.seed_id, null, left(e.first_name,50), left(e.last_name,50), COALESCE(e.gender_code,0), COALESCE(e.dob, date '2000-01-01'), e.emp_id, left(e.employee_code,40),
        nullif(left(COALESCE(e.father_or_husband_name,''),120),''), left(COALESCE(e.department,'Store'),80), left(COALESCE(e.designation,'Staff'),80),
        'Monthly', 0, 0, left(COALESCE(e.status,'Active'),30), null, null, null,
        e.joining_date, e.leaving_date, COALESCE(e.working,true), COALESCE(e.category,8), null, left(COALESCE(e.aadhar,''),12), null, left(COALESCE(e.mobile,''),15),
        null, null, null, null, null, null, now(), now(), false, false,
        v_company_id, 'attendance-final-import-v2', v_store_group_id, v_store_id
    FROM _aadwika_emp_import e
    WHERE NOT EXISTS (
        SELECT 1 FROM "Employees" x
        WHERE NOT x."Deleted" AND x."CompanyId" = v_company_id AND x."StoreId" = v_store_id
          AND (lower(trim(COALESCE(x."EmployeeCode",''))) = lower(trim(e.employee_code))
               OR lower(trim(COALESCE(x."FirstName",'') || ' ' || COALESCE(x."LastName",''))) = lower(trim(e.full_name)))
    );
    GET DIAGNOSTICS v_inserted_employees = ROW_COUNT;

    UPDATE "Employees" x
    SET "EmployeeCode" = left(e.employee_code,40),
        "FirstName" = left(e.first_name,50),
        "LastName" = left(e.last_name,50),
        "Gender" = COALESCE(e.gender_code, x."Gender"),
        "EmpId" = e.emp_id,
        "FatherOrHusbandName" = nullif(left(COALESCE(e.father_or_husband_name,''),120),''),
        "DateOfBirth" = COALESCE(e.dob, x."DateOfBirth"),
        "Department" = left(COALESCE(e.department,'Store'),80),
        "Designation" = left(COALESCE(e.designation,'Staff'),80),
        "EmployeeStatus" = left(COALESCE(e.status,'Active'),30),
        "JoiningDate" = e.joining_date,
        "LeavingDate" = e.leaving_date,
        "Working" = COALESCE(e.working,true),
        "Category" = COALESCE(e.category,8),
        "Aadhar" = left(COALESCE(e.aadhar,''),12),
        "Mobile" = left(COALESCE(e.mobile,''),15),
        "UpdatedAt" = now()
    FROM _aadwika_emp_import e
    WHERE NOT x."Deleted" AND x."CompanyId" = v_company_id AND x."StoreId" = v_store_id
      AND (lower(trim(COALESCE(x."EmployeeCode",''))) = lower(trim(e.employee_code))
           OR lower(trim(COALESCE(x."FirstName",'') || ' ' || COALESCE(x."LastName",''))) = lower(trim(e.full_name)));
    GET DIAGNOSTICS v_updated_employees = ROW_COUNT;

    -- Upsert shift templates for this store.
    INSERT INTO "AttendanceShifts" (
        "Id", "CreatedAt", "UpdatedAt", "Synced", "Deleted", "CompanyId", "CreatedBy", "StoreGroupId", "StoreId",
        "Name", "StartTimeMinutes", "EndTimeMinutes", "GraceMinutes", "LateAfterMinutes", "HalfDayAfterMinutes",
        "MinimumFullDayMinutes", "MinimumHalfDayMinutes", "OvertimeAfterMinutes", "AutoCheckoutEnabled", "AutoCheckoutTimeMinutes",
        "WeeklyOffDays", "Active", "AttendanceMode", "ShiftCategory", "HasBreak", "RequiresBreakPunch", "BreakStartMinutes", "BreakEndMinutes",
        "RequiredSessionsForFullDay", "RequiredSessionsForHalfDay", "CountBreakAsWork"
    )
    SELECT gen_random_uuid(), now(), NULL, false, false, v_company_id, 'attendance-final-import-v2', v_store_group_id, v_store_id,
           s.shift_name, s.start_min, s.end_min, 10, 10, COALESCE(s.break_start_min, s.start_min + ((s.end_min - s.start_min) / 2)),
           0, 0, s.end_min, false, NULL, 'Sunday', true, 'SessionBased', s.shift_code,
           s.has_break, s.has_break, s.break_start_min, s.break_end_min, s.required_full_sessions, s.required_half_sessions, false
    FROM _aadwika_shift_import s
    WHERE NOT EXISTS (
        SELECT 1 FROM "AttendanceShifts" sh
        WHERE NOT sh."Deleted" AND sh."CompanyId" = v_company_id AND sh."StoreId" = v_store_id AND sh."Name" = s.shift_name
    );
    GET DIAGNOSTICS v_shift_count = ROW_COUNT;

    UPDATE "AttendanceShifts" sh
    SET "StartTimeMinutes" = s.start_min,
        "EndTimeMinutes" = s.end_min,
        "HasBreak" = s.has_break,
        "RequiresBreakPunch" = s.has_break,
        "BreakStartMinutes" = s.break_start_min,
        "BreakEndMinutes" = s.break_end_min,
        "RequiredSessionsForFullDay" = s.required_full_sessions,
        "RequiredSessionsForHalfDay" = s.required_half_sessions,
        "AttendanceMode" = 'SessionBased',
        "ShiftCategory" = s.shift_code,
        "Active" = true,
        "UpdatedAt" = now()
    FROM _aadwika_shift_import s
    WHERE NOT sh."Deleted" AND sh."CompanyId" = v_company_id AND sh."StoreId" = v_store_id AND sh."Name" = s.shift_name;

    -- Replace only previous import-created employee-specific rules, not manual rules.
    UPDATE "EmployeeAttendanceShiftRules"
    SET "Deleted" = true, "Active" = false, "UpdatedAt" = now()
    WHERE "CompanyId" = v_company_id AND "StoreId" = v_store_id AND "CreatedBy" = 'attendance-final-import-v2';

    INSERT INTO "EmployeeAttendanceShiftRules" (
        "Id", "CreatedAt", "UpdatedAt", "Synced", "Deleted", "CompanyId", "CreatedBy", "StoreGroupId", "StoreId",
        "RuleType", "MatchValue", "EmployeeId", "AttendanceShiftId", "EffectiveFrom", "EffectiveTo", "Priority", "Active", "Notes"
    )
    SELECT gen_random_uuid(), now(), NULL, false, false, v_company_id, 'attendance-final-import-v2', v_store_group_id, v_store_id,
           'Employee', e.employee_code, emp."Id", sh."Id", GREATEST(e.joining_date, date '2024-04-01'), e.leaving_date, 100, true,
           'Stage 11D-24 employee-specific rule: ' || e.shift_name
    FROM _aadwika_emp_import e
    JOIN "Employees" emp ON NOT emp."Deleted" AND emp."CompanyId" = v_company_id AND emp."StoreId" = v_store_id AND lower(trim(COALESCE(emp."EmployeeCode",''))) = lower(trim(e.employee_code))
    JOIN "AttendanceShifts" sh ON NOT sh."Deleted" AND sh."CompanyId" = v_company_id AND sh."StoreId" = v_store_id AND sh."Name" = e.shift_name;
    GET DIAGNOSTICS v_rule_count = ROW_COUNT;

    -- Remove old punches created by this import batch for these employees/dates only.
    DELETE FROM "AttendancePunches" p
    USING _aadwika_att_import a
    JOIN "Employees" emp ON NOT emp."Deleted" AND emp."CompanyId" = v_company_id AND emp."StoreId" = v_store_id AND lower(trim(COALESCE(emp."EmployeeCode",''))) = lower(trim(a.employee_code))
    WHERE p."CompanyId" = v_company_id AND p."StoreId" = v_store_id AND p."EmployeeId" = emp."Id"
      AND p."CreatedBy" = 'attendance-final-import-v2'
      AND p."LocalPunchTime"::date = a.on_date;
    GET DIAGNOSTICS v_deleted_old_punches = ROW_COUNT;

    -- Upsert daily Attendance rows from status CSV + shift defaults.
    INSERT INTO "Attendance" (
        "Id", "EmployeeId", "OnDate", "Status", "CheckInTime", "BreakOutTime", "BreakInTime", "CheckOutTime", "EntryTime", "Remarks",
        "CreatedAt", "UpdatedAt", "Synced", "Deleted", "CompanyId", "CreatedBy", "StoreGroupId", "StoreId"
    )
    SELECT a.seed_id, emp."Id", a.on_date::timestamp, a.status_code,
           CASE WHEN a.status_code IN (0,2) THEN make_interval(mins => sh."StartTimeMinutes") ELSE NULL END,
           CASE WHEN a.status_code IN (0,2) AND sh."HasBreak" THEN make_interval(mins => COALESCE(sh."BreakStartMinutes", sh."StartTimeMinutes")) ELSE NULL END,
           CASE WHEN a.status_code = 0 AND sh."HasBreak" THEN make_interval(mins => COALESCE(sh."BreakEndMinutes", sh."EndTimeMinutes")) ELSE NULL END,
           CASE WHEN a.status_code = 0 THEN make_interval(mins => sh."EndTimeMinutes")
                WHEN a.status_code = 2 AND NOT sh."HasBreak" THEN make_interval(mins => sh."StartTimeMinutes" + ((sh."EndTimeMinutes" - sh."StartTimeMinutes") / 2))
                ELSE NULL END,
           CASE WHEN a.status_code IN (0,2) THEN to_char(time '00:00' + make_interval(mins => sh."StartTimeMinutes"), 'HH24:MI:SS') ELSE NULL END,
           left(COALESCE(a.remarks,'') || ' | Stage 11D-24 shift-aware import', 100),
           now(), now(), false, false, v_company_id, 'attendance-final-import-v2', v_store_group_id, v_store_id
    FROM _aadwika_att_import a
    JOIN "Employees" emp ON NOT emp."Deleted" AND emp."CompanyId" = v_company_id AND emp."StoreId" = v_store_id AND lower(trim(COALESCE(emp."EmployeeCode",''))) = lower(trim(a.employee_code))
    JOIN _aadwika_emp_import ei ON lower(trim(ei.employee_code)) = lower(trim(a.employee_code))
    JOIN "AttendanceShifts" sh ON NOT sh."Deleted" AND sh."CompanyId" = v_company_id AND sh."StoreId" = v_store_id AND sh."Name" = ei.shift_name
    WHERE a.on_date BETWEEN GREATEST(ei.joining_date, date '2024-04-01') AND COALESCE(ei.leaving_date, date '2026-06-23')
      AND NOT EXISTS (
          SELECT 1 FROM "Attendance" existing
          WHERE existing."CompanyId" = v_company_id
            AND existing."StoreId" = v_store_id
            AND existing."EmployeeId" = emp."Id"
            AND existing."OnDate"::date = a.on_date
            AND NOT existing."Deleted"
      );
    GET DIAGNOSTICS v_upserted_attendance = ROW_COUNT;

    -- Also update matching existing rows by employee/date when IDs were different due to older import.
    UPDATE "Attendance" att
    SET "Status" = a.status_code,
        "CheckInTime" = CASE WHEN a.status_code IN (0,2) THEN make_interval(mins => sh."StartTimeMinutes") ELSE NULL END,
        "BreakOutTime" = CASE WHEN a.status_code IN (0,2) AND sh."HasBreak" THEN make_interval(mins => COALESCE(sh."BreakStartMinutes", sh."StartTimeMinutes")) ELSE NULL END,
        "BreakInTime" = CASE WHEN a.status_code = 0 AND sh."HasBreak" THEN make_interval(mins => COALESCE(sh."BreakEndMinutes", sh."EndTimeMinutes")) ELSE NULL END,
        "CheckOutTime" = CASE WHEN a.status_code = 0 THEN make_interval(mins => sh."EndTimeMinutes")
                              WHEN a.status_code = 2 AND NOT sh."HasBreak" THEN make_interval(mins => sh."StartTimeMinutes" + ((sh."EndTimeMinutes" - sh."StartTimeMinutes") / 2))
                              ELSE NULL END,
        "EntryTime" = CASE WHEN a.status_code IN (0,2) THEN to_char(time '00:00' + make_interval(mins => sh."StartTimeMinutes"), 'HH24:MI:SS') ELSE NULL END,
        "Remarks" = left(COALESCE(a.remarks,'') || ' | Stage 11D-24 shift-aware import', 100),
        "UpdatedAt" = now(),
        "CreatedBy" = 'attendance-final-import-v2',
        "Deleted" = false
    FROM _aadwika_att_import a
    JOIN "Employees" emp ON NOT emp."Deleted" AND emp."CompanyId" = v_company_id AND emp."StoreId" = v_store_id AND lower(trim(COALESCE(emp."EmployeeCode",''))) = lower(trim(a.employee_code))
    JOIN _aadwika_emp_import ei ON lower(trim(ei.employee_code)) = lower(trim(a.employee_code))
    JOIN "AttendanceShifts" sh ON NOT sh."Deleted" AND sh."CompanyId" = v_company_id AND sh."StoreId" = v_store_id AND sh."Name" = ei.shift_name
    WHERE att."CompanyId" = v_company_id AND att."StoreId" = v_store_id AND att."EmployeeId" = emp."Id" AND att."OnDate"::date = a.on_date
      AND a.on_date BETWEEN GREATEST(ei.joining_date, date '2024-04-01') AND COALESCE(ei.leaving_date, date '2026-06-23');

    SELECT count(*) INTO v_absent_rows FROM _aadwika_att_import WHERE status_code = 1;

    -- Generate punch rows for Present and HalfDay statuses. All times are India local; UTC audit = local - 05:30.
    WITH base AS (
        SELECT emp."Id" AS employee_id, a.employee_code, a.on_date, a.status_code, sh."StartTimeMinutes" AS start_min, sh."EndTimeMinutes" AS end_min,
               sh."HasBreak" AS has_break, sh."BreakStartMinutes" AS break_start_min, sh."BreakEndMinutes" AS break_end_min,
               v_company_id AS company_id, v_store_group_id AS store_group_id, v_store_id AS store_id
        FROM _aadwika_att_import a
        JOIN "Employees" emp ON NOT emp."Deleted" AND emp."CompanyId" = v_company_id AND emp."StoreId" = v_store_id AND lower(trim(COALESCE(emp."EmployeeCode",''))) = lower(trim(a.employee_code))
        JOIN _aadwika_emp_import ei ON lower(trim(ei.employee_code)) = lower(trim(a.employee_code))
        JOIN "AttendanceShifts" sh ON NOT sh."Deleted" AND sh."CompanyId" = v_company_id AND sh."StoreId" = v_store_id AND sh."Name" = ei.shift_name
        WHERE a.status_code IN (0,2)
          AND a.on_date BETWEEN GREATEST(ei.joining_date, date '2024-04-01') AND COALESCE(ei.leaving_date, date '2026-06-23')
    ), punch_plan AS (
        SELECT *, 'CheckIn'::text AS punch_type, start_min AS punch_min FROM base
        UNION ALL SELECT *, 'BreakOut', COALESCE(break_start_min, start_min) FROM base WHERE has_break AND status_code IN (0,2)
        UNION ALL SELECT *, 'BreakIn', COALESCE(break_end_min, end_min) FROM base WHERE has_break AND status_code = 0
        UNION ALL SELECT *, 'CheckOut', end_min FROM base WHERE status_code = 0
        UNION ALL SELECT *, 'CheckOut', start_min + ((end_min - start_min) / 2) FROM base WHERE status_code = 2 AND NOT has_break
    )
    INSERT INTO "AttendancePunches" (
        "Id", "EmployeeId", "PunchType", "PunchTimeUtc", "LocalPunchTime", "Source", "DeviceId", "DeviceCode", "VerificationStatus",
        "PhotoProofPath", "ClientPunchId", "Latitude", "Longitude", "ConfidenceScore", "IsManual", "IsSynced", "DuplicateOfPunchId", "Reason", "Remarks",
        "CreatedAt", "UpdatedAt", "Synced", "Deleted", "CompanyId", "CreatedBy", "StoreGroupId", "StoreId"
    )
    SELECT gen_random_uuid(), employee_id, punch_type,
           (on_date::timestamp + make_interval(mins => punch_min) - interval '5 hours 30 minutes') AS punch_time_utc,
           (on_date::timestamp + make_interval(mins => punch_min)) AS local_punch_time,
           'ExcelImport', NULL, NULL, 'Imported', NULL,
           'attendance-final-v2:' || employee_code || ':' || on_date::text || ':' || punch_type,
           NULL, NULL, NULL, false, true, NULL, 'Historical attendance import',
           'Stage 11D-24 shift-aware import generated from attendance status sheet',
           now(), now(), false, false, company_id, 'attendance-final-import-v2', store_group_id, store_id
    FROM punch_plan;
    GET DIAGNOSTICS v_inserted_punches = ROW_COUNT;

    RAISE NOTICE 'Stage 11D-24 import complete: employees inserted %, employees updated %, new shifts %, shift rules %, old import punches deleted %, attendance upserted/updated %, punches inserted %, absent rows source %',
        v_inserted_employees, v_updated_employees, v_shift_count, v_rule_count, v_deleted_old_punches, v_upserted_attendance, v_inserted_punches, v_absent_rows;
END $$;

\echo 'Validation summary after Stage 11D-24 import'
SELECT 'Employees' AS metric, count(*) AS value FROM "Employees" e
JOIN "Companies" c ON c."Id" = e."CompanyId"
JOIN "Stores" s ON s."Id" = e."StoreId"
WHERE c."Name" ILIKE '%Aadwika%Fashion%' AND s."Name" ILIKE '%Smart%Menswear%' AND NOT e."Deleted"
UNION ALL
SELECT 'Employee shift rules', count(*) FROM "EmployeeAttendanceShiftRules" r
JOIN "Companies" c ON c."Id" = r."CompanyId"
JOIN "Stores" s ON s."Id" = r."StoreId"
WHERE c."Name" ILIKE '%Aadwika%Fashion%' AND s."Name" ILIKE '%Smart%Menswear%' AND NOT r."Deleted"
UNION ALL
SELECT 'Imported attendance rows', count(*) FROM "Attendance" a
JOIN "Companies" c ON c."Id" = a."CompanyId"
JOIN "Stores" s ON s."Id" = a."StoreId"
WHERE c."Name" ILIKE '%Aadwika%Fashion%' AND s."Name" ILIKE '%Smart%Menswear%' AND NOT a."Deleted" AND a."CreatedBy" = 'attendance-final-import-v2'
UNION ALL
SELECT 'Imported punch rows', count(*) FROM "AttendancePunches" p
JOIN "Companies" c ON c."Id" = p."CompanyId"
JOIN "Stores" s ON s."Id" = p."StoreId"
WHERE c."Name" ILIKE '%Aadwika%Fashion%' AND s."Name" ILIKE '%Smart%Menswear%' AND NOT p."Deleted" AND p."CreatedBy" = 'attendance-final-import-v2';
