using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Garmetix.Infrastructure.Data.Migrations;

[DbContext(typeof(GarmetixDbContext))]
[Migration("20260615193000_HardenDocumentSequenceConcurrency")]
public partial class HardenDocumentSequenceConcurrency : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            UPDATE "DocumentSequences"
            SET "DocumentType" = btrim("DocumentType"),
                "SequenceDate" = date_trunc('day', "SequenceDate")
            WHERE NOT "Deleted";

            WITH ranked AS (
                SELECT
                    "Id",
                    max("LastNumber") OVER (
                        PARTITION BY "CompanyId", "StoreGroupId", "StoreId", "DocumentType", "SequenceDate"
                    ) AS max_number,
                    row_number() OVER (
                        PARTITION BY "CompanyId", "StoreGroupId", "StoreId", "DocumentType", "SequenceDate"
                        ORDER BY "CreatedAt", "Id"
                    ) AS row_number
                FROM "DocumentSequences"
                WHERE NOT "Deleted"
            )
            UPDATE "DocumentSequences" target
            SET "LastNumber" = ranked.max_number,
                "Deleted" = ranked.row_number > 1,
                "UpdatedAt" = now()
            FROM ranked
            WHERE target."Id" = ranked."Id";

            DROP INDEX IF EXISTS "IX_DocumentSequences_Company_Store_Type_Date";
            CREATE UNIQUE INDEX "IX_DocumentSequences_Company_Store_Type_Date"
                ON "DocumentSequences" ("CompanyId", "StoreGroupId", "StoreId", "DocumentType", "SequenceDate")
                NULLS NOT DISTINCT
                WHERE "Deleted" = false;
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            DROP INDEX IF EXISTS "IX_DocumentSequences_Company_Store_Type_Date";
            CREATE INDEX "IX_DocumentSequences_Company_Store_Type_Date"
                ON "DocumentSequences" ("CompanyId", "StoreGroupId", "StoreId", "DocumentType", "SequenceDate");
            """);
    }
}
