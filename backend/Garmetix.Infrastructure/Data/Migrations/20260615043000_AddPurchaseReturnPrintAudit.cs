using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Garmetix.Infrastructure.Data.Migrations;

[DbContext(typeof(GarmetixDbContext))]
[Migration("20260615043000_AddPurchaseReturnPrintAudit")]
public partial class AddPurchaseReturnPrintAudit : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            ALTER TABLE "PurchaseReturns" ADD COLUMN IF NOT EXISTS "Printed" boolean NOT NULL DEFAULT false;
            ALTER TABLE "PurchaseReturns" ADD COLUMN IF NOT EXISTS "PrintCount" integer NOT NULL DEFAULT 0;
            ALTER TABLE "PurchaseReturns" ADD COLUMN IF NOT EXISTS "LastPrintedAt" timestamp without time zone NULL;
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            ALTER TABLE "PurchaseReturns" DROP COLUMN IF EXISTS "LastPrintedAt";
            ALTER TABLE "PurchaseReturns" DROP COLUMN IF EXISTS "PrintCount";
            ALTER TABLE "PurchaseReturns" DROP COLUMN IF EXISTS "Printed";
            """);
    }
}
