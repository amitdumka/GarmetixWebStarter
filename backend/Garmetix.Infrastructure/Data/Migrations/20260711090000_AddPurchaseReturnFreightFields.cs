using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Garmetix.Infrastructure.Data.Migrations
{
    public partial class AddPurchaseReturnFreightFields : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "TransportDetails",
                table: "PurchaseReturns",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "FreightAmount",
                table: "PurchaseReturns",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "FreightBearer",
                table: "PurchaseReturns",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "FreightExpenseVoucherId",
                table: "PurchaseReturns",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FreightExpenseVoucherNumber",
                table: "PurchaseReturns",
                type: "text",
                nullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "TransportDetails", table: "PurchaseReturns");
            migrationBuilder.DropColumn(name: "FreightAmount", table: "PurchaseReturns");
            migrationBuilder.DropColumn(name: "FreightBearer", table: "PurchaseReturns");
            migrationBuilder.DropColumn(name: "FreightExpenseVoucherId", table: "PurchaseReturns");
            migrationBuilder.DropColumn(name: "FreightExpenseVoucherNumber", table: "PurchaseReturns");
        }
    }
}
