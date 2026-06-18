using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Garmetix.Infrastructure.Data.Migrations;

[DbContext(typeof(GarmetixDbContext))]
[Migration("20260615150000_BackfillLegacyStockProjectionMovements")]
public partial class BackfillLegacyStockProjectionMovements : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(LegacyStockProjectionBackfill.Sql);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            DELETE FROM "StockMovements"
            WHERE "SourceType" = 'LegacyStockProjection'
              AND "MovementType" IN ('LegacyOpeningIn', 'LegacyOpeningOut');
            """);
    }
}

internal static class LegacyStockProjectionBackfill
{
    public const string Sql = """
        DROP TABLE IF EXISTS "_LegacyStockProjectionBackfill";
        CREATE TEMP TABLE "_LegacyStockProjectionBackfill" ON COMMIT DROP AS
        SELECT stock.*
        FROM "Stocks" AS stock
        WHERE stock."IsOFB" = false
          AND (stock."PurchaseQty" <> 0 OR stock."SoldQty" <> 0)
          AND NOT EXISTS (
              SELECT 1
              FROM "StockMovements" AS movement
              WHERE movement."StockId" = stock."Id");

        INSERT INTO "StockMovements" (
            "Id", "StockId", "ProductId", "Barcode", "MovementType",
            "QuantityIn", "QuantityOut", "CostPrice",
            "QuantityBefore", "QuantityAfter", "AverageCostBefore", "AverageCostAfter",
            "InventoryValueBefore", "InventoryValueAfter", "CostImpact", "ValuationMethod",
            "MRP", "TaxRate", "HSNCode", "SourceType", "SourceId", "SourceNumber", "Remarks",
            "OnDate", "CompanyId", "CreatedBy", "StoreGroupId", "StoreId",
            "CreatedAt", "UpdatedAt", "Synced", "Deleted")
        SELECT
            md5(stock."Id"::text || ':legacy-opening-in')::uuid,
            stock."Id",
            stock."ProductId",
            stock."Barcode",
            'LegacyOpeningIn',
            stock."PurchaseQty",
            0,
            stock."CostPrice",
            0,
            stock."PurchaseQty",
            0,
            CASE WHEN stock."PurchaseQty" > 0 THEN stock."CostPrice" ELSE 0 END,
            0,
            ROUND(stock."PurchaseQty" * stock."CostPrice", 2),
            ROUND(stock."PurchaseQty" * stock."CostPrice", 2),
            'WeightedAverage',
            stock."MRP",
            stock."TaxRate",
            stock."HSNCode",
            'LegacyStockProjection',
            stock."Id",
            stock."Barcode",
            'Backfilled from legacy Stock purchase quantity',
            stock."CreatedAt",
            stock."CompanyId",
            'Migration',
            stock."StoreGroupId",
            stock."StoreId",
            stock."CreatedAt",
            NULL,
            false,
            false
        FROM "_LegacyStockProjectionBackfill" AS stock
        WHERE stock."PurchaseQty" > 0;

        INSERT INTO "StockMovements" (
            "Id", "StockId", "ProductId", "Barcode", "MovementType",
            "QuantityIn", "QuantityOut", "CostPrice",
            "QuantityBefore", "QuantityAfter", "AverageCostBefore", "AverageCostAfter",
            "InventoryValueBefore", "InventoryValueAfter", "CostImpact", "ValuationMethod",
            "MRP", "TaxRate", "HSNCode", "SourceType", "SourceId", "SourceNumber", "Remarks",
            "OnDate", "CompanyId", "CreatedBy", "StoreGroupId", "StoreId",
            "CreatedAt", "UpdatedAt", "Synced", "Deleted")
        SELECT
            md5(stock."Id"::text || ':legacy-opening-out')::uuid,
            stock."Id",
            stock."ProductId",
            stock."Barcode",
            'LegacyOpeningOut',
            0,
            stock."SoldQty",
            CASE WHEN stock."PurchaseQty" > 0 THEN stock."CostPrice" ELSE 0 END,
            stock."PurchaseQty",
            ROUND(stock."PurchaseQty" - stock."SoldQty", 2),
            CASE WHEN stock."PurchaseQty" > 0 THEN stock."CostPrice" ELSE 0 END,
            CASE WHEN stock."PurchaseQty" - stock."SoldQty" > 0 THEN stock."CostPrice" ELSE 0 END,
            ROUND(stock."PurchaseQty" * stock."CostPrice", 2),
            ROUND((stock."PurchaseQty" - stock."SoldQty") * stock."CostPrice", 2),
            ROUND(-stock."SoldQty" * stock."CostPrice", 2),
            'WeightedAverage',
            stock."MRP",
            stock."TaxRate",
            stock."HSNCode",
            'LegacyStockProjection',
            stock."Id",
            stock."Barcode",
            'Backfilled from legacy Stock sold quantity',
            stock."CreatedAt" + interval '1 second',
            stock."CompanyId",
            'Migration',
            stock."StoreGroupId",
            stock."StoreId",
            stock."CreatedAt" + interval '1 second',
            NULL,
            false,
            false
        FROM "_LegacyStockProjectionBackfill" AS stock
        WHERE stock."SoldQty" > 0;
        """;
}
