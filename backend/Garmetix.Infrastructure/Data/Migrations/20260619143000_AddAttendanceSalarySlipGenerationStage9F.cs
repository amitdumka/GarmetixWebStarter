using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Garmetix.Infrastructure.Data.Migrations;

public partial class AddAttendanceSalarySlipGenerationStage9F : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
        ALTER TABLE "AttendanceSalarySlipDrafts" ADD COLUMN IF NOT EXISTS "GeneratedSalaryPaySlipId" uuid NULL;
        ALTER TABLE "AttendanceSalarySlipDrafts" ADD COLUMN IF NOT EXISTS "GeneratedAtUtc" timestamp without time zone NULL;
        ALTER TABLE "AttendanceSalarySlipDrafts" ADD COLUMN IF NOT EXISTS "GeneratedBy" character varying(120) NULL;
        CREATE INDEX IF NOT EXISTS "IX_AttendanceSalarySlipDrafts_CompanyId_StoreId_Year_Month_PayrollPostStatus" ON "AttendanceSalarySlipDrafts" ("CompanyId", "StoreId", "Year", "Month", "PayrollPostStatus");
        CREATE INDEX IF NOT EXISTS "IX_AttendanceSalarySlipDrafts_GeneratedSalaryPaySlipId" ON "AttendanceSalarySlipDrafts" ("GeneratedSalaryPaySlipId");
        """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
        DROP INDEX IF EXISTS "IX_AttendanceSalarySlipDrafts_GeneratedSalaryPaySlipId";
        DROP INDEX IF EXISTS "IX_AttendanceSalarySlipDrafts_CompanyId_StoreId_Year_Month_PayrollPostStatus";
        ALTER TABLE "AttendanceSalarySlipDrafts" DROP COLUMN IF EXISTS "GeneratedBy";
        ALTER TABLE "AttendanceSalarySlipDrafts" DROP COLUMN IF EXISTS "GeneratedAtUtc";
        ALTER TABLE "AttendanceSalarySlipDrafts" DROP COLUMN IF EXISTS "GeneratedSalaryPaySlipId";
        """);
    }
}
