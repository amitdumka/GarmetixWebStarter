using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Garmetix.Infrastructure.Data.Migrations;

public partial class AddNonGstGoodsModule : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(@"
            ALTER TABLE IF EXISTS ""Stocks"" ADD COLUMN IF NOT EXISTS ""IsOFB"" boolean NOT NULL DEFAULT false;

            CREATE TABLE IF NOT EXISTS ""NonGstGoodsDocuments"" (
                ""Id"" uuid NOT NULL,
                ""DocumentNumber"" text NOT NULL DEFAULT '',
                ""OnDate"" timestamp without time zone NOT NULL,
                ""DocumentType"" integer NOT NULL DEFAULT 1,
                ""PartyName"" text NOT NULL DEFAULT '',
                ""VendorId"" uuid NULL,
                ""CustomerId"" uuid NULL,
                ""PaymentMode"" integer NOT NULL DEFAULT 0,
                ""ReferenceNumber"" text NULL,
                ""GrossAmount"" numeric(18,2) NOT NULL DEFAULT 0,
                ""DiscountAmount"" numeric(18,2) NOT NULL DEFAULT 0,
                ""NetAmount"" numeric(18,2) NOT NULL DEFAULT 0,
                ""LedgerId"" uuid NULL,
                ""Remarks"" text NULL,
                ""CompanyId"" uuid NOT NULL,
                ""CreatedBy"" text NULL,
                ""StoreGroupId"" uuid NOT NULL,
                ""StoreId"" uuid NOT NULL,
                ""CreatedAt"" timestamp without time zone NOT NULL,
                ""UpdatedAt"" timestamp without time zone NULL,
                ""Synced"" boolean NOT NULL DEFAULT false,
                ""Deleted"" boolean NOT NULL DEFAULT false,
                CONSTRAINT ""PK_NonGstGoodsDocuments"" PRIMARY KEY (""Id"")
            );

            CREATE TABLE IF NOT EXISTS ""NonGstGoodsItems"" (
                ""Id"" uuid NOT NULL,
                ""DocumentId"" uuid NOT NULL,
                ""ProductId"" uuid NOT NULL,
                ""StockId"" uuid NULL,
                ""Barcode"" text NOT NULL DEFAULT '',
                ""ProductName"" text NOT NULL DEFAULT '',
                ""Quantity"" numeric(18,2) NOT NULL DEFAULT 0,
                ""Rate"" numeric(18,2) NOT NULL DEFAULT 0,
                ""DiscountAmount"" numeric(18,2) NOT NULL DEFAULT 0,
                ""Amount"" numeric(18,2) NOT NULL DEFAULT 0,
                ""CompanyId"" uuid NOT NULL,
                ""CreatedBy"" text NULL,
                ""CreatedAt"" timestamp without time zone NOT NULL,
                ""UpdatedAt"" timestamp without time zone NULL,
                ""Synced"" boolean NOT NULL DEFAULT false,
                ""Deleted"" boolean NOT NULL DEFAULT false,
                CONSTRAINT ""PK_NonGstGoodsItems"" PRIMARY KEY (""Id"")
            );

            CREATE INDEX IF NOT EXISTS ""IX_Stocks_CompanyId_StoreId_IsOFB"" ON ""Stocks"" (""CompanyId"", ""StoreId"", ""IsOFB"");
            CREATE INDEX IF NOT EXISTS ""IX_NonGstGoodsDocuments_CompanyId_StoreId_DocumentType_OnDate"" ON ""NonGstGoodsDocuments"" (""CompanyId"", ""StoreId"", ""DocumentType"", ""OnDate"");
            CREATE INDEX IF NOT EXISTS ""IX_NonGstGoodsDocuments_CompanyId_DocumentNumber"" ON ""NonGstGoodsDocuments"" (""CompanyId"", ""DocumentNumber"");
            CREATE INDEX IF NOT EXISTS ""IX_NonGstGoodsItems_CompanyId_DocumentId"" ON ""NonGstGoodsItems"" (""CompanyId"", ""DocumentId"");
        ");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(@"
            DROP TABLE IF EXISTS ""NonGstGoodsItems"";
            DROP TABLE IF EXISTS ""NonGstGoodsDocuments"";
            ALTER TABLE IF EXISTS ""Stocks"" DROP COLUMN IF EXISTS ""IsOFB"";
        ");
    }
}
