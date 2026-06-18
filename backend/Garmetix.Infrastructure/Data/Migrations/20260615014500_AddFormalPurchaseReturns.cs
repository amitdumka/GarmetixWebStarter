using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Garmetix.Infrastructure.Data.Migrations;

[DbContext(typeof(GarmetixDbContext))]
[Migration("20260615014500_AddFormalPurchaseReturns")]
public partial class AddFormalPurchaseReturns : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            CREATE TABLE IF NOT EXISTS "PurchaseReturns" (
                "Id" uuid NOT NULL,
                "ReturnNumber" text NOT NULL DEFAULT '',
                "OnDate" timestamp without time zone NOT NULL,
                "PurchaseInvoiceId" uuid NOT NULL,
                "OriginalInvoiceNumber" text NOT NULL DEFAULT '',
                "OriginalInvoiceDate" timestamp without time zone NOT NULL,
                "SupplierInvoiceDate" timestamp without time zone NULL,
                "VendorId" uuid NOT NULL,
                "VendorName" text NOT NULL DEFAULT '',
                "VendorGstin" text NULL,
                "ReturnKind" text NOT NULL DEFAULT 'Partial',
                "Status" text NOT NULL DEFAULT 'Posted',
                "Reason" text NOT NULL DEFAULT '',
                "Quantity" numeric(18,2) NOT NULL DEFAULT 0,
                "TaxableAmount" numeric(18,2) NOT NULL DEFAULT 0,
                "TaxAmount" numeric(18,2) NOT NULL DEFAULT 0,
                "CGSTAmount" numeric(18,2) NOT NULL DEFAULT 0,
                "SGSTAmount" numeric(18,2) NOT NULL DEFAULT 0,
                "IGSTAmount" numeric(18,2) NOT NULL DEFAULT 0,
                "ReturnAmount" numeric(18,2) NOT NULL DEFAULT 0,
                "DebitNoteId" uuid NULL,
                "DebitNoteNumber" text NULL,
                "ItemCount" integer NOT NULL DEFAULT 0,
                "CompanyId" uuid NOT NULL,
                "CreatedBy" text NULL,
                "StoreGroupId" uuid NOT NULL,
                "StoreId" uuid NOT NULL,
                "CreatedAt" timestamp without time zone NOT NULL DEFAULT now(),
                "UpdatedAt" timestamp without time zone NULL,
                "Synced" boolean NOT NULL DEFAULT false,
                "Deleted" boolean NOT NULL DEFAULT false,
                CONSTRAINT "PK_PurchaseReturns" PRIMARY KEY ("Id")
            );

            CREATE TABLE IF NOT EXISTS "PurchaseReturnItems" (
                "Id" uuid NOT NULL,
                "PurchaseReturnId" uuid NOT NULL,
                "PurchaseInvoiceId" uuid NOT NULL,
                "PurchaseInvoiceItemId" uuid NOT NULL,
                "ProductId" uuid NOT NULL,
                "ProductName" text NOT NULL DEFAULT '',
                "Barcode" text NOT NULL DEFAULT '',
                "HSNCode" text NULL,
                "Unit" integer NULL,
                "ProductCategoryId" uuid NULL,
                "ProductSubCategoryId" uuid NULL,
                "PurchasedQuantity" numeric(18,2) NOT NULL DEFAULT 0,
                "PreviouslyReturnedQuantity" numeric(18,2) NOT NULL DEFAULT 0,
                "ReturnedQuantity" numeric(18,2) NOT NULL DEFAULT 0,
                "MRP" numeric(18,2) NOT NULL DEFAULT 0,
                "UnitRate" numeric(18,2) NOT NULL DEFAULT 0,
                "DiscountAmount" numeric(18,2) NOT NULL DEFAULT 0,
                "TaxableAmount" numeric(18,2) NOT NULL DEFAULT 0,
                "TaxRate" numeric(18,2) NOT NULL DEFAULT 0,
                "TaxAmount" numeric(18,2) NOT NULL DEFAULT 0,
                "CGSTAmount" numeric(18,2) NOT NULL DEFAULT 0,
                "SGSTAmount" numeric(18,2) NOT NULL DEFAULT 0,
                "IGSTAmount" numeric(18,2) NOT NULL DEFAULT 0,
                "ReturnAmount" numeric(18,2) NOT NULL DEFAULT 0,
                "Reason" text NULL,
                "CompanyId" uuid NOT NULL,
                "CreatedBy" text NULL,
                "CreatedAt" timestamp without time zone NOT NULL DEFAULT now(),
                "UpdatedAt" timestamp without time zone NULL,
                "Synced" boolean NOT NULL DEFAULT false,
                "Deleted" boolean NOT NULL DEFAULT false,
                CONSTRAINT "PK_PurchaseReturnItems" PRIMARY KEY ("Id")
            );

            CREATE INDEX IF NOT EXISTS "IX_PurchaseReturns_CompanyId_StoreId_ReturnNumber"
                ON "PurchaseReturns" ("CompanyId", "StoreId", "ReturnNumber");
            CREATE INDEX IF NOT EXISTS "IX_PurchaseReturns_CompanyId_PurchaseInvoiceId_OnDate"
                ON "PurchaseReturns" ("CompanyId", "PurchaseInvoiceId", "OnDate");
            CREATE INDEX IF NOT EXISTS "IX_PurchaseReturnItems_CompanyId_PurchaseReturnId"
                ON "PurchaseReturnItems" ("CompanyId", "PurchaseReturnId");
            CREATE INDEX IF NOT EXISTS "IX_PurchaseReturnItems_CompanyId_PurchaseInvoiceId_PurchaseInvoiceItemId"
                ON "PurchaseReturnItems" ("CompanyId", "PurchaseInvoiceId", "PurchaseInvoiceItemId");
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            DROP TABLE IF EXISTS "PurchaseReturnItems";
            DROP TABLE IF EXISTS "PurchaseReturns";
            """);
    }
}
