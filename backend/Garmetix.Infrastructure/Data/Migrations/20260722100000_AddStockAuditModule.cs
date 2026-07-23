using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Garmetix.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddStockAuditModule : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "StockAuditPeriods",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    Synced = table.Column<bool>(type: "boolean", nullable: false),
                    Deleted = table.Column<bool>(type: "boolean", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    StoreGroupId = table.Column<Guid>(type: "uuid", nullable: false),
                    StoreId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    Name = table.Column<string>(type: "text", nullable: false),
                    StartDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    EndDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    Notes = table.Column<string>(type: "text", nullable: true),
                    ClosedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table => table.PrimaryKey("PK_StockAuditPeriods", x => x.Id));

            migrationBuilder.CreateTable(
                name: "StockAuditScans",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    Synced = table.Column<bool>(type: "boolean", nullable: false),
                    Deleted = table.Column<bool>(type: "boolean", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    StoreGroupId = table.Column<Guid>(type: "uuid", nullable: false),
                    StoreId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    AuditPeriodId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProductId = table.Column<Guid>(type: "uuid", nullable: true),
                    StockId = table.Column<Guid>(type: "uuid", nullable: true),
                    Barcode = table.Column<string>(type: "text", nullable: false),
                    ScanDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    Quantity = table.Column<decimal>(type: "numeric", nullable: false),
                    ScanCount = table.Column<int>(type: "integer", nullable: false),
                    FirstScannedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    LastScannedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    ProductName = table.Column<string>(type: "text", nullable: false),
                    CategoryName = table.Column<string>(type: "text", nullable: true),
                    SubCategoryName = table.Column<string>(type: "text", nullable: true),
                    Color = table.Column<string>(type: "text", nullable: true),
                    Size = table.Column<string>(type: "text", nullable: true),
                    Unit = table.Column<string>(type: "text", nullable: true),
                    MRP = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    CostPrice = table.Column<decimal>(type: "numeric(18,4)", nullable: false)
                },
                constraints: table => table.PrimaryKey("PK_StockAuditScans", x => x.Id));

            migrationBuilder.CreateIndex(
                name: "IX_StockAuditPeriods_CompanyId_StoreId_Status",
                table: "StockAuditPeriods",
                columns: new[] { "CompanyId", "StoreId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_StockAuditScans_AuditPeriodId_StoreId_Barcode_ScanDate",
                table: "StockAuditScans",
                columns: new[] { "AuditPeriodId", "StoreId", "Barcode", "ScanDate" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_StockAuditScans_AuditPeriodId_ScanDate",
                table: "StockAuditScans",
                columns: new[] { "AuditPeriodId", "ScanDate" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "StockAuditScans");
            migrationBuilder.DropTable(name: "StockAuditPeriods");
        }
    }
}
