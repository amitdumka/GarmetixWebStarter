using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Garmetix.Infrastructure.Data.Migrations
{
    public partial class AddEdcSettlement : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "EdcSettlementBatchId",
                table: "InvoicePayments",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "EdcSettlementBatches",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PosMachineAccountId = table.Column<Guid>(type: "uuid", nullable: false),
                    RealBankAccountId = table.Column<Guid>(type: "uuid", nullable: false),
                    SettlementDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: false, defaultValueSql: "now()"),
                    GrossAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false, defaultValue: 0m),
                    NetAmountReceived = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false, defaultValue: 0m),
                    ChargeAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false, defaultValue: 0m),
                    PaymentCount = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    ReferenceNumber = table.Column<string>(type: "text", nullable: true),
                    Remarks = table.Column<string>(type: "text", nullable: true),
                    JournalEntryId = table.Column<Guid>(type: "uuid", nullable: true),
                    JournalEntryNumber = table.Column<string>(type: "text", nullable: true),
                    Reversed = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    ReversedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    ReversedBy = table.Column<string>(type: "text", nullable: true),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    StoreGroupId = table.Column<Guid>(type: "uuid", nullable: false),
                    StoreId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false, defaultValueSql: "now()"),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    Synced = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    Deleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EdcSettlementBatches", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_InvoicePayments_CompanyId_BankAccountId_EdcSettlementBatchId",
                table: "InvoicePayments",
                columns: new[] { "CompanyId", "BankAccountId", "EdcSettlementBatchId" });

            migrationBuilder.CreateIndex(
                name: "IX_EdcSettlementBatches_CompanyId_StoreId_PosMachineAccountId_SettlementDate",
                table: "EdcSettlementBatches",
                columns: new[] { "CompanyId", "StoreId", "PosMachineAccountId", "SettlementDate" });
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "EdcSettlementBatches");
            migrationBuilder.DropIndex(name: "IX_InvoicePayments_CompanyId_BankAccountId_EdcSettlementBatchId", table: "InvoicePayments");
            migrationBuilder.DropColumn(name: "EdcSettlementBatchId", table: "InvoicePayments");
        }
    }
}
