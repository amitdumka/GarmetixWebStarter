using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Garmetix.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddSaaSManagerModule : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SaaSClients",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    Synced = table.Column<bool>(type: "boolean", nullable: false),
                    Deleted = table.Column<bool>(type: "boolean", nullable: false),
                    ClientCode = table.Column<string>(type: "text", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Email = table.Column<string>(type: "text", nullable: true),
                    Mobile = table.Column<string>(type: "text", nullable: true),
                    Address = table.Column<string>(type: "text", nullable: true),
                    City = table.Column<string>(type: "text", nullable: true),
                    State = table.Column<string>(type: "text", nullable: true),
                    Country = table.Column<string>(type: "text", nullable: false, defaultValue: "India"),
                    ZipCode = table.Column<string>(type: "text", nullable: true),
                    Gstin = table.Column<string>(type: "text", nullable: true),
                    Active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true)
                },
                constraints: table => table.PrimaryKey("PK_SaaSClients", x => x.Id));

            migrationBuilder.CreateTable(
                name: "SaaSPlans",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    Synced = table.Column<bool>(type: "boolean", nullable: false),
                    Deleted = table.Column<bool>(type: "boolean", nullable: false),
                    PlanName = table.Column<string>(type: "text", nullable: false),
                    MaxCompanies = table.Column<int>(type: "integer", nullable: false, defaultValue: 1),
                    MaxStoreGroups = table.Column<int>(type: "integer", nullable: false, defaultValue: 1),
                    MaxStores = table.Column<int>(type: "integer", nullable: false, defaultValue: 2),
                    MaxUsers = table.Column<int>(type: "integer", nullable: false, defaultValue: 20),
                    IncludedModulesCsv = table.Column<string>(type: "text", nullable: false),
                    Active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true)
                },
                constraints: table => table.PrimaryKey("PK_SaaSPlans", x => x.Id));

            migrationBuilder.CreateTable(
                name: "SaaSTokens",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    Synced = table.Column<bool>(type: "boolean", nullable: false),
                    Deleted = table.Column<bool>(type: "boolean", nullable: false),
                    TokenString = table.Column<string>(type: "text", nullable: false),
                    SaaSClientId = table.Column<Guid>(type: "uuid", nullable: false),
                    SaaSPlanId = table.Column<Guid>(type: "uuid", nullable: false),
                    ValidityDays = table.Column<int>(type: "integer", nullable: false, defaultValue: 365),
                    ExpiresAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    IsActivated = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    ActivatedAtUtc = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    ActivatedCompanyId = table.Column<Guid>(type: "uuid", nullable: true),
                    Notes = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table => table.PrimaryKey("PK_SaaSTokens", x => x.Id));

            migrationBuilder.CreateTable(
                name: "TenantSubscriptions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    Synced = table.Column<bool>(type: "boolean", nullable: false),
                    Deleted = table.Column<bool>(type: "boolean", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    SaaSClientId = table.Column<Guid>(type: "uuid", nullable: false),
                    SaaSPlanId = table.Column<Guid>(type: "uuid", nullable: false),
                    SaaSTokenId = table.Column<Guid>(type: "uuid", nullable: true),
                    PlanName = table.Column<string>(type: "text", nullable: false),
                    MaxCompanies = table.Column<int>(type: "integer", nullable: false, defaultValue: 1),
                    MaxStoreGroups = table.Column<int>(type: "integer", nullable: false, defaultValue: 1),
                    MaxStores = table.Column<int>(type: "integer", nullable: false, defaultValue: 2),
                    MaxUsers = table.Column<int>(type: "integer", nullable: false, defaultValue: 20),
                    ValidFrom = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    ValidTo = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true)
                },
                constraints: table => table.PrimaryKey("PK_TenantSubscriptions", x => x.Id));

            migrationBuilder.AddColumn<Guid>(name: "SaaSClientId", table: "Companies", type: "uuid", nullable: true);
            migrationBuilder.CreateIndex(name: "IX_Companies_SaaSClientId", table: "Companies", column: "SaaSClientId");

            migrationBuilder.CreateIndex(name: "IX_SaaSClients_ClientCode", table: "SaaSClients", column: "ClientCode", unique: true);
            migrationBuilder.CreateIndex(name: "IX_SaaSTokens_TokenString", table: "SaaSTokens", column: "TokenString", unique: true);
            migrationBuilder.CreateIndex(name: "IX_SaaSTokens_SaaSClientId_IsActivated", table: "SaaSTokens", columns: new[] { "SaaSClientId", "IsActivated" });
            migrationBuilder.CreateIndex(name: "IX_TenantSubscriptions_CompanyId_IsActive", table: "TenantSubscriptions", columns: new[] { "CompanyId", "IsActive" });
            migrationBuilder.CreateIndex(name: "IX_TenantSubscriptions_SaaSClientId_IsActive", table: "TenantSubscriptions", columns: new[] { "SaaSClientId", "IsActive" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "SaaSClientId", table: "Companies");
            migrationBuilder.DropTable(name: "TenantSubscriptions");
            migrationBuilder.DropTable(name: "SaaSTokens");
            migrationBuilder.DropTable(name: "SaaSPlans");
            migrationBuilder.DropTable(name: "SaaSClients");
        }
    }
}
