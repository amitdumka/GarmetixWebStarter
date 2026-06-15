using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Garmetix.Infrastructure.Data.Migrations;

[DbContext(typeof(GarmetixDbContext))]
[Migration("20260615173000_AddStockOperationAccounting")]
public partial class AddStockOperationAccounting : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            ALTER TABLE "StockOperationDocuments"
                ADD COLUMN IF NOT EXISTS "AccountingStatus" text NOT NULL DEFAULT 'Pending';
            ALTER TABLE "StockOperationDocuments"
                ADD COLUMN IF NOT EXISTS "JournalEntryId" uuid NULL;

            CREATE INDEX IF NOT EXISTS "IX_StockOperationDocuments_CompanyId_JournalEntryId"
                ON "StockOperationDocuments" ("CompanyId", "JournalEntryId");
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            DROP INDEX IF EXISTS "IX_StockOperationDocuments_CompanyId_JournalEntryId";
            ALTER TABLE "StockOperationDocuments" DROP COLUMN IF EXISTS "JournalEntryId";
            ALTER TABLE "StockOperationDocuments" DROP COLUMN IF EXISTS "AccountingStatus";
            """);
    }
}
