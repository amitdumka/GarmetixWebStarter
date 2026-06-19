using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Garmetix.Infrastructure.Data.Migrations;

public partial class AddHrEmployeeMasterAndBenefits : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            ALTER TABLE "Employees" ADD COLUMN IF NOT EXISTS "EmployeeCode" character varying(40) NULL;
            ALTER TABLE "Employees" ADD COLUMN IF NOT EXISTS "FatherOrHusbandName" character varying(120) NULL;
            ALTER TABLE "Employees" ADD COLUMN IF NOT EXISTS "Department" character varying(80) NULL;
            ALTER TABLE "Employees" ADD COLUMN IF NOT EXISTS "Designation" character varying(80) NULL;
            ALTER TABLE "Employees" ADD COLUMN IF NOT EXISTS "SalaryType" character varying(30) NOT NULL DEFAULT 'Monthly';
            ALTER TABLE "Employees" ADD COLUMN IF NOT EXISTS "MonthlySalary" numeric(18,2) NOT NULL DEFAULT 0;
            ALTER TABLE "Employees" ADD COLUMN IF NOT EXISTS "DailyWage" numeric(18,2) NOT NULL DEFAULT 0;
            ALTER TABLE "Employees" ADD COLUMN IF NOT EXISTS "EmployeeStatus" character varying(30) NOT NULL DEFAULT 'Active';
            ALTER TABLE "Employees" ADD COLUMN IF NOT EXISTS "ExitReason" character varying(200) NULL;
            ALTER TABLE "Employees" ADD COLUMN IF NOT EXISTS "BloodGroup" character varying(40) NULL;
            ALTER TABLE "Employees" ADD COLUMN IF NOT EXISTS "PhotoDataUrl" text NULL;
            ALTER TABLE "Employees" ADD COLUMN IF NOT EXISTS "BankAccountName" character varying(120) NULL;
            ALTER TABLE "Employees" ADD COLUMN IF NOT EXISTS "BankAccountNumber" character varying(30) NULL;
            ALTER TABLE "Employees" ADD COLUMN IF NOT EXISTS "IFSC" character varying(20) NULL;
            ALTER TABLE "Employees" ADD COLUMN IF NOT EXISTS "ESINumber" character varying(30) NULL;
            ALTER TABLE "Employees" ADD COLUMN IF NOT EXISTS "PFNumber" character varying(30) NULL;
            ALTER TABLE "Employees" ADD COLUMN IF NOT EXISTS "EmergencyContact" character varying(120) NULL;

            UPDATE "Employees"
            SET "EmployeeStatus" = CASE WHEN "Working" THEN 'Active' ELSE 'Inactive' END
            WHERE "EmployeeStatus" IS NULL OR "EmployeeStatus" = '';

            CREATE TABLE IF NOT EXISTS "EmployeePayrollAdjustments" (
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
                "AdjustmentType" character varying(40) NOT NULL DEFAULT 'SalaryAdvance',
                "OnDate" timestamp without time zone NOT NULL DEFAULT now(),
                "SalaryMonth" integer NULL,
                "Amount" numeric(18,2) NOT NULL DEFAULT 0,
                "LeaveDays" numeric(18,2) NOT NULL DEFAULT 0,
                "RecoverFromSalary" boolean NOT NULL DEFAULT true,
                "RecoveredAmount" numeric(18,2) NOT NULL DEFAULT 0,
                "PfEmployee" numeric(18,2) NOT NULL DEFAULT 0,
                "PfEmployer" numeric(18,2) NOT NULL DEFAULT 0,
                "GratuityAmount" numeric(18,2) NOT NULL DEFAULT 0,
                "Status" character varying(30) NOT NULL DEFAULT 'Open',
                "Remarks" character varying(200) NULL,
                CONSTRAINT "PK_EmployeePayrollAdjustments" PRIMARY KEY ("Id")
            );

            CREATE INDEX IF NOT EXISTS "IX_Employees_CompanyId_StoreId_EmployeeCode" ON "Employees" ("CompanyId", "StoreId", "EmployeeCode");
            CREATE INDEX IF NOT EXISTS "IX_EmployeePayrollAdjustments_Company_Store_Employee_Type_Status" ON "EmployeePayrollAdjustments" ("CompanyId", "StoreId", "EmployeeId", "AdjustmentType", "Status");
            CREATE INDEX IF NOT EXISTS "IX_EmployeePayrollAdjustments_Company_OnDate" ON "EmployeePayrollAdjustments" ("CompanyId", "OnDate");
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            DROP TABLE IF EXISTS "EmployeePayrollAdjustments";
            ALTER TABLE "Employees" DROP COLUMN IF EXISTS "EmployeeCode";
            ALTER TABLE "Employees" DROP COLUMN IF EXISTS "FatherOrHusbandName";
            ALTER TABLE "Employees" DROP COLUMN IF EXISTS "Department";
            ALTER TABLE "Employees" DROP COLUMN IF EXISTS "Designation";
            ALTER TABLE "Employees" DROP COLUMN IF EXISTS "SalaryType";
            ALTER TABLE "Employees" DROP COLUMN IF EXISTS "MonthlySalary";
            ALTER TABLE "Employees" DROP COLUMN IF EXISTS "DailyWage";
            ALTER TABLE "Employees" DROP COLUMN IF EXISTS "EmployeeStatus";
            ALTER TABLE "Employees" DROP COLUMN IF EXISTS "ExitReason";
            ALTER TABLE "Employees" DROP COLUMN IF EXISTS "BloodGroup";
            ALTER TABLE "Employees" DROP COLUMN IF EXISTS "PhotoDataUrl";
            ALTER TABLE "Employees" DROP COLUMN IF EXISTS "BankAccountName";
            ALTER TABLE "Employees" DROP COLUMN IF EXISTS "BankAccountNumber";
            ALTER TABLE "Employees" DROP COLUMN IF EXISTS "IFSC";
            ALTER TABLE "Employees" DROP COLUMN IF EXISTS "ESINumber";
            ALTER TABLE "Employees" DROP COLUMN IF EXISTS "PFNumber";
            ALTER TABLE "Employees" DROP COLUMN IF EXISTS "EmergencyContact";
            """);
    }
}
