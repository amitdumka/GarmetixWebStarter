using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Garmetix.Infrastructure.Data.Migrations;

[DbContext(typeof(GarmetixDbContext))]
[Migration("20260614094500_SeparateNonGstGoodsFromBooks")]
public partial class SeparateNonGstGoodsFromBooks : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            ALTER TABLE IF EXISTS "NonGstGoodsDocuments"
                ADD COLUMN IF NOT EXISTS "PaidAmount" numeric(18,2) NOT NULL DEFAULT 0;
            ALTER TABLE IF EXISTS "NonGstGoodsDocuments"
                ADD COLUMN IF NOT EXISTS "BalanceAmount" numeric(18,2) NOT NULL DEFAULT 0;

            UPDATE "NonGstGoodsDocuments"
            SET "PaidAmount" = "NetAmount",
                "BalanceAmount" = 0,
                "LedgerId" = NULL;

            DELETE FROM "JournalLines"
            WHERE "JournalEntryId" IN (
                SELECT "Id"
                FROM "JournalEntries"
                WHERE "SourceType" IN ('NonGstPurchase', 'NonGstSale')
            );

            DELETE FROM "JournalEntries"
            WHERE "SourceType" IN ('NonGstPurchase', 'NonGstSale');
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            ALTER TABLE IF EXISTS "NonGstGoodsDocuments" DROP COLUMN IF EXISTS "PaidAmount";
            ALTER TABLE IF EXISTS "NonGstGoodsDocuments" DROP COLUMN IF EXISTS "BalanceAmount";
            """);
    }
}
