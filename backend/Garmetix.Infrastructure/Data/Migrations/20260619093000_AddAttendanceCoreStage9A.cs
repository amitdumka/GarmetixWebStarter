using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Garmetix.Infrastructure.Data.Migrations;

public partial class AddAttendanceCoreStage9A : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            CREATE TABLE IF NOT EXISTS "AttendanceDevices" (
                "Id" uuid NOT NULL,
                "CreatedAt" timestamp without time zone NOT NULL DEFAULT now(),
                "UpdatedAt" timestamp without time zone NULL,
                "Synced" boolean NOT NULL DEFAULT false,
                "Deleted" boolean NOT NULL DEFAULT false,
                "CompanyId" uuid NOT NULL,
                "CreatedBy" text NULL,
                "StoreGroupId" uuid NOT NULL,
                "StoreId" uuid NOT NULL,
                "DeviceCode" character varying(40) NOT NULL DEFAULT '',
                "DeviceName" character varying(120) NOT NULL DEFAULT '',
                "DeviceType" character varying(40) NOT NULL DEFAULT 'WebKiosk',
                "DeviceTokenHash" character varying(120) NOT NULL DEFAULT '',
                "Status" character varying(40) NOT NULL DEFAULT 'Active',
                "AppVersion" character varying(80) NULL,
                "Notes" character varying(200) NULL,
                "RegisteredAtUtc" timestamp without time zone NOT NULL DEFAULT now(),
                "LastSeenAtUtc" timestamp without time zone NULL,
                "RevokedAtUtc" timestamp without time zone NULL,
                "RegisteredByUserId" uuid NULL,
                "RegisteredByUserName" character varying(120) NULL,
                CONSTRAINT "PK_AttendanceDevices" PRIMARY KEY ("Id")
            );

            CREATE TABLE IF NOT EXISTS "AttendancePunches" (
                "Id" uuid NOT NULL,
                "CreatedAt" timestamp without time zone NOT NULL DEFAULT now(),
                "UpdatedAt" timestamp without time zone NULL,
                "Synced" boolean NOT NULL DEFAULT false,
                "Deleted" boolean NOT NULL DEFAULT false,
                "CompanyId" uuid NOT NULL,
                "CreatedBy" text NULL,
                "StoreGroupId" uuid NOT NULL,
                "StoreId" uuid NOT NULL,
                "EmployeeId" uuid NOT NULL,
                "PunchType" character varying(20) NOT NULL DEFAULT 'CheckIn',
                "PunchTimeUtc" timestamp without time zone NOT NULL DEFAULT now(),
                "LocalPunchTime" timestamp without time zone NOT NULL DEFAULT now(),
                "Source" character varying(40) NOT NULL DEFAULT 'Manual',
                "DeviceId" uuid NULL,
                "DeviceCode" character varying(40) NULL,
                "VerificationStatus" character varying(40) NOT NULL DEFAULT 'ManualApproved',
                "PhotoProofPath" character varying(300) NULL,
                "ClientPunchId" character varying(120) NULL,
                "Latitude" numeric(12,8) NULL,
                "Longitude" numeric(12,8) NULL,
                "ConfidenceScore" numeric(7,4) NULL,
                "IsManual" boolean NOT NULL DEFAULT false,
                "IsSynced" boolean NOT NULL DEFAULT true,
                "DuplicateOfPunchId" uuid NULL,
                "Reason" character varying(300) NULL,
                "Remarks" character varying(300) NULL,
                CONSTRAINT "PK_AttendancePunches" PRIMARY KEY ("Id")
            );

            CREATE TABLE IF NOT EXISTS "AttendanceShifts" (
                "Id" uuid NOT NULL,
                "CreatedAt" timestamp without time zone NOT NULL DEFAULT now(),
                "UpdatedAt" timestamp without time zone NULL,
                "Synced" boolean NOT NULL DEFAULT false,
                "Deleted" boolean NOT NULL DEFAULT false,
                "CompanyId" uuid NOT NULL,
                "CreatedBy" text NULL,
                "StoreGroupId" uuid NOT NULL,
                "StoreId" uuid NOT NULL,
                "Name" character varying(120) NOT NULL DEFAULT 'Default Shift',
                "StartTimeMinutes" integer NOT NULL DEFAULT 600,
                "EndTimeMinutes" integer NOT NULL DEFAULT 1200,
                "GraceMinutes" integer NOT NULL DEFAULT 10,
                "LateAfterMinutes" integer NOT NULL DEFAULT 10,
                "HalfDayAfterMinutes" integer NOT NULL DEFAULT 750,
                "MinimumFullDayMinutes" integer NOT NULL DEFAULT 480,
                "MinimumHalfDayMinutes" integer NOT NULL DEFAULT 240,
                "OvertimeAfterMinutes" integer NOT NULL DEFAULT 540,
                "AutoCheckoutEnabled" boolean NOT NULL DEFAULT false,
                "AutoCheckoutTimeMinutes" integer NULL,
                "WeeklyOffDays" character varying(80) NOT NULL DEFAULT 'Sunday',
                "Active" boolean NOT NULL DEFAULT true,
                CONSTRAINT "PK_AttendanceShifts" PRIMARY KEY ("Id")
            );

            CREATE TABLE IF NOT EXISTS "AttendancePolicies" (
                "Id" uuid NOT NULL,
                "CreatedAt" timestamp without time zone NOT NULL DEFAULT now(),
                "UpdatedAt" timestamp without time zone NULL,
                "Synced" boolean NOT NULL DEFAULT false,
                "Deleted" boolean NOT NULL DEFAULT false,
                "CompanyId" uuid NOT NULL,
                "CreatedBy" text NULL,
                "StoreGroupId" uuid NOT NULL,
                "StoreId" uuid NOT NULL,
                "Name" character varying(120) NOT NULL DEFAULT 'Default Attendance Policy',
                "GraceMinutes" integer NOT NULL DEFAULT 10,
                "LateAfterMinutes" integer NOT NULL DEFAULT 10,
                "HalfDayAfterMinutes" integer NOT NULL DEFAULT 750,
                "MinimumFullDayMinutes" integer NOT NULL DEFAULT 480,
                "MinimumHalfDayMinutes" integer NOT NULL DEFAULT 240,
                "OvertimeAfterMinutes" integer NOT NULL DEFAULT 540,
                "AutoCheckoutEnabled" boolean NOT NULL DEFAULT false,
                "AutoCheckoutAfterMinutes" integer NULL,
                "DuplicateWindowMinutes" integer NOT NULL DEFAULT 5,
                "Active" boolean NOT NULL DEFAULT true,
                CONSTRAINT "PK_AttendancePolicies" PRIMARY KEY ("Id")
            );

            CREATE TABLE IF NOT EXISTS "EmployeeBiometricEnrollments" (
                "Id" uuid NOT NULL,
                "CreatedAt" timestamp without time zone NOT NULL DEFAULT now(),
                "UpdatedAt" timestamp without time zone NULL,
                "Synced" boolean NOT NULL DEFAULT false,
                "Deleted" boolean NOT NULL DEFAULT false,
                "CompanyId" uuid NOT NULL,
                "CreatedBy" text NULL,
                "StoreGroupId" uuid NOT NULL,
                "StoreId" uuid NOT NULL,
                "EmployeeId" uuid NOT NULL,
                "ConsentGiven" boolean NOT NULL DEFAULT false,
                "ConsentAtUtc" timestamp without time zone NULL,
                "FacePhotoPath" character varying(300) NULL,
                "FaceTemplateRef" character varying(300) NULL,
                "FingerprintTemplateRef" character varying(300) NULL,
                "WebAuthnCredentialId" character varying(300) NULL,
                "EnrollmentStatus" character varying(40) NOT NULL DEFAULT 'NotEnrolled',
                "EnrolledAtUtc" timestamp without time zone NULL,
                "RevokedAtUtc" timestamp without time zone NULL,
                "RevokedReason" character varying(300) NULL,
                "Notes" character varying(300) NULL,
                CONSTRAINT "PK_EmployeeBiometricEnrollments" PRIMARY KEY ("Id")
            );

            CREATE TABLE IF NOT EXISTS "AttendanceRegularizationRequests" (
                "Id" uuid NOT NULL,
                "CreatedAt" timestamp without time zone NOT NULL DEFAULT now(),
                "UpdatedAt" timestamp without time zone NULL,
                "Synced" boolean NOT NULL DEFAULT false,
                "Deleted" boolean NOT NULL DEFAULT false,
                "CompanyId" uuid NOT NULL,
                "CreatedBy" text NULL,
                "StoreGroupId" uuid NOT NULL,
                "StoreId" uuid NOT NULL,
                "EmployeeId" uuid NOT NULL,
                "AttendancePunchId" uuid NULL,
                "RequestType" character varying(40) NOT NULL DEFAULT 'MissedPunch',
                "RequestedPunchType" character varying(20) NOT NULL DEFAULT 'CheckIn',
                "RequestedPunchTimeUtc" timestamp without time zone NULL,
                "RequestedLocalPunchTime" timestamp without time zone NULL,
                "Reason" character varying(300) NOT NULL DEFAULT '',
                "Status" character varying(40) NOT NULL DEFAULT 'Pending',
                "RequestedBy" character varying(120) NULL,
                "ApprovedBy" character varying(120) NULL,
                "ApprovedAtUtc" timestamp without time zone NULL,
                "RejectionReason" character varying(300) NULL,
                CONSTRAINT "PK_AttendanceRegularizationRequests" PRIMARY KEY ("Id")
            );

            CREATE TABLE IF NOT EXISTS "AttendanceApprovals" (
                "Id" uuid NOT NULL,
                "CreatedAt" timestamp without time zone NOT NULL DEFAULT now(),
                "UpdatedAt" timestamp without time zone NULL,
                "Synced" boolean NOT NULL DEFAULT false,
                "Deleted" boolean NOT NULL DEFAULT false,
                "CompanyId" uuid NOT NULL,
                "CreatedBy" text NULL,
                "StoreGroupId" uuid NOT NULL,
                "StoreId" uuid NOT NULL,
                "RequestId" uuid NOT NULL,
                "Approved" boolean NOT NULL DEFAULT false,
                "Decision" character varying(40) NOT NULL DEFAULT 'Approved',
                "Remarks" character varying(300) NULL,
                "ApprovedBy" character varying(120) NULL,
                "ApprovedAtUtc" timestamp without time zone NOT NULL DEFAULT now(),
                CONSTRAINT "PK_AttendanceApprovals" PRIMARY KEY ("Id")
            );

            CREATE TABLE IF NOT EXISTS "AttendanceMonthlySummaries" (
                "Id" uuid NOT NULL,
                "CreatedAt" timestamp without time zone NOT NULL DEFAULT now(),
                "UpdatedAt" timestamp without time zone NULL,
                "Synced" boolean NOT NULL DEFAULT false,
                "Deleted" boolean NOT NULL DEFAULT false,
                "CompanyId" uuid NOT NULL,
                "CreatedBy" text NULL,
                "StoreGroupId" uuid NOT NULL,
                "StoreId" uuid NOT NULL,
                "EmployeeId" uuid NOT NULL,
                "Year" integer NOT NULL,
                "Month" integer NOT NULL,
                "PresentDays" numeric(18,2) NOT NULL DEFAULT 0,
                "AbsentDays" numeric(18,2) NOT NULL DEFAULT 0,
                "LateDays" numeric(18,2) NOT NULL DEFAULT 0,
                "HalfDays" numeric(18,2) NOT NULL DEFAULT 0,
                "LeaveDays" numeric(18,2) NOT NULL DEFAULT 0,
                "WorkingMinutes" integer NOT NULL DEFAULT 0,
                "OvertimeMinutes" integer NOT NULL DEFAULT 0,
                "Locked" boolean NOT NULL DEFAULT false,
                "LockedAtUtc" timestamp without time zone NULL,
                "LockedBy" character varying(120) NULL,
                "SummaryJson" text NULL,
                CONSTRAINT "PK_AttendanceMonthlySummaries" PRIMARY KEY ("Id")
            );

            CREATE INDEX IF NOT EXISTS "IX_AttendanceDevices_CompanyId_StoreId_DeviceCode" ON "AttendanceDevices" ("CompanyId", "StoreId", "DeviceCode");
            CREATE INDEX IF NOT EXISTS "IX_AttendanceDevices_CompanyId_StoreId_Status" ON "AttendanceDevices" ("CompanyId", "StoreId", "Status");
            CREATE INDEX IF NOT EXISTS "IX_AttendancePunches_CompanyId_StoreId_EmployeeId_LocalPunchTime" ON "AttendancePunches" ("CompanyId", "StoreId", "EmployeeId", "LocalPunchTime");
            CREATE INDEX IF NOT EXISTS "IX_AttendancePunches_CompanyId_StoreId_DeviceId_PunchTimeUtc" ON "AttendancePunches" ("CompanyId", "StoreId", "DeviceId", "PunchTimeUtc");
            CREATE INDEX IF NOT EXISTS "IX_AttendancePunches_CompanyId_ClientPunchId" ON "AttendancePunches" ("CompanyId", "ClientPunchId");
            CREATE INDEX IF NOT EXISTS "IX_AttendanceShifts_CompanyId_StoreId_Active" ON "AttendanceShifts" ("CompanyId", "StoreId", "Active");
            CREATE INDEX IF NOT EXISTS "IX_AttendancePolicies_CompanyId_StoreId_Active" ON "AttendancePolicies" ("CompanyId", "StoreId", "Active");
            CREATE INDEX IF NOT EXISTS "IX_EmployeeBiometricEnrollments_CompanyId_StoreId_EmployeeId" ON "EmployeeBiometricEnrollments" ("CompanyId", "StoreId", "EmployeeId");
            CREATE INDEX IF NOT EXISTS "IX_AttendanceRegularizationRequests_CompanyId_StoreId_EmployeeId_Status" ON "AttendanceRegularizationRequests" ("CompanyId", "StoreId", "EmployeeId", "Status");
            CREATE INDEX IF NOT EXISTS "IX_AttendanceApprovals_CompanyId_StoreId_RequestId" ON "AttendanceApprovals" ("CompanyId", "StoreId", "RequestId");
            CREATE INDEX IF NOT EXISTS "IX_AttendanceMonthlySummaries_CompanyId_StoreId_EmployeeId_Year_Month" ON "AttendanceMonthlySummaries" ("CompanyId", "StoreId", "EmployeeId", "Year", "Month");
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            DROP TABLE IF EXISTS "AttendanceApprovals";
            DROP TABLE IF EXISTS "AttendanceRegularizationRequests";
            DROP TABLE IF EXISTS "EmployeeBiometricEnrollments";
            DROP TABLE IF EXISTS "AttendanceMonthlySummaries";
            DROP TABLE IF EXISTS "AttendancePunches";
            DROP TABLE IF EXISTS "AttendancePolicies";
            DROP TABLE IF EXISTS "AttendanceShifts";
            DROP TABLE IF EXISTS "AttendanceDevices";
            """);
    }
}
