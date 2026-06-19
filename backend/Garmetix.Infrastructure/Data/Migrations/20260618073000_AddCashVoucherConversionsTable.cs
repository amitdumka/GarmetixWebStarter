
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Garmetix.Infrastructure.Data.Migrations;

public partial class AddCashVoucherConversionsTable : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            CREATE TABLE IF NOT EXISTS "CashVoucherConversions" (
                "Id" uuid NOT NULL,
                "CreatedAt" timestamp without time zone NOT NULL DEFAULT now(),
                "UpdatedAt" timestamp without time zone NULL,
                "Synced" boolean NOT NULL DEFAULT false,
                "Deleted" boolean NOT NULL DEFAULT false,
                "CompanyId" uuid NOT NULL,
                "CreatedBy" text NULL,
                "StoreGroupId" uuid NOT NULL,
                "StoreId" uuid NOT NULL,
                "Direction" text NOT NULL DEFAULT '',
                "CashVoucherId" uuid NOT NULL,
                "VoucherId" uuid NOT NULL,
                "CashVoucherNumber" text NOT NULL DEFAULT '',
                "VoucherNumber" text NOT NULL DEFAULT '',
                "VoucherType" integer NOT NULL DEFAULT 0,
                "Amount" numeric(18,2) NOT NULL DEFAULT 0,
                "PartyName" text NOT NULL DEFAULT '',
                "Particulars" text NOT NULL DEFAULT '',
                "Reason" text NOT NULL DEFAULT '',
                "ConvertedByUserId" uuid NOT NULL DEFAULT '00000000-0000-0000-0000-000000000000',
                "ConvertedByUserName" text NOT NULL DEFAULT '',
                "ConvertedAt" timestamp without time zone NOT NULL DEFAULT now(),
                CONSTRAINT "PK_CashVoucherConversions" PRIMARY KEY ("Id")
            );

            CREATE INDEX IF NOT EXISTS "IX_CashVoucherConversions_CompanyId_CashVoucherId" ON "CashVoucherConversions" ("CompanyId", "CashVoucherId");
            CREATE INDEX IF NOT EXISTS "IX_CashVoucherConversions_CompanyId_VoucherId" ON "CashVoucherConversions" ("CompanyId", "VoucherId");
            CREATE INDEX IF NOT EXISTS "IX_CashVoucherConversions_CompanyId_StoreId_ConvertedAt" ON "CashVoucherConversions" ("CompanyId", "StoreId", "ConvertedAt");
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "CashVoucherConversions");
    }
}
