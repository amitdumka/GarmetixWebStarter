using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Garmetix.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddPartyGstinVerification : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            AddGstinColumns(migrationBuilder, "Customers");
            AddGstinColumns(migrationBuilder, "Vendors");

            migrationBuilder.CreateIndex(
                name: "IX_Customers_CompanyId_GSTIN",
                table: "Customers",
                columns: new[] { "CompanyId", "GSTIN" });

            migrationBuilder.CreateIndex(
                name: "IX_Vendors_CompanyId_GSTIN",
                table: "Vendors",
                columns: new[] { "CompanyId", "GSTIN" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(name: "IX_Customers_CompanyId_GSTIN", table: "Customers");
            migrationBuilder.DropIndex(name: "IX_Vendors_CompanyId_GSTIN", table: "Vendors");

            DropGstinColumns(migrationBuilder, "Customers");
            DropGstinColumns(migrationBuilder, "Vendors");
        }

        private static void AddGstinColumns(MigrationBuilder migrationBuilder, string table)
        {
            migrationBuilder.AddColumn<string>(name: "GSTLegalName", table: table, type: "text", nullable: true);
            migrationBuilder.AddColumn<string>(name: "GSTTradeName", table: table, type: "text", nullable: true);
            migrationBuilder.AddColumn<string>(name: "GSTPrincipalAddress", table: table, type: "text", nullable: true);
            migrationBuilder.AddColumn<string>(name: "GSTStateCode", table: table, type: "text", nullable: true);
            migrationBuilder.AddColumn<string>(name: "GSTTaxpayerType", table: table, type: "text", nullable: true);
            migrationBuilder.AddColumn<string>(name: "GSTRegistrationStatus", table: table, type: "text", nullable: true);
            migrationBuilder.AddColumn<bool>(name: "GSTVerified", table: table, type: "boolean", nullable: false, defaultValue: false);
            migrationBuilder.AddColumn<DateTime>(name: "GSTVerifiedAt", table: table, type: "timestamp without time zone", nullable: true);
            migrationBuilder.AddColumn<string>(name: "GSTLookupSource", table: table, type: "text", nullable: true);
            migrationBuilder.AddColumn<string>(name: "GSTMismatchAlert", table: table, type: "text", nullable: true);
        }

        private static void DropGstinColumns(MigrationBuilder migrationBuilder, string table)
        {
            migrationBuilder.DropColumn(name: "GSTLegalName", table: table);
            migrationBuilder.DropColumn(name: "GSTTradeName", table: table);
            migrationBuilder.DropColumn(name: "GSTPrincipalAddress", table: table);
            migrationBuilder.DropColumn(name: "GSTStateCode", table: table);
            migrationBuilder.DropColumn(name: "GSTTaxpayerType", table: table);
            migrationBuilder.DropColumn(name: "GSTRegistrationStatus", table: table);
            migrationBuilder.DropColumn(name: "GSTVerified", table: table);
            migrationBuilder.DropColumn(name: "GSTVerifiedAt", table: table);
            migrationBuilder.DropColumn(name: "GSTLookupSource", table: table);
            migrationBuilder.DropColumn(name: "GSTMismatchAlert", table: table);
        }
    }
}
