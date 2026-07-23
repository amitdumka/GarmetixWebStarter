using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Garmetix.Infrastructure.Data.Migrations
{
    public partial class AddInvoiceBookDate : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "BookDate",
                table: "SalesInvoices",
                type: "timestamp without time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "BookDateUpdatedAt",
                table: "SalesInvoices",
                type: "timestamp without time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BookDateUpdatedBy",
                table: "SalesInvoices",
                type: "text",
                nullable: true);

            migrationBuilder.Sql("UPDATE \"SalesInvoices\" SET \"BookDate\" = \"OnDate\" WHERE \"BookDate\" IS NULL;");

            migrationBuilder.AlterColumn<DateTime>(
                name: "BookDate",
                table: "SalesInvoices",
                type: "timestamp without time zone",
                nullable: false,
                oldClrType: typeof(DateTime),
                oldType: "timestamp without time zone",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_SalesInvoices_CompanyId_StoreId_BookDate",
                table: "SalesInvoices",
                columns: new[] { "CompanyId", "StoreId", "BookDate" });
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_SalesInvoices_CompanyId_StoreId_BookDate",
                table: "SalesInvoices");

            migrationBuilder.DropColumn(name: "BookDateUpdatedBy", table: "SalesInvoices");
            migrationBuilder.DropColumn(name: "BookDateUpdatedAt", table: "SalesInvoices");
            migrationBuilder.DropColumn(name: "BookDate", table: "SalesInvoices");
        }
    }
}
