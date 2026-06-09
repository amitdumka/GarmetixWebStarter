using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Garmetix.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddGstReturnDrafts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "GstReturnDrafts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    Synced = table.Column<bool>(type: "boolean", nullable: false),
                    Deleted = table.Column<bool>(type: "boolean", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    Form = table.Column<string>(type: "text", nullable: false),
                    Gstin = table.Column<string>(type: "text", nullable: false),
                    ReturnPeriod = table.Column<string>(type: "text", nullable: false),
                    Title = table.Column<string>(type: "text", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    PayloadJson = table.Column<string>(type: "text", nullable: false),
                    LastPreviewIssuesJson = table.Column<string>(type: "text", nullable: false),
                    RowCount = table.Column<int>(type: "integer", nullable: false),
                    TaxableValue = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    IntegratedTax = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    CentralTax = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    StateTax = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    Cess = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedByUserName = table.Column<string>(type: "text", nullable: false),
                    UpdatedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    UpdatedByUserName = table.Column<string>(type: "text", nullable: false),
                    FiledAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    LockedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GstReturnDrafts", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "GstReturnAuditEntries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    Synced = table.Column<bool>(type: "boolean", nullable: false),
                    Deleted = table.Column<bool>(type: "boolean", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    DraftId = table.Column<Guid>(type: "uuid", nullable: false),
                    Form = table.Column<string>(type: "text", nullable: false),
                    ReturnPeriod = table.Column<string>(type: "text", nullable: false),
                    Gstin = table.Column<string>(type: "text", nullable: false),
                    Action = table.Column<string>(type: "text", nullable: false),
                    Summary = table.Column<string>(type: "text", nullable: false),
                    ActorUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    ActorName = table.Column<string>(type: "text", nullable: false),
                    DetailsJson = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GstReturnAuditEntries", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_GstReturnDrafts_CompanyId_Form_ReturnPeriod_Gstin",
                table: "GstReturnDrafts",
                columns: new[] { "CompanyId", "Form", "ReturnPeriod", "Gstin" });

            migrationBuilder.CreateIndex(
                name: "IX_GstReturnDrafts_CompanyId_Status_UpdatedAt",
                table: "GstReturnDrafts",
                columns: new[] { "CompanyId", "Status", "UpdatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_GstReturnAuditEntries_CompanyId_DraftId_CreatedAt",
                table: "GstReturnAuditEntries",
                columns: new[] { "CompanyId", "DraftId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_GstReturnAuditEntries_CompanyId_Form_ReturnPeriod",
                table: "GstReturnAuditEntries",
                columns: new[] { "CompanyId", "Form", "ReturnPeriod" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "GstReturnAuditEntries");
            migrationBuilder.DropTable(name: "GstReturnDrafts");
        }
    }
}
