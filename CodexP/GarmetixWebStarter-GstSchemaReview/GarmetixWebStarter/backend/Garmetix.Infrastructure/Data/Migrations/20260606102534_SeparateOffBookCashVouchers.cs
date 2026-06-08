using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Garmetix.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class SeparateOffBookCashVouchers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Vouchers_Transactions_TransactionId",
                table: "Vouchers");

            migrationBuilder.DropIndex(
                name: "IX_Vouchers_TransactionId",
                table: "Vouchers");

            migrationBuilder.DropColumn(
                name: "TransactionId",
                table: "Vouchers");

            migrationBuilder.CreateTable(
                name: "CashVouchers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    VoucherNumber = table.Column<string>(type: "text", nullable: false),
                    OnDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    VoucherType = table.Column<int>(type: "integer", nullable: false),
                    PartyName = table.Column<string>(type: "text", nullable: false),
                    Particulars = table.Column<string>(type: "text", nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Remarks = table.Column<string>(type: "text", nullable: false),
                    SlipNumber = table.Column<string>(type: "text", nullable: true),
                    LedgerId = table.Column<Guid>(type: "uuid", nullable: true),
                    EmployeeId = table.Column<Guid>(type: "uuid", nullable: true),
                    TransactionId = table.Column<Guid>(type: "uuid", nullable: false),
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
                    table.PrimaryKey("PK_CashVouchers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CashVouchers_Employees_EmployeeId",
                        column: x => x.EmployeeId,
                        principalTable: "Employees",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_CashVouchers_Ledgers_LedgerId",
                        column: x => x.LedgerId,
                        principalTable: "Ledgers",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_CashVouchers_Transactions_TransactionId",
                        column: x => x.TransactionId,
                        principalTable: "Transactions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CashVouchers_CompanyId_StoreId_VoucherNumber",
                table: "CashVouchers",
                columns: new[] { "CompanyId", "StoreId", "VoucherNumber" });

            migrationBuilder.CreateIndex(
                name: "IX_CashVouchers_EmployeeId",
                table: "CashVouchers",
                column: "EmployeeId");

            migrationBuilder.CreateIndex(
                name: "IX_CashVouchers_LedgerId",
                table: "CashVouchers",
                column: "LedgerId");

            migrationBuilder.CreateIndex(
                name: "IX_CashVouchers_TransactionId",
                table: "CashVouchers",
                column: "TransactionId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CashVouchers");

            migrationBuilder.AddColumn<Guid>(
                name: "TransactionId",
                table: "Vouchers",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Vouchers_TransactionId",
                table: "Vouchers",
                column: "TransactionId");

            migrationBuilder.AddForeignKey(
                name: "FK_Vouchers_Transactions_TransactionId",
                table: "Vouchers",
                column: "TransactionId",
                principalTable: "Transactions",
                principalColumn: "Id");
        }
    }
}
