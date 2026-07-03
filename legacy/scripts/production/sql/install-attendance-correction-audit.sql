\echo 'Installing attendance correction audit triggers'

CREATE TABLE IF NOT EXISTS "AttendanceCorrectionAudits" (
    "Id" uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    "CreatedAt" timestamp without time zone NOT NULL DEFAULT now(),
    "UpdatedAt" timestamp without time zone NOT NULL DEFAULT now(),
    "Synced" boolean NOT NULL DEFAULT false,
    "Deleted" boolean NOT NULL DEFAULT false,
    "CompanyId" uuid NULL,
    "StoreGroupId" uuid NULL,
    "StoreId" uuid NULL,
    "EmployeeId" uuid NULL,
    "AttendanceId" uuid NULL,
    "AttendancePunchId" uuid NULL,
    "OnDate" date NULL,
    "AuditSource" varchar(40) NOT NULL,
    "Action" varchar(40) NOT NULL,
    "ChangedFields" text NOT NULL DEFAULT '',
    "OldValuesJson" jsonb NULL,
    "NewValuesJson" jsonb NULL,
    "ChangedBy" varchar(120) NULL,
    "Reason" varchar(300) NULL,
    "Remarks" varchar(500) NULL,
    "OccurredAtUtc" timestamp without time zone NOT NULL DEFAULT (now() AT TIME ZONE 'UTC')
);

CREATE INDEX IF NOT EXISTS "IX_AttendanceCorrectionAudits_Company_Store_Date"
    ON "AttendanceCorrectionAudits" ("CompanyId", "StoreId", "OnDate");

CREATE INDEX IF NOT EXISTS "IX_AttendanceCorrectionAudits_Employee_Date"
    ON "AttendanceCorrectionAudits" ("EmployeeId", "OnDate");

CREATE INDEX IF NOT EXISTS "IX_AttendanceCorrectionAudits_Attendance"
    ON "AttendanceCorrectionAudits" ("AttendanceId");

CREATE INDEX IF NOT EXISTS "IX_AttendanceCorrectionAudits_Punch"
    ON "AttendanceCorrectionAudits" ("AttendancePunchId");

CREATE OR REPLACE FUNCTION garmetix_attendance_daily_audit_trigger()
RETURNS trigger AS $$
DECLARE
    changed text[] := ARRAY[]::text[];
BEGIN
    IF (OLD."CheckInTime" IS DISTINCT FROM NEW."CheckInTime") THEN changed := array_append(changed, 'CheckInTime'); END IF;
    IF (OLD."BreakOutTime" IS DISTINCT FROM NEW."BreakOutTime") THEN changed := array_append(changed, 'BreakOutTime'); END IF;
    IF (OLD."BreakInTime" IS DISTINCT FROM NEW."BreakInTime") THEN changed := array_append(changed, 'BreakInTime'); END IF;
    IF (OLD."CheckOutTime" IS DISTINCT FROM NEW."CheckOutTime") THEN changed := array_append(changed, 'CheckOutTime'); END IF;
    IF (OLD."Status" IS DISTINCT FROM NEW."Status") THEN changed := array_append(changed, 'Status'); END IF;
    IF (OLD."Remarks" IS DISTINCT FROM NEW."Remarks") THEN changed := array_append(changed, 'Remarks'); END IF;

    IF array_length(changed, 1) IS NULL THEN
        RETURN NEW;
    END IF;

    INSERT INTO "AttendanceCorrectionAudits" (
        "CompanyId", "StoreGroupId", "StoreId", "EmployeeId", "AttendanceId", "OnDate",
        "AuditSource", "Action", "ChangedFields", "OldValuesJson", "NewValuesJson",
        "ChangedBy", "Reason", "Remarks", "OccurredAtUtc"
    ) VALUES (
        NEW."CompanyId", NEW."StoreGroupId", NEW."StoreId", NEW."EmployeeId", NEW."Id", NEW."OnDate"::date,
        'DailyAttendance', TG_OP, array_to_string(changed, ','),
        jsonb_build_object(
            'CheckInTime', OLD."CheckInTime", 'BreakOutTime', OLD."BreakOutTime", 'BreakInTime', OLD."BreakInTime", 'CheckOutTime', OLD."CheckOutTime", 'Status', OLD."Status", 'Remarks', OLD."Remarks"
        ),
        jsonb_build_object(
            'CheckInTime', NEW."CheckInTime", 'BreakOutTime', NEW."BreakOutTime", 'BreakInTime', NEW."BreakInTime", 'CheckOutTime', NEW."CheckOutTime", 'Status', NEW."Status", 'Remarks', NEW."Remarks"
        ),
        current_user,
        NULL,
        'Automatic audit: daily attendance timing/status changed',
        now() AT TIME ZONE 'UTC'
    );

    RETURN NEW;
END;
$$ LANGUAGE plpgsql;

CREATE OR REPLACE FUNCTION garmetix_attendance_punch_audit_trigger()
RETURNS trigger AS $$
DECLARE
    changed text[] := ARRAY[]::text[];
BEGIN
    IF (OLD."PunchType" IS DISTINCT FROM NEW."PunchType") THEN changed := array_append(changed, 'PunchType'); END IF;
    IF (OLD."PunchTimeUtc" IS DISTINCT FROM NEW."PunchTimeUtc") THEN changed := array_append(changed, 'PunchTimeUtc'); END IF;
    IF (OLD."LocalPunchTime" IS DISTINCT FROM NEW."LocalPunchTime") THEN changed := array_append(changed, 'LocalPunchTime'); END IF;
    IF (OLD."VerificationStatus" IS DISTINCT FROM NEW."VerificationStatus") THEN changed := array_append(changed, 'VerificationStatus'); END IF;
    IF (OLD."Reason" IS DISTINCT FROM NEW."Reason") THEN changed := array_append(changed, 'Reason'); END IF;
    IF (OLD."Remarks" IS DISTINCT FROM NEW."Remarks") THEN changed := array_append(changed, 'Remarks'); END IF;
    IF (OLD."Deleted" IS DISTINCT FROM NEW."Deleted") THEN changed := array_append(changed, 'Deleted'); END IF;

    IF array_length(changed, 1) IS NULL THEN
        RETURN NEW;
    END IF;

    INSERT INTO "AttendanceCorrectionAudits" (
        "CompanyId", "StoreGroupId", "StoreId", "EmployeeId", "AttendancePunchId", "OnDate",
        "AuditSource", "Action", "ChangedFields", "OldValuesJson", "NewValuesJson",
        "ChangedBy", "Reason", "Remarks", "OccurredAtUtc"
    ) VALUES (
        NEW."CompanyId", NEW."StoreGroupId", NEW."StoreId", NEW."EmployeeId", NEW."Id", NEW."LocalPunchTime"::date,
        'AttendancePunch', TG_OP, array_to_string(changed, ','),
        jsonb_build_object(
            'PunchType', OLD."PunchType", 'PunchTimeUtc', OLD."PunchTimeUtc", 'LocalPunchTime', OLD."LocalPunchTime", 'VerificationStatus', OLD."VerificationStatus", 'Reason', OLD."Reason", 'Remarks', OLD."Remarks", 'Deleted', OLD."Deleted"
        ),
        jsonb_build_object(
            'PunchType', NEW."PunchType", 'PunchTimeUtc', NEW."PunchTimeUtc", 'LocalPunchTime', NEW."LocalPunchTime", 'VerificationStatus', NEW."VerificationStatus", 'Reason', NEW."Reason", 'Remarks', NEW."Remarks", 'Deleted', NEW."Deleted"
        ),
        current_user,
        NEW."Reason",
        'Automatic audit: punch changed/deleted',
        now() AT TIME ZONE 'UTC'
    );

    RETURN NEW;
END;
$$ LANGUAGE plpgsql;

DROP TRIGGER IF EXISTS "TR_Attendance_DailyCorrectionAudit" ON "Attendance";
CREATE TRIGGER "TR_Attendance_DailyCorrectionAudit"
AFTER UPDATE ON "Attendance"
FOR EACH ROW
EXECUTE FUNCTION garmetix_attendance_daily_audit_trigger();

DROP TRIGGER IF EXISTS "TR_AttendancePunches_CorrectionAudit" ON "AttendancePunches";
CREATE TRIGGER "TR_AttendancePunches_CorrectionAudit"
AFTER UPDATE ON "AttendancePunches"
FOR EACH ROW
EXECUTE FUNCTION garmetix_attendance_punch_audit_trigger();

\echo 'Attendance correction audit triggers installed.'
