using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Garmetix.Infrastructure.Data.Migrations;

public partial class AddAttendanceKioskPhotoProofStage9B : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
        CREATE TABLE IF NOT EXISTS "AttendancePhotoProofs" (
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
            "DeviceId" uuid NULL,
            "DeviceCode" character varying(40) NULL,
            "ClientPunchId" character varying(120) NULL,
            "ProofPath" character varying(500) NOT NULL DEFAULT '',
            "ContentType" character varying(80) NOT NULL DEFAULT 'image/jpeg',
            "SizeBytes" bigint NOT NULL DEFAULT 0,
            "CapturedAtUtc" timestamp without time zone NOT NULL DEFAULT now(),
            "UploadedAtUtc" timestamp without time zone NOT NULL DEFAULT now(),
            "RetentionUntilUtc" timestamp without time zone NULL,
            "VerificationStatus" character varying(40) NOT NULL DEFAULT 'PhotoProofOnly',
            "Remarks" character varying(300) NULL,
            CONSTRAINT "PK_AttendancePhotoProofs" PRIMARY KEY ("Id")
        );

        CREATE TABLE IF NOT EXISTS "AttendanceKioskSyncBatches" (
            "Id" uuid NOT NULL,
            "CreatedAt" timestamp without time zone NOT NULL DEFAULT now(),
            "UpdatedAt" timestamp without time zone NULL,
            "Synced" boolean NOT NULL DEFAULT false,
            "Deleted" boolean NOT NULL DEFAULT false,
            "CompanyId" uuid NOT NULL,
            "CreatedBy" text NULL,
            "StoreGroupId" uuid NOT NULL,
            "StoreId" uuid NOT NULL,
            "DeviceId" uuid NOT NULL,
            "DeviceCode" character varying(40) NOT NULL DEFAULT '',
            "BatchClientId" character varying(120) NULL,
            "TotalCount" integer NOT NULL DEFAULT 0,
            "AcceptedCount" integer NOT NULL DEFAULT 0,
            "DuplicateCount" integer NOT NULL DEFAULT 0,
            "FailedCount" integer NOT NULL DEFAULT 0,
            "Status" character varying(40) NOT NULL DEFAULT 'Received',
            "ReceivedAtUtc" timestamp without time zone NOT NULL DEFAULT now(),
            "CompletedAtUtc" timestamp without time zone NULL,
            "ResultJson" text NULL,
            CONSTRAINT "PK_AttendanceKioskSyncBatches" PRIMARY KEY ("Id")
        );

        CREATE INDEX IF NOT EXISTS "IX_AttendancePhotoProofs_CompanyId_StoreId_EmployeeId_CapturedAtUtc" ON "AttendancePhotoProofs" ("CompanyId", "StoreId", "EmployeeId", "CapturedAtUtc");
        CREATE INDEX IF NOT EXISTS "IX_AttendancePhotoProofs_CompanyId_ClientPunchId" ON "AttendancePhotoProofs" ("CompanyId", "ClientPunchId");
        CREATE INDEX IF NOT EXISTS "IX_AttendanceKioskSyncBatches_CompanyId_StoreId_DeviceId_ReceivedAtUtc" ON "AttendanceKioskSyncBatches" ("CompanyId", "StoreId", "DeviceId", "ReceivedAtUtc");
        """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
        DROP TABLE IF EXISTS "AttendanceKioskSyncBatches";
        DROP TABLE IF EXISTS "AttendancePhotoProofs";
        """);
    }
}
