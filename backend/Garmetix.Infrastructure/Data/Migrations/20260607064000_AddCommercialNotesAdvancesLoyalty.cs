using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Garmetix.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddCommercialNotesAdvancesLoyalty : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CommercialNotes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    NoteNumber = table.Column<string>(type: "text", nullable: false),
                    NoteType = table.Column<int>(type: "integer", nullable: false),
                    OnDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    PartyType = table.Column<int>(type: "integer", nullable: false),
                    PartyId = table.Column<Guid>(type: "uuid", nullable: true),
                    CustomerId = table.Column<Guid>(type: "uuid", nullable: true),
                    VendorId = table.Column<Guid>(type: "uuid", nullable: true),
                    PartyName = table.Column<string>(type: "text", nullable: false),
                    PartyGstin = table.Column<string>(type: "text", nullable: true),
                    SourceType = table.Column<string>(type: "text", nullable: false),
                    SourceId = table.Column<Guid>(type: "uuid", nullable: true),
                    SourceNumber = table.Column<string>(type: "text", nullable: true),
                    Reason = table.Column<string>(type: "text", nullable: false),
                    TaxableAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    TaxAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    IsAdjusted = table.Column<bool>(type: "boolean", nullable: false),
                    AdjustedAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Printed = table.Column<bool>(type: "boolean", nullable: false),
                    Remarks = table.Column<string>(type: "text", nullable: true),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    StoreGroupId = table.Column<Guid>(type: "uuid", nullable: false),
                    StoreId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    Synced = table.Column<bool>(type: "boolean", nullable: false),
                    Deleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CommercialNotes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CustomerAdvanceReceipts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ReceiptNumber = table.Column<string>(type: "text", nullable: false),
                    OnDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    CustomerId = table.Column<Guid>(type: "uuid", nullable: false),
                    CustomerName = table.Column<string>(type: "text", nullable: false),
                    CustomerMobileNumber = table.Column<string>(type: "text", nullable: true),
                    Amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    AdjustedAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    AvailableAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    PaymentMode = table.Column<int>(type: "integer", nullable: false),
                    BankAccountId = table.Column<Guid>(type: "uuid", nullable: true),
                    ReferenceNumber = table.Column<string>(type: "text", nullable: true),
                    Remarks = table.Column<string>(type: "text", nullable: true),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    StoreGroupId = table.Column<Guid>(type: "uuid", nullable: false),
                    StoreId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    Synced = table.Column<bool>(type: "boolean", nullable: false),
                    Deleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CustomerAdvanceReceipts", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "LoyaltyPrograms",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Enabled = table.Column<bool>(type: "boolean", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    EarnPointsPerRupee = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    RedeemValuePerPoint = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    MinimumBillAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    ExpiryDays = table.Column<int>(type: "integer", nullable: true),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    StoreGroupId = table.Column<Guid>(type: "uuid", nullable: false),
                    StoreId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    Synced = table.Column<bool>(type: "boolean", nullable: false),
                    Deleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LoyaltyPrograms", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "LoyaltyPointLedgers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CustomerId = table.Column<Guid>(type: "uuid", nullable: false),
                    CustomerName = table.Column<string>(type: "text", nullable: false),
                    OnDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    SourceType = table.Column<string>(type: "text", nullable: false),
                    SourceId = table.Column<Guid>(type: "uuid", nullable: true),
                    SourceNumber = table.Column<string>(type: "text", nullable: true),
                    PointsIn = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    PointsOut = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    BalanceAfter = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Remarks = table.Column<string>(type: "text", nullable: true),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    StoreGroupId = table.Column<Guid>(type: "uuid", nullable: false),
                    StoreId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    Synced = table.Column<bool>(type: "boolean", nullable: false),
                    Deleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LoyaltyPointLedgers", x => x.Id);
                });

            migrationBuilder.CreateIndex(name: "IX_CommercialNotes_CompanyId_StoreId_NoteNumber", table: "CommercialNotes", columns: new[] { "CompanyId", "StoreId", "NoteNumber" });
            migrationBuilder.CreateIndex(name: "IX_CommercialNotes_CompanyId_PartyType_PartyName", table: "CommercialNotes", columns: new[] { "CompanyId", "PartyType", "PartyName" });
            migrationBuilder.CreateIndex(name: "IX_CustomerAdvanceReceipts_CompanyId_StoreId_ReceiptNumber", table: "CustomerAdvanceReceipts", columns: new[] { "CompanyId", "StoreId", "ReceiptNumber" });
            migrationBuilder.CreateIndex(name: "IX_CustomerAdvanceReceipts_CompanyId_CustomerId_OnDate", table: "CustomerAdvanceReceipts", columns: new[] { "CompanyId", "CustomerId", "OnDate" });
            migrationBuilder.CreateIndex(name: "IX_LoyaltyPrograms_CompanyId_StoreId", table: "LoyaltyPrograms", columns: new[] { "CompanyId", "StoreId" });
            migrationBuilder.CreateIndex(name: "IX_LoyaltyPointLedgers_CompanyId_CustomerId_OnDate", table: "LoyaltyPointLedgers", columns: new[] { "CompanyId", "CustomerId", "OnDate" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "CommercialNotes");
            migrationBuilder.DropTable(name: "CustomerAdvanceReceipts");
            migrationBuilder.DropTable(name: "LoyaltyPointLedgers");
            migrationBuilder.DropTable(name: "LoyaltyPrograms");
        }
    }
}
