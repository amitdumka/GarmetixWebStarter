using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Garmetix.Infrastructure.Data.Migrations;

[DbContext(typeof(GarmetixDbContext))]
[Migration("20260615143000_AddWeightedAverageStockValuation")]
public partial class AddWeightedAverageStockValuation : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            ALTER TABLE "Stocks" ALTER COLUMN "CostPrice" TYPE numeric(18,4);
            ALTER TABLE "StockMovements" ALTER COLUMN "CostPrice" TYPE numeric(18,4);
            ALTER TABLE "StockMovements" ADD COLUMN IF NOT EXISTS "QuantityBefore" numeric(18,2) NOT NULL DEFAULT 0;
            ALTER TABLE "StockMovements" ADD COLUMN IF NOT EXISTS "QuantityAfter" numeric(18,2) NOT NULL DEFAULT 0;
            ALTER TABLE "StockMovements" ADD COLUMN IF NOT EXISTS "AverageCostBefore" numeric(18,4) NOT NULL DEFAULT 0;
            ALTER TABLE "StockMovements" ADD COLUMN IF NOT EXISTS "AverageCostAfter" numeric(18,4) NOT NULL DEFAULT 0;
            ALTER TABLE "StockMovements" ADD COLUMN IF NOT EXISTS "InventoryValueBefore" numeric(18,2) NOT NULL DEFAULT 0;
            ALTER TABLE "StockMovements" ADD COLUMN IF NOT EXISTS "InventoryValueAfter" numeric(18,2) NOT NULL DEFAULT 0;
            ALTER TABLE "StockMovements" ADD COLUMN IF NOT EXISTS "CostImpact" numeric(18,2) NOT NULL DEFAULT 0;
            ALTER TABLE "StockMovements" ADD COLUMN IF NOT EXISTS "ValuationMethod" text NOT NULL DEFAULT 'WeightedAverage';

            WITH RECURSIVE ordered AS (
                SELECT
                    movement.*,
                    ROW_NUMBER() OVER (
                        PARTITION BY movement."StockId"
                        ORDER BY movement."OnDate", movement."CreatedAt", movement."Id"
                    ) AS row_number
                FROM "StockMovements" AS movement
                INNER JOIN "Stocks" AS stock ON stock."Id" = movement."StockId"
                WHERE movement."StockId" IS NOT NULL
                  AND stock."IsOFB" = false
            ),
            replay AS (
                SELECT
                    movement."Id",
                    movement."StockId",
                    movement.row_number,
                    0::numeric AS quantity_before,
                    ROUND(movement."QuantityIn" - movement."QuantityOut", 2) AS quantity_after,
                    0::numeric AS average_cost_before,
                    CASE
                        WHEN movement."QuantityIn" - movement."QuantityOut" > 0
                            THEN ROUND(
                                (movement."QuantityIn" * movement."CostPrice")
                                / (movement."QuantityIn" - movement."QuantityOut"),
                                4)
                        ELSE 0
                    END AS average_cost_after,
                    0::numeric AS inventory_value_before,
                    ROUND(movement."QuantityIn" * movement."CostPrice", 2) AS inventory_value_after,
                    ROUND(movement."QuantityIn" * movement."CostPrice", 2) AS cost_impact
                FROM ordered AS movement
                WHERE movement.row_number = 1

                UNION ALL

                SELECT
                    movement."Id",
                    movement."StockId",
                    movement.row_number,
                    previous.quantity_after,
                    calculation.quantity_after,
                    previous.average_cost_after,
                    CASE
                        WHEN calculation.quantity_after > 0
                            THEN ROUND(calculation.inventory_value_after / calculation.quantity_after, 4)
                        ELSE 0
                    END,
                    previous.inventory_value_after,
                    calculation.inventory_value_after,
                    calculation.cost_impact
                FROM replay AS previous
                INNER JOIN ordered AS movement
                    ON movement."StockId" = previous."StockId"
                   AND movement.row_number = previous.row_number + 1
                CROSS JOIN LATERAL (
                    SELECT
                        ROUND(previous.quantity_after + movement."QuantityIn" - movement."QuantityOut", 2) AS quantity_after,
                        ROUND(
                            previous.inventory_value_after
                            + CASE
                                WHEN movement."QuantityIn" > 0
                                    THEN movement."QuantityIn" * movement."CostPrice"
                                ELSE -movement."QuantityOut" * previous.average_cost_after
                              END,
                            2) AS inventory_value_after,
                        ROUND(
                            CASE
                                WHEN movement."QuantityIn" > 0
                                    THEN movement."QuantityIn" * movement."CostPrice"
                                ELSE -movement."QuantityOut" * previous.average_cost_after
                            END,
                            2) AS cost_impact
                ) AS calculation
            )
            UPDATE "StockMovements" AS movement
            SET "QuantityBefore" = replay.quantity_before,
                "QuantityAfter" = replay.quantity_after,
                "AverageCostBefore" = replay.average_cost_before,
                "AverageCostAfter" = replay.average_cost_after,
                "InventoryValueBefore" = replay.inventory_value_before,
                "InventoryValueAfter" = replay.inventory_value_after,
                "CostImpact" = replay.cost_impact,
                "ValuationMethod" = 'WeightedAverage',
                "CostPrice" = CASE
                    WHEN movement."QuantityOut" > 0 THEN replay.average_cost_before
                    ELSE movement."CostPrice"
                END
            FROM replay
            WHERE movement."Id" = replay."Id";

            WITH totals AS (
                SELECT
                    movement."StockId",
                    SUM(movement."QuantityIn") AS quantity_in,
                    SUM(movement."QuantityOut") AS quantity_out
                FROM "StockMovements" AS movement
                INNER JOIN "Stocks" AS stock ON stock."Id" = movement."StockId"
                WHERE movement."StockId" IS NOT NULL
                  AND stock."IsOFB" = false
                GROUP BY movement."StockId"
            ),
            latest AS (
                SELECT DISTINCT ON (movement."StockId")
                    movement."StockId",
                    movement."AverageCostAfter"
                FROM "StockMovements" AS movement
                INNER JOIN "Stocks" AS stock ON stock."Id" = movement."StockId"
                WHERE movement."StockId" IS NOT NULL
                  AND stock."IsOFB" = false
                ORDER BY movement."StockId", movement."OnDate" DESC, movement."CreatedAt" DESC, movement."Id" DESC
            )
            UPDATE "Stocks" AS stock
            SET "PurchaseQty" = totals.quantity_in,
                "SoldQty" = totals.quantity_out,
                "CostPrice" = latest."AverageCostAfter"
            FROM totals
            INNER JOIN latest ON latest."StockId" = totals."StockId"
            WHERE stock."Id" = totals."StockId"
              AND stock."IsOFB" = false;
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            ALTER TABLE "StockMovements" DROP COLUMN IF EXISTS "QuantityBefore";
            ALTER TABLE "StockMovements" DROP COLUMN IF EXISTS "QuantityAfter";
            ALTER TABLE "StockMovements" DROP COLUMN IF EXISTS "AverageCostBefore";
            ALTER TABLE "StockMovements" DROP COLUMN IF EXISTS "AverageCostAfter";
            ALTER TABLE "StockMovements" DROP COLUMN IF EXISTS "InventoryValueBefore";
            ALTER TABLE "StockMovements" DROP COLUMN IF EXISTS "InventoryValueAfter";
            ALTER TABLE "StockMovements" DROP COLUMN IF EXISTS "CostImpact";
            ALTER TABLE "StockMovements" DROP COLUMN IF EXISTS "ValuationMethod";
            ALTER TABLE "Stocks" ALTER COLUMN "CostPrice" TYPE numeric(18,2);
            ALTER TABLE "StockMovements" ALTER COLUMN "CostPrice" TYPE numeric(18,2);
            """);
    }
}
