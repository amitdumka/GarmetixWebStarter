using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Garmetix.Infrastructure.Data.Migrations;

[DbContext(typeof(GarmetixDbContext))]
[Migration("20260615102000_AddFormalStockOperationDocuments")]
public partial class AddFormalStockOperationDocuments : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            CREATE TABLE IF NOT EXISTS "StockOperationDocuments" (
                "Id" uuid NOT NULL,
                "DocumentNumber" text NOT NULL DEFAULT '',
                "OnDate" timestamp without time zone NOT NULL,
                "OperationType" text NOT NULL DEFAULT '',
                "Status" text NOT NULL DEFAULT 'Posted',
                "FromStoreId" uuid NULL,
                "FromStoreName" text NULL,
                "ToStoreId" uuid NULL,
                "ToStoreName" text NULL,
                "Reason" text NOT NULL DEFAULT '',
                "TotalQuantity" numeric(18,2) NOT NULL DEFAULT 0,
                "TotalCostValue" numeric(18,2) NOT NULL DEFAULT 0,
                "TotalMrpValue" numeric(18,2) NOT NULL DEFAULT 0,
                "ItemCount" integer NOT NULL DEFAULT 0,
                "PostedAt" timestamp without time zone NOT NULL,
                "CompanyId" uuid NOT NULL,
                "CreatedBy" text NULL,
                "StoreGroupId" uuid NOT NULL,
                "StoreId" uuid NOT NULL,
                "CreatedAt" timestamp without time zone NOT NULL DEFAULT now(),
                "UpdatedAt" timestamp without time zone NULL,
                "Synced" boolean NOT NULL DEFAULT false,
                "Deleted" boolean NOT NULL DEFAULT false,
                CONSTRAINT "PK_StockOperationDocuments" PRIMARY KEY ("Id")
            );

            CREATE TABLE IF NOT EXISTS "StockOperationItems" (
                "Id" uuid NOT NULL,
                "StockOperationDocumentId" uuid NOT NULL,
                "ProductId" uuid NOT NULL,
                "StockId" uuid NULL,
                "DestinationStockId" uuid NULL,
                "ProductName" text NOT NULL DEFAULT '',
                "Barcode" text NOT NULL DEFAULT '',
                "HSNCode" text NULL,
                "Unit" integer NOT NULL DEFAULT 0,
                "FromStoreId" uuid NULL,
                "ToStoreId" uuid NULL,
                "SystemQuantity" numeric(18,2) NOT NULL DEFAULT 0,
                "CountedQuantity" numeric(18,2) NULL,
                "QuantityIn" numeric(18,2) NOT NULL DEFAULT 0,
                "QuantityOut" numeric(18,2) NOT NULL DEFAULT 0,
                "QuantityDifference" numeric(18,2) NOT NULL DEFAULT 0,
                "FromQuantityBefore" numeric(18,2) NOT NULL DEFAULT 0,
                "FromQuantityAfter" numeric(18,2) NOT NULL DEFAULT 0,
                "ToQuantityBefore" numeric(18,2) NULL,
                "ToQuantityAfter" numeric(18,2) NULL,
                "CostPrice" numeric(18,2) NOT NULL DEFAULT 0,
                "MRP" numeric(18,2) NOT NULL DEFAULT 0,
                "CostValue" numeric(18,2) NOT NULL DEFAULT 0,
                "MrpValue" numeric(18,2) NOT NULL DEFAULT 0,
                "OutMovementId" uuid NULL,
                "InMovementId" uuid NULL,
                "Reason" text NULL,
                "CompanyId" uuid NOT NULL,
                "CreatedBy" text NULL,
                "CreatedAt" timestamp without time zone NOT NULL DEFAULT now(),
                "UpdatedAt" timestamp without time zone NULL,
                "Synced" boolean NOT NULL DEFAULT false,
                "Deleted" boolean NOT NULL DEFAULT false,
                CONSTRAINT "PK_StockOperationItems" PRIMARY KEY ("Id")
            );

            CREATE INDEX IF NOT EXISTS "IX_StockOperationDocuments_CompanyId_StoreId_DocumentNumber"
                ON "StockOperationDocuments" ("CompanyId", "StoreId", "DocumentNumber");
            CREATE INDEX IF NOT EXISTS "IX_StockOperationDocuments_CompanyId_OperationType_OnDate"
                ON "StockOperationDocuments" ("CompanyId", "OperationType", "OnDate");
            CREATE INDEX IF NOT EXISTS "IX_StockOperationItems_CompanyId_StockOperationDocumentId"
                ON "StockOperationItems" ("CompanyId", "StockOperationDocumentId");
            CREATE INDEX IF NOT EXISTS "IX_StockOperationItems_CompanyId_ProductId_StockId"
                ON "StockOperationItems" ("CompanyId", "ProductId", "StockId");

            INSERT INTO "StockOperationDocuments" (
                "Id", "DocumentNumber", "OnDate", "OperationType", "Status",
                "FromStoreId", "FromStoreName", "ToStoreId", "ToStoreName", "Reason",
                "TotalQuantity", "TotalCostValue", "TotalMrpValue", "ItemCount", "PostedAt",
                "CompanyId", "CreatedBy", "StoreGroupId", "StoreId", "CreatedAt", "UpdatedAt", "Synced", "Deleted"
            )
            SELECT
                (array_agg(movement."Id" ORDER BY movement."OnDate", movement."Id"))[1],
                movement."SourceNumber",
                MIN(movement."OnDate"),
                CASE movement."SourceType"
                    WHEN 'StockAdjustment' THEN 'Adjustment'
                    WHEN 'StockTransfer' THEN 'Transfer'
                    ELSE 'PhysicalCount'
                END,
                'Backfilled',
                (array_agg(movement."StoreId" ORDER BY movement."QuantityOut" DESC, movement."OnDate", movement."Id"))[1],
                (array_agg(store."Name" ORDER BY movement."QuantityOut" DESC, movement."OnDate", movement."Id"))[1],
                CASE WHEN movement."SourceType" = 'StockTransfer'
                    THEN (array_agg(movement."StoreId" ORDER BY movement."QuantityIn" DESC, movement."OnDate", movement."Id"))[1]
                    ELSE NULL
                END,
                CASE WHEN movement."SourceType" = 'StockTransfer'
                    THEN (array_agg(store."Name" ORDER BY movement."QuantityIn" DESC, movement."OnDate", movement."Id"))[1]
                    ELSE NULL
                END,
                COALESCE(MAX(movement."Remarks"), 'Backfilled from stock movement ledger'),
                MAX(GREATEST(movement."QuantityIn", movement."QuantityOut")),
                MAX(GREATEST(movement."QuantityIn", movement."QuantityOut") * movement."CostPrice"),
                MAX(GREATEST(movement."QuantityIn", movement."QuantityOut") * movement."MRP"),
                COUNT(DISTINCT movement."ProductId")::integer,
                MIN(movement."OnDate"),
                movement."CompanyId",
                MAX(movement."CreatedBy"),
                (array_agg(movement."StoreGroupId" ORDER BY movement."OnDate", movement."Id"))[1],
                (array_agg(movement."StoreId" ORDER BY movement."QuantityOut" DESC, movement."OnDate", movement."Id"))[1],
                MIN(movement."CreatedAt"),
                MAX(movement."UpdatedAt"),
                BOOL_AND(movement."Synced"),
                false
            FROM "StockMovements" AS movement
            LEFT JOIN "Stores" AS store ON store."Id" = movement."StoreId"
            WHERE movement."SourceType" IN ('StockAdjustment', 'StockTransfer', 'PhysicalCount')
              AND movement."SourceNumber" IS NOT NULL
              AND movement."SourceNumber" <> ''
            GROUP BY movement."CompanyId", movement."SourceType", movement."SourceNumber"
            ON CONFLICT ("Id") DO NOTHING;

            INSERT INTO "StockOperationItems" (
                "Id", "StockOperationDocumentId", "ProductId", "StockId", "DestinationStockId",
                "ProductName", "Barcode", "HSNCode", "Unit", "FromStoreId", "ToStoreId",
                "SystemQuantity", "CountedQuantity", "QuantityIn", "QuantityOut", "QuantityDifference",
                "FromQuantityBefore", "FromQuantityAfter", "ToQuantityBefore", "ToQuantityAfter",
                "CostPrice", "MRP", "CostValue", "MrpValue", "OutMovementId", "InMovementId",
                "Reason", "CompanyId", "CreatedBy", "CreatedAt", "UpdatedAt", "Synced", "Deleted"
            )
            SELECT
                movement."Id",
                document."Id",
                movement."ProductId",
                movement."StockId",
                NULL,
                COALESCE(product."Name", movement."Barcode"),
                movement."Barcode",
                movement."HSNCode",
                COALESCE(stock."Unit", 0),
                movement."StoreId",
                document."ToStoreId",
                0,
                NULL,
                movement."QuantityIn",
                movement."QuantityOut",
                movement."QuantityIn" - movement."QuantityOut",
                0,
                0,
                NULL,
                NULL,
                movement."CostPrice",
                movement."MRP",
                GREATEST(movement."QuantityIn", movement."QuantityOut") * movement."CostPrice",
                GREATEST(movement."QuantityIn", movement."QuantityOut") * movement."MRP",
                CASE WHEN movement."QuantityOut" > 0 THEN movement."Id" ELSE NULL END,
                CASE WHEN movement."QuantityIn" > 0 THEN movement."Id" ELSE NULL END,
                movement."Remarks",
                movement."CompanyId",
                movement."CreatedBy",
                movement."CreatedAt",
                movement."UpdatedAt",
                movement."Synced",
                movement."Deleted"
            FROM "StockMovements" AS movement
            INNER JOIN "StockOperationDocuments" AS document
                ON document."CompanyId" = movement."CompanyId"
               AND document."DocumentNumber" = movement."SourceNumber"
            LEFT JOIN "Products" AS product ON product."Id" = movement."ProductId"
            LEFT JOIN "Stocks" AS stock ON stock."Id" = movement."StockId"
            WHERE movement."SourceType" IN ('StockAdjustment', 'StockTransfer', 'PhysicalCount')
            ON CONFLICT ("Id") DO NOTHING;

            UPDATE "StockMovements" AS movement
            SET "SourceType" = 'StockOperationDocument',
                "SourceId" = document."Id"
            FROM "StockOperationDocuments" AS document
            WHERE movement."CompanyId" = document."CompanyId"
              AND movement."SourceNumber" = document."DocumentNumber"
              AND movement."SourceType" IN ('StockAdjustment', 'StockTransfer', 'PhysicalCount');
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            DROP TABLE IF EXISTS "StockOperationItems";
            DROP TABLE IF EXISTS "StockOperationDocuments";
            """);
    }
}
