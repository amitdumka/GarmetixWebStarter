using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Garmetix.Infrastructure.Data.Migrations;

[DbContext(typeof(GarmetixDbContext))]
[Migration("20260616090000_AddCashVoucherConversionAudit")]
public partial class AddCashVoucherConversionAudit : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "CashVoucherConversions",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                Direction = table.Column<string>(type: "text", nullable: false),
                CashVoucherId = table.Column<Guid>(type: "uuid", nullable: false),
                VoucherId = table.Column<Guid>(type: "uuid", nullable: false),
                CashVoucherNumber = table.Column<string>(type: "text", nullable: false),
                VoucherNumber = table.Column<string>(type: "text", nullable: false),
                VoucherType = table.Column<int>(type: "integer", nullable: false),
                Amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                PartyName = table.Column<string>(type: "text", nullable: false),
                Particulars = table.Column<string>(type: "text", nullable: false),
                Reason = table.Column<string>(type: "text", nullable: false),
                ConvertedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                ConvertedByUserName = table.Column<string>(type: "text", nullable: false),
                ConvertedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                UpdatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                Synced = table.Column<bool>(type: "boolean", nullable: false),
                Deleted = table.Column<bool>(type: "boolean", nullable: false),
                CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                CreatedBy = table.Column<string>(type: "text", nullable: true),
                StoreGroupId = table.Column<Guid>(type: "uuid", nullable: false),
                StoreId = table.Column<Guid>(type: "uuid", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_CashVoucherConversions", x => x.Id);
            });

        migrationBuilder.CreateIndex(
            name: "IX_CashVoucherConversions_CashVoucherId_VoucherId",
            table: "CashVoucherConversions",
            columns: new[] { "CashVoucherId", "VoucherId" });

        migrationBuilder.CreateIndex(
            name: "IX_CashVoucherConversions_CompanyId_StoreId_ConvertedAt",
            table: "CashVoucherConversions",
            columns: new[] { "CompanyId", "StoreId", "ConvertedAt" });
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "CashVoucherConversions");
    }
}
