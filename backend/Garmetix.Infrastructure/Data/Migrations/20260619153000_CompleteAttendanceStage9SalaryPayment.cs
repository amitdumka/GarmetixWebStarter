using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Garmetix.Infrastructure.Data.Migrations;

public partial class CompleteAttendanceStage9SalaryPayment : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
        ALTER TABLE "AttendanceSalarySlipDrafts" ADD COLUMN IF NOT EXISTS "GeneratedSalaryPaymentId" uuid NULL;
        ALTER TABLE "AttendanceSalarySlipDrafts" ADD COLUMN IF NOT EXISTS "SalaryPaidAtUtc" timestamp without time zone NULL;
        ALTER TABLE "AttendanceSalarySlipDrafts" ADD COLUMN IF NOT EXISTS "SalaryPaidBy" character varying(120) NULL;
        ALTER TABLE "AttendanceSalarySlipDrafts" ADD COLUMN IF NOT EXISTS "PaymentPostStatus" character varying(40) NOT NULL DEFAULT 'NotPaid';
        CREATE INDEX IF NOT EXISTS "IX_AttendanceSalarySlipDrafts_GeneratedSalaryPaymentId" ON "AttendanceSalarySlipDrafts" ("GeneratedSalaryPaymentId");
        CREATE INDEX IF NOT EXISTS "IX_AttendanceSalarySlipDrafts_CompanyId_StoreId_Year_Month_PaymentPostStatus" ON "AttendanceSalarySlipDrafts" ("CompanyId", "StoreId", "Year", "Month", "PaymentPostStatus");
        """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
        DROP INDEX IF EXISTS "IX_AttendanceSalarySlipDrafts_CompanyId_StoreId_Year_Month_PaymentPostStatus";
        DROP INDEX IF EXISTS "IX_AttendanceSalarySlipDrafts_GeneratedSalaryPaymentId";
        ALTER TABLE "AttendanceSalarySlipDrafts" DROP COLUMN IF EXISTS "PaymentPostStatus";
        ALTER TABLE "AttendanceSalarySlipDrafts" DROP COLUMN IF EXISTS "SalaryPaidBy";
        ALTER TABLE "AttendanceSalarySlipDrafts" DROP COLUMN IF EXISTS "SalaryPaidAtUtc";
        ALTER TABLE "AttendanceSalarySlipDrafts" DROP COLUMN IF EXISTS "GeneratedSalaryPaymentId";
        """);
    }
}
