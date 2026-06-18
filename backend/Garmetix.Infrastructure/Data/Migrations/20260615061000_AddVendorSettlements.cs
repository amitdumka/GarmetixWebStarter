using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Garmetix.Infrastructure.Data.Migrations;

[DbContext(typeof(GarmetixDbContext))]
[Migration("20260615061000_AddVendorSettlements")]
public partial class AddVendorSettlements : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            ALTER TABLE "PurchaseReturns" ADD COLUMN IF NOT EXISTS "SettledAmount" numeric(18,2) NOT NULL DEFAULT 0;
            ALTER TABLE "PurchaseReturns" ADD COLUMN IF NOT EXISTS "SettlementStatus" text NOT NULL DEFAULT 'Open';
            ALTER TABLE "PurchasePayments" ADD COLUMN IF NOT EXISTS "AdjustmentSourceType" text NULL;
            ALTER TABLE "PurchasePayments" ADD COLUMN IF NOT EXISTS "AdjustmentSourceId" uuid NULL;

            CREATE TABLE IF NOT EXISTS "VendorSettlements" (
                "Id" uuid NOT NULL,
                "SettlementNumber" text NOT NULL DEFAULT '',
                "OnDate" timestamp without time zone NOT NULL,
                "VendorId" uuid NOT NULL,
                "VendorName" text NOT NULL DEFAULT '',
                "PurchaseReturnId" uuid NOT NULL,
                "ReturnNumber" text NOT NULL DEFAULT '',
                "DebitNoteId" uuid NOT NULL,
                "DebitNoteNumber" text NOT NULL DEFAULT '',
                "SettlementType" text NOT NULL DEFAULT 'Adjustment',
                "AdjustedAmount" numeric(18,2) NOT NULL DEFAULT 0,
                "RefundAmount" numeric(18,2) NOT NULL DEFAULT 0,
                "TotalAmount" numeric(18,2) NOT NULL DEFAULT 0,
                "PaymentMode" integer NULL,
                "BankAccountId" uuid NULL,
                "ReferenceNumber" text NULL,
                "VoucherId" uuid NULL,
                "JournalEntryId" uuid NULL,
                "BankTransactionId" uuid NULL,
                "Status" text NOT NULL DEFAULT 'Posted',
                "Remarks" text NULL,
                "CompanyId" uuid NOT NULL,
                "CreatedBy" text NULL,
                "StoreGroupId" uuid NOT NULL,
                "StoreId" uuid NOT NULL,
                "CreatedAt" timestamp without time zone NOT NULL DEFAULT now(),
                "UpdatedAt" timestamp without time zone NULL,
                "Synced" boolean NOT NULL DEFAULT false,
                "Deleted" boolean NOT NULL DEFAULT false,
                CONSTRAINT "PK_VendorSettlements" PRIMARY KEY ("Id")
            );

            CREATE TABLE IF NOT EXISTS "VendorSettlementAllocations" (
                "Id" uuid NOT NULL,
                "VendorSettlementId" uuid NOT NULL,
                "PurchaseInvoiceId" uuid NOT NULL,
                "PurchaseInvoiceNumber" text NOT NULL DEFAULT '',
                "Amount" numeric(18,2) NOT NULL DEFAULT 0,
                "CompanyId" uuid NOT NULL,
                "CreatedBy" text NULL,
                "CreatedAt" timestamp without time zone NOT NULL DEFAULT now(),
                "UpdatedAt" timestamp without time zone NULL,
                "Synced" boolean NOT NULL DEFAULT false,
                "Deleted" boolean NOT NULL DEFAULT false,
                CONSTRAINT "PK_VendorSettlementAllocations" PRIMARY KEY ("Id")
            );

            CREATE INDEX IF NOT EXISTS "IX_VendorSettlements_CompanyId_StoreId_SettlementNumber"
                ON "VendorSettlements" ("CompanyId", "StoreId", "SettlementNumber");
            CREATE INDEX IF NOT EXISTS "IX_VendorSettlements_CompanyId_VendorId_OnDate"
                ON "VendorSettlements" ("CompanyId", "VendorId", "OnDate");
            CREATE INDEX IF NOT EXISTS "IX_VendorSettlements_CompanyId_PurchaseReturnId"
                ON "VendorSettlements" ("CompanyId", "PurchaseReturnId");
            CREATE INDEX IF NOT EXISTS "IX_VendorSettlementAllocations_CompanyId_VendorSettlementId"
                ON "VendorSettlementAllocations" ("CompanyId", "VendorSettlementId");
            CREATE INDEX IF NOT EXISTS "IX_VendorSettlementAllocations_CompanyId_PurchaseInvoiceId"
                ON "VendorSettlementAllocations" ("CompanyId", "PurchaseInvoiceId");

            UPDATE "PurchaseReturns" AS purchase_return
            SET "SettledAmount" = LEAST(note."AdjustedAmount", purchase_return."ReturnAmount"),
                "SettlementStatus" = CASE
                    WHEN note."AdjustedAmount" <= 0 THEN 'Open'
                    WHEN note."AdjustedAmount" >= purchase_return."ReturnAmount" THEN 'Settled'
                    ELSE 'Partially Settled'
                END
            FROM "CommercialNotes" AS note
            WHERE purchase_return."DebitNoteId" = note."Id";
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            DROP TABLE IF EXISTS "VendorSettlementAllocations";
            DROP TABLE IF EXISTS "VendorSettlements";
            ALTER TABLE "PurchasePayments" DROP COLUMN IF EXISTS "AdjustmentSourceId";
            ALTER TABLE "PurchasePayments" DROP COLUMN IF EXISTS "AdjustmentSourceType";
            ALTER TABLE "PurchaseReturns" DROP COLUMN IF EXISTS "SettlementStatus";
            ALTER TABLE "PurchaseReturns" DROP COLUMN IF EXISTS "SettledAmount";
            """);
    }
}
