using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Garmetix.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddVoucherCashTransaction : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
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

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
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
        }
    }
}
