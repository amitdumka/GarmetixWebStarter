using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Garmetix.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddStage3AProductMaster : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                ALTER TABLE IF EXISTS "Products" ADD COLUMN IF NOT EXISTS "ProductGroup" integer NOT NULL DEFAULT 0;
                ALTER TABLE IF EXISTS "Products" ADD COLUMN IF NOT EXISTS "HSNCode" text NULL;
                ALTER TABLE IF EXISTS "Stocks" ADD COLUMN IF NOT EXISTS "StockType" integer NOT NULL DEFAULT 0;
                ALTER TABLE IF EXISTS "ProductCategories" ADD COLUMN IF NOT EXISTS "ProductGroup" integer NULL DEFAULT 0;
                ALTER TABLE IF EXISTS "ProductCategories" ADD COLUMN IF NOT EXISTS "IsActive" boolean NOT NULL DEFAULT true;
                ALTER TABLE IF EXISTS "ProductSubCategories" ADD COLUMN IF NOT EXISTS "CategoryId" uuid NULL;

                CREATE TABLE IF NOT EXISTS "ProductDetails" (
                    "Id" uuid NOT NULL,
                    "ProductId" uuid NOT NULL,
                    "Barcode" text NOT NULL DEFAULT '',
                    "StyleCode" text NULL,
                    "BaseColor" text NULL,
                    "Brand" text NULL,
                    "VendorId" uuid NULL,
                    "CompanyId" uuid NOT NULL,
                    "CreatedBy" text NULL,
                    "CreatedAt" timestamp without time zone NOT NULL,
                    "UpdatedAt" timestamp without time zone NULL,
                    "Synced" boolean NOT NULL DEFAULT false,
                    "Deleted" boolean NOT NULL DEFAULT false,
                    CONSTRAINT "PK_ProductDetails" PRIMARY KEY ("Id")
                );

                CREATE TABLE IF NOT EXISTS "ProductAttributes" (
                    "Id" uuid NOT NULL,
                    "Name" text NOT NULL DEFAULT '',
                    CONSTRAINT "PK_ProductAttributes" PRIMARY KEY ("Id")
                );

                CREATE TABLE IF NOT EXISTS "ProductAttributeValues" (
                    "ProductId" uuid NOT NULL,
                    "AttributeId" uuid NOT NULL,
                    "Value" text NOT NULL DEFAULT '',
                    CONSTRAINT "PK_ProductAttributeValues" PRIMARY KEY ("ProductId", "AttributeId")
                );

                CREATE TABLE IF NOT EXISTS "ProductTags" (
                    "Id" uuid NOT NULL,
                    "Name" text NOT NULL DEFAULT '',
                    CONSTRAINT "PK_ProductTags" PRIMARY KEY ("Id")
                );

                CREATE TABLE IF NOT EXISTS "ProductTagMappings" (
                    "ProductId" uuid NOT NULL,
                    "TagId" uuid NOT NULL,
                    CONSTRAINT "PK_ProductTagMappings" PRIMARY KEY ("ProductId", "TagId")
                );

                CREATE INDEX IF NOT EXISTS "IX_Products_CompanyId_ProductGroup_ProductType" ON "Products" ("CompanyId", "ProductGroup", "ProductType");
                CREATE INDEX IF NOT EXISTS "IX_ProductCategories_CompanyId_ProductGroup_Name" ON "ProductCategories" ("CompanyId", "ProductGroup", "Name");
                CREATE INDEX IF NOT EXISTS "IX_ProductSubCategories_CompanyId_CategoryId_Name" ON "ProductSubCategories" ("CompanyId", "CategoryId", "Name");
                CREATE INDEX IF NOT EXISTS "IX_ProductDetails_CompanyId_ProductId_Barcode" ON "ProductDetails" ("CompanyId", "ProductId", "Barcode");
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DROP TABLE IF EXISTS "ProductTagMappings";
                DROP TABLE IF EXISTS "ProductTags";
                DROP TABLE IF EXISTS "ProductAttributeValues";
                DROP TABLE IF EXISTS "ProductAttributes";
                DROP TABLE IF EXISTS "ProductDetails";
                ALTER TABLE IF EXISTS "ProductSubCategories" DROP COLUMN IF EXISTS "CategoryId";
                ALTER TABLE IF EXISTS "ProductCategories" DROP COLUMN IF EXISTS "IsActive";
                ALTER TABLE IF EXISTS "ProductCategories" DROP COLUMN IF EXISTS "ProductGroup";
                ALTER TABLE IF EXISTS "Stocks" DROP COLUMN IF EXISTS "StockType";
                ALTER TABLE IF EXISTS "Products" DROP COLUMN IF EXISTS "ProductGroup";
                """);
        }
    }
}
