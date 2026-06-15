using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Garmetix.Infrastructure.Data.Migrations;

[DbContext(typeof(GarmetixDbContext))]
[Migration("20260615083000_AddPurchaseReturnItcReconciliation")]
public partial class AddPurchaseReturnItcReconciliation : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            ALTER TABLE "PurchaseReturns" ADD COLUMN IF NOT EXISTS "ItcReversalAmount" numeric(18,2) NOT NULL DEFAULT 0;
            ALTER TABLE "PurchaseReturns" ADD COLUMN IF NOT EXISTS "ItcReversalStatus" text NOT NULL DEFAULT 'Pending';
            ALTER TABLE "PurchaseReturns" ADD COLUMN IF NOT EXISTS "JournalEntryId" uuid NULL;

            CREATE TABLE IF NOT EXISTS "PurchaseReturnItcReversals" (
                "Id" uuid NOT NULL,
                "PurchaseReturnId" uuid NOT NULL,
                "PurchaseReturnItemId" uuid NOT NULL,
                "PurchaseInvoiceId" uuid NOT NULL,
                "PurchaseInvoiceItemId" uuid NOT NULL,
                "ReturnNumber" text NOT NULL DEFAULT '',
                "OriginalInvoiceNumber" text NOT NULL DEFAULT '',
                "OnDate" timestamp without time zone NOT NULL,
                "ProductId" uuid NOT NULL,
                "ProductName" text NOT NULL DEFAULT '',
                "HSNCode" text NULL,
                "TaxRate" numeric(18,2) NOT NULL DEFAULT 0,
                "ReturnedQuantity" numeric(18,2) NOT NULL DEFAULT 0,
                "TaxableAmount" numeric(18,2) NOT NULL DEFAULT 0,
                "CGSTAmount" numeric(18,2) NOT NULL DEFAULT 0,
                "SGSTAmount" numeric(18,2) NOT NULL DEFAULT 0,
                "IGSTAmount" numeric(18,2) NOT NULL DEFAULT 0,
                "TaxAmount" numeric(18,2) NOT NULL DEFAULT 0,
                "JournalEntryId" uuid NULL,
                "Status" text NOT NULL DEFAULT 'Posted',
                "CompanyId" uuid NOT NULL,
                "CreatedBy" text NULL,
                "StoreGroupId" uuid NOT NULL,
                "StoreId" uuid NOT NULL,
                "CreatedAt" timestamp without time zone NOT NULL DEFAULT now(),
                "UpdatedAt" timestamp without time zone NULL,
                "Synced" boolean NOT NULL DEFAULT false,
                "Deleted" boolean NOT NULL DEFAULT false,
                CONSTRAINT "PK_PurchaseReturnItcReversals" PRIMARY KEY ("Id")
            );

            CREATE INDEX IF NOT EXISTS "IX_PurchaseReturnItcReversals_CompanyId_PurchaseReturnId"
                ON "PurchaseReturnItcReversals" ("CompanyId", "PurchaseReturnId");
            CREATE INDEX IF NOT EXISTS "IX_PurchaseReturnItcReversals_CompanyId_PurchaseInvoiceId_PurchaseInvoiceItemId"
                ON "PurchaseReturnItcReversals" ("CompanyId", "PurchaseInvoiceId", "PurchaseInvoiceItemId");
            CREATE INDEX IF NOT EXISTS "IX_PurchaseReturnItcReversals_CompanyId_JournalEntryId"
                ON "PurchaseReturnItcReversals" ("CompanyId", "JournalEntryId");

            UPDATE "PurchaseReturns" AS purchase_return
            SET "JournalEntryId" = journal."Id"
            FROM "JournalEntries" AS journal
            WHERE purchase_return."JournalEntryId" IS NULL
              AND (
                (journal."SourceType" = 'PurchaseReturn' AND journal."SourceId" = purchase_return."Id")
                OR (
                    purchase_return."ReturnKind" = 'Cancellation'
                    AND journal."SourceType" = 'PurchaseInvoiceCancellation'
                    AND journal."SourceId" = purchase_return."PurchaseInvoiceId"
                )
              );

            INSERT INTO "PurchaseReturnItcReversals" (
                "Id", "PurchaseReturnId", "PurchaseReturnItemId", "PurchaseInvoiceId", "PurchaseInvoiceItemId",
                "ReturnNumber", "OriginalInvoiceNumber", "OnDate", "ProductId", "ProductName", "HSNCode",
                "TaxRate", "ReturnedQuantity", "TaxableAmount", "CGSTAmount", "SGSTAmount", "IGSTAmount",
                "TaxAmount", "JournalEntryId", "Status", "CompanyId", "CreatedBy", "StoreGroupId", "StoreId",
                "CreatedAt", "UpdatedAt", "Synced", "Deleted"
            )
            SELECT
                item."Id", item."PurchaseReturnId", item."Id", item."PurchaseInvoiceId", item."PurchaseInvoiceItemId",
                purchase_return."ReturnNumber", purchase_return."OriginalInvoiceNumber", purchase_return."OnDate",
                item."ProductId", item."ProductName", item."HSNCode", item."TaxRate", item."ReturnedQuantity",
                item."TaxableAmount", item."CGSTAmount", item."SGSTAmount", item."IGSTAmount", item."TaxAmount",
                purchase_return."JournalEntryId", 'Backfilled', item."CompanyId", item."CreatedBy",
                purchase_return."StoreGroupId", purchase_return."StoreId", item."CreatedAt", item."UpdatedAt",
                item."Synced", item."Deleted"
            FROM "PurchaseReturnItems" AS item
            INNER JOIN "PurchaseReturns" AS purchase_return ON purchase_return."Id" = item."PurchaseReturnId"
            ON CONFLICT ("Id") DO NOTHING;

            UPDATE "PurchaseReturns" AS purchase_return
            SET "ItcReversalAmount" = reversal.total_tax,
                "ItcReversalStatus" = CASE
                    WHEN reversal.total_tax = purchase_return."TaxAmount" THEN 'Reconciled'
                    ELSE 'Mismatch'
                END
            FROM (
                SELECT "PurchaseReturnId", ROUND(SUM("TaxAmount"), 2) AS total_tax
                FROM "PurchaseReturnItcReversals"
                WHERE NOT "Deleted"
                GROUP BY "PurchaseReturnId"
            ) AS reversal
            WHERE reversal."PurchaseReturnId" = purchase_return."Id";
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            DROP TABLE IF EXISTS "PurchaseReturnItcReversals";
            ALTER TABLE "PurchaseReturns" DROP COLUMN IF EXISTS "JournalEntryId";
            ALTER TABLE "PurchaseReturns" DROP COLUMN IF EXISTS "ItcReversalStatus";
            ALTER TABLE "PurchaseReturns" DROP COLUMN IF EXISTS "ItcReversalAmount";
            """);
    }
}
