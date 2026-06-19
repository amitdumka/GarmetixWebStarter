using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Garmetix.Infrastructure.Data.Migrations;

public partial class AddAttendancePayrollReviewStage9D : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
        CREATE TABLE IF NOT EXISTS "AttendancePayrollReviews" (
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
            "PayableDays" numeric(18,2) NOT NULL DEFAULT 0,
            "DeductionDays" numeric(18,2) NOT NULL DEFAULT 0,
            "WorkingMinutes" integer NOT NULL DEFAULT 0,
            "OvertimeMinutes" integer NOT NULL DEFAULT 0,
            "EstimatedDailyRate" numeric(18,2) NOT NULL DEFAULT 0,
            "EstimatedGrossPay" numeric(18,2) NOT NULL DEFAULT 0,
            "ReviewStatus" character varying(40) NOT NULL DEFAULT 'Draft',
            "PayrollActionStatus" character varying(40) NOT NULL DEFAULT 'NotPosted',
            "Locked" boolean NOT NULL DEFAULT false,
            "LockedAtUtc" timestamp without time zone NULL,
            "ReviewedBy" character varying(120) NULL,
            "ReviewedAtUtc" timestamp without time zone NULL,
            "Notes" character varying(300) NULL,
            "SourceSummaryJson" text NULL,
            CONSTRAINT "PK_AttendancePayrollReviews" PRIMARY KEY ("Id")
        );

        CREATE INDEX IF NOT EXISTS "IX_AttendancePayrollReviews_CompanyId_StoreId_EmployeeId_Year_Month" ON "AttendancePayrollReviews" ("CompanyId", "StoreId", "EmployeeId", "Year", "Month");
        CREATE INDEX IF NOT EXISTS "IX_AttendancePayrollReviews_CompanyId_StoreId_Year_Month_ReviewStatus" ON "AttendancePayrollReviews" ("CompanyId", "StoreId", "Year", "Month", "ReviewStatus");
        """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
        DROP TABLE IF EXISTS "AttendancePayrollReviews";
        """);
    }
}
