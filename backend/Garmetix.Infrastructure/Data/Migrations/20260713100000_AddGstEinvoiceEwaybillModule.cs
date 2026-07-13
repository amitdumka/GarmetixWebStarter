using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Garmetix.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddGstEinvoiceEwaybillModule : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "GstEinvoiceIrnRecords",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    Synced = table.Column<bool>(type: "boolean", nullable: false),
                    Deleted = table.Column<bool>(type: "boolean", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    InvoiceId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProviderId = table.Column<Guid>(type: "uuid", nullable: true),
                    Irn = table.Column<string>(type: "text", nullable: true),
                    AckNumber = table.Column<string>(type: "text", nullable: true),
                    AckDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    SignedInvoiceJson = table.Column<string>(type: "text", nullable: true),
                    SignedQrCode = table.Column<string>(type: "text", nullable: true),
                    Status = table.Column<string>(type: "text", nullable: false),
                    CancelReason = table.Column<string>(type: "text", nullable: true),
                    CancelledAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    ErrorCode = table.Column<string>(type: "text", nullable: true),
                    ErrorMessage = table.Column<string>(type: "text", nullable: true),
                    LastAttemptAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table => table.PrimaryKey("PK_GstEinvoiceIrnRecords", x => x.Id));

            migrationBuilder.CreateTable(
                name: "GstEwaybillRecords",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    Synced = table.Column<bool>(type: "boolean", nullable: false),
                    Deleted = table.Column<bool>(type: "boolean", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    InvoiceId = table.Column<Guid>(type: "uuid", nullable: true),
                    PurchaseInvoiceId = table.Column<Guid>(type: "uuid", nullable: true),
                    ProviderId = table.Column<Guid>(type: "uuid", nullable: true),
                    EwbNumber = table.Column<string>(type: "text", nullable: true),
                    EwbDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    ValidUpto = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    VehicleNumber = table.Column<string>(type: "text", nullable: true),
                    TransporterId = table.Column<string>(type: "text", nullable: true),
                    TransporterName = table.Column<string>(type: "text", nullable: true),
                    DistanceKm = table.Column<int>(type: "integer", nullable: true),
                    Status = table.Column<string>(type: "text", nullable: false),
                    CancelReason = table.Column<string>(type: "text", nullable: true),
                    CancelledAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    ErrorCode = table.Column<string>(type: "text", nullable: true),
                    ErrorMessage = table.Column<string>(type: "text", nullable: true),
                    LastAttemptAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table => table.PrimaryKey("PK_GstEwaybillRecords", x => x.Id));

            migrationBuilder.CreateIndex(name: "IX_GstEinvoiceIrnRecords_CompanyId_InvoiceId", table: "GstEinvoiceIrnRecords", columns: new[] { "CompanyId", "InvoiceId" }, unique: true);
            migrationBuilder.CreateIndex(name: "IX_GstEinvoiceIrnRecords_CompanyId_Status", table: "GstEinvoiceIrnRecords", columns: new[] { "CompanyId", "Status" });
            migrationBuilder.CreateIndex(name: "IX_GstEwaybillRecords_CompanyId_InvoiceId", table: "GstEwaybillRecords", columns: new[] { "CompanyId", "InvoiceId" });
            migrationBuilder.CreateIndex(name: "IX_GstEwaybillRecords_CompanyId_PurchaseInvoiceId", table: "GstEwaybillRecords", columns: new[] { "CompanyId", "PurchaseInvoiceId" });
            migrationBuilder.CreateIndex(name: "IX_GstEwaybillRecords_CompanyId_Status", table: "GstEwaybillRecords", columns: new[] { "CompanyId", "Status" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "GstEinvoiceIrnRecords");
            migrationBuilder.DropTable(name: "GstEwaybillRecords");
        }
    }
}
