using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Garmetix.Infrastructure.Data.Migrations;

public partial class AddAttendanceSalarySlipDraftStage9E : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
        CREATE TABLE IF NOT EXISTS "AttendanceSalarySlipDrafts" (
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
            "PayrollReviewId" uuid NULL,
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
            "MonthlySalary" numeric(18,2) NOT NULL DEFAULT 0,
            "DailyRate" numeric(18,2) NOT NULL DEFAULT 0,
            "AttendanceGrossPreview" numeric(18,2) NOT NULL DEFAULT 0,
            "AttendanceDeductionPreview" numeric(18,2) NOT NULL DEFAULT 0,
            "BonusPreview" numeric(18,2) NOT NULL DEFAULT 0,
            "LeaveEncashmentPreview" numeric(18,2) NOT NULL DEFAULT 0,
            "SalaryAdvanceRecoveryPreview" numeric(18,2) NOT NULL DEFAULT 0,
            "PfEmployeePreview" numeric(18,2) NOT NULL DEFAULT 0,
            "GratuityPreview" numeric(18,2) NOT NULL DEFAULT 0,
            "OtherDeductionPreview" numeric(18,2) NOT NULL DEFAULT 0,
            "NetPayPreview" numeric(18,2) NOT NULL DEFAULT 0,
            "DraftStatus" character varying(40) NOT NULL DEFAULT 'Draft',
            "PayrollPostStatus" character varying(40) NOT NULL DEFAULT 'PreviewOnly',
            "PreparedAtUtc" timestamp without time zone NULL,
            "PreparedBy" character varying(120) NULL,
            "MarkedReadyAtUtc" timestamp without time zone NULL,
            "MarkedReadyBy" character varying(120) NULL,
            "Notes" character varying(300) NULL,
            "SourceJson" text NULL,
            CONSTRAINT "PK_AttendanceSalarySlipDrafts" PRIMARY KEY ("Id")
        );

        CREATE INDEX IF NOT EXISTS "IX_AttendanceSalarySlipDrafts_CompanyId_StoreId_EmployeeId_Year_Month" ON "AttendanceSalarySlipDrafts" ("CompanyId", "StoreId", "EmployeeId", "Year", "Month");
        CREATE INDEX IF NOT EXISTS "IX_AttendanceSalarySlipDrafts_CompanyId_StoreId_Year_Month_DraftStatus" ON "AttendanceSalarySlipDrafts" ("CompanyId", "StoreId", "Year", "Month", "DraftStatus");
        """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
        DROP TABLE IF EXISTS "AttendanceSalarySlipDrafts";
        """);
    }
}
