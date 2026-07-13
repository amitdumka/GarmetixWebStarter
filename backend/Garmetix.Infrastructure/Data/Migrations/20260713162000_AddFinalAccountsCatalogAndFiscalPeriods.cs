using System;
using Garmetix.Infrastructure.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Garmetix.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    [DbContext(typeof(GarmetixDbContext))]
    [Migration("20260713162000_AddFinalAccountsCatalogAndFiscalPeriods")]
    public partial class AddFinalAccountsCatalogAndFiscalPeriods : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(name: "final_accounts");

            migrationBuilder.CreateTable(
                name: "fa_account_groups",
                schema: "final_accounts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    Synced = table.Column<bool>(type: "boolean", nullable: false),
                    Deleted = table.Column<bool>(type: "boolean", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: true),
                    StoreGroupId = table.Column<Guid>(type: "uuid", nullable: true),
                    StoreId = table.Column<Guid>(type: "uuid", nullable: true),
                    ParentGroupId = table.Column<Guid>(type: "uuid", nullable: true),
                    Code = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    Name = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    AccountType = table.Column<int>(type: "integer", nullable: false),
                    NaturalBalance = table.Column<int>(type: "integer", nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    IsSystem = table.Column<bool>(type: "boolean", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Revision = table.Column<int>(type: "integer", nullable: false),
                    CreatedBy = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    UpdatedBy = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_fa_account_groups", x => x.Id);
                    table.ForeignKey(
                        name: "FK_fa_account_groups_parent",
                        column: x => x.ParentGroupId,
                        principalSchema: "final_accounts",
                        principalTable: "fa_account_groups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "fa_fiscal_years",
                schema: "final_accounts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    Synced = table.Column<bool>(type: "boolean", nullable: false),
                    Deleted = table.Column<bool>(type: "boolean", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: true),
                    StoreGroupId = table.Column<Guid>(type: "uuid", nullable: true),
                    StoreId = table.Column<Guid>(type: "uuid", nullable: true),
                    Name = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    StartDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    EndDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    ClosedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    Revision = table.Column<int>(type: "integer", nullable: false),
                    CreatedBy = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    UpdatedBy = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_fa_fiscal_years", x => x.Id);
                    table.CheckConstraint("CK_fa_fiscal_years_dates", "\"EndDate\" >= \"StartDate\"");
                });

            migrationBuilder.CreateTable(
                name: "fa_accounts",
                schema: "final_accounts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    Synced = table.Column<bool>(type: "boolean", nullable: false),
                    Deleted = table.Column<bool>(type: "boolean", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: true),
                    StoreGroupId = table.Column<Guid>(type: "uuid", nullable: true),
                    StoreId = table.Column<Guid>(type: "uuid", nullable: true),
                    AccountGroupId = table.Column<Guid>(type: "uuid", nullable: false),
                    ParentAccountId = table.Column<Guid>(type: "uuid", nullable: true),
                    Code = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    Name = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    AccountType = table.Column<int>(type: "integer", nullable: false),
                    NaturalBalance = table.Column<int>(type: "integer", nullable: false),
                    OpeningBalance = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    IsControlAccount = table.Column<bool>(type: "boolean", nullable: false),
                    IsSystem = table.Column<bool>(type: "boolean", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    Revision = table.Column<int>(type: "integer", nullable: false),
                    CreatedBy = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    UpdatedBy = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_fa_accounts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_fa_accounts_group",
                        column: x => x.AccountGroupId,
                        principalSchema: "final_accounts",
                        principalTable: "fa_account_groups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_fa_accounts_parent",
                        column: x => x.ParentAccountId,
                        principalSchema: "final_accounts",
                        principalTable: "fa_accounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "fa_fiscal_periods",
                schema: "final_accounts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    Synced = table.Column<bool>(type: "boolean", nullable: false),
                    Deleted = table.Column<bool>(type: "boolean", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: true),
                    StoreGroupId = table.Column<Guid>(type: "uuid", nullable: true),
                    StoreId = table.Column<Guid>(type: "uuid", nullable: true),
                    FiscalYearId = table.Column<Guid>(type: "uuid", nullable: false),
                    PeriodNumber = table.Column<int>(type: "integer", nullable: false),
                    Name = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    StartDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    EndDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    ClosedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    Revision = table.Column<int>(type: "integer", nullable: false),
                    CreatedBy = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    UpdatedBy = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_fa_fiscal_periods", x => x.Id);
                    table.CheckConstraint("CK_fa_fiscal_periods_dates", "\"EndDate\" >= \"StartDate\"");
                    table.ForeignKey(
                        name: "FK_fa_fiscal_periods_year",
                        column: x => x.FiscalYearId,
                        principalSchema: "final_accounts",
                        principalTable: "fa_fiscal_years",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "fa_account_mappings",
                schema: "final_accounts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    Synced = table.Column<bool>(type: "boolean", nullable: false),
                    Deleted = table.Column<bool>(type: "boolean", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: true),
                    StoreGroupId = table.Column<Guid>(type: "uuid", nullable: true),
                    StoreId = table.Column<Guid>(type: "uuid", nullable: true),
                    SourceType = table.Column<int>(type: "integer", nullable: false),
                    MappingKey = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    DisplayName = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    AccountId = table.Column<Guid>(type: "uuid", nullable: false),
                    IsRequired = table.Column<bool>(type: "boolean", nullable: false),
                    IsSystem = table.Column<bool>(type: "boolean", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    Notes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Revision = table.Column<int>(type: "integer", nullable: false),
                    CreatedBy = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    UpdatedBy = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_fa_account_mappings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_fa_account_mappings_account",
                        column: x => x.AccountId,
                        principalSchema: "final_accounts",
                        principalTable: "fa_accounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(name: "IX_fa_account_groups_parent", schema: "final_accounts", table: "fa_account_groups", column: "ParentGroupId");
            migrationBuilder.CreateIndex(name: "IX_fa_account_groups_scope_type_active", schema: "final_accounts", table: "fa_account_groups", columns: new[] { "CompanyId", "StoreGroupId", "StoreId", "AccountType", "IsActive" });
            migrationBuilder.CreateIndex(name: "IX_fa_accounts_group_active", schema: "final_accounts", table: "fa_accounts", columns: new[] { "CompanyId", "StoreGroupId", "StoreId", "AccountGroupId", "IsActive" });
            migrationBuilder.CreateIndex(name: "IX_fa_accounts_parent", schema: "final_accounts", table: "fa_accounts", column: "ParentAccountId");
            migrationBuilder.CreateIndex(name: "IX_fa_account_mappings_account_active", schema: "final_accounts", table: "fa_account_mappings", columns: new[] { "AccountId", "IsActive" });
            migrationBuilder.CreateIndex(name: "IX_fa_fiscal_years_scope_dates", schema: "final_accounts", table: "fa_fiscal_years", columns: new[] { "CompanyId", "StoreGroupId", "StoreId", "StartDate", "EndDate" });
            migrationBuilder.CreateIndex(name: "IX_fa_fiscal_periods_year_number", schema: "final_accounts", table: "fa_fiscal_periods", columns: new[] { "FiscalYearId", "PeriodNumber" }, unique: true);
            migrationBuilder.CreateIndex(name: "IX_fa_fiscal_periods_scope_dates", schema: "final_accounts", table: "fa_fiscal_periods", columns: new[] { "CompanyId", "StoreGroupId", "StoreId", "StartDate", "EndDate" });

            migrationBuilder.Sql(UniqueScopeIndexSql("IX_fa_account_groups_scope_code", "fa_account_groups", @"""Code"""));
            migrationBuilder.Sql(UniqueScopeIndexSql("IX_fa_accounts_scope_code", "fa_accounts", @"""Code"""));
            migrationBuilder.Sql(UniqueScopeIndexSql("IX_fa_fiscal_years_scope_name", "fa_fiscal_years", @"""Name"""));
            migrationBuilder.Sql(UniqueScopeIndexSql("IX_fa_account_mappings_scope_key", "fa_account_mappings", @"""SourceType"", ""MappingKey"""));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "fa_account_mappings", schema: "final_accounts");
            migrationBuilder.DropTable(name: "fa_fiscal_periods", schema: "final_accounts");
            migrationBuilder.DropTable(name: "fa_accounts", schema: "final_accounts");
            migrationBuilder.DropTable(name: "fa_fiscal_years", schema: "final_accounts");
            migrationBuilder.DropTable(name: "fa_account_groups", schema: "final_accounts");
        }

        private static string UniqueScopeIndexSql(string indexName, string tableName, string extraColumns)
            => $"""
                CREATE UNIQUE INDEX "{indexName}"
                ON final_accounts.{tableName} (
                    COALESCE("CompanyId", '00000000-0000-0000-0000-000000000000'::uuid),
                    COALESCE("StoreGroupId", '00000000-0000-0000-0000-000000000000'::uuid),
                    COALESCE("StoreId", '00000000-0000-0000-0000-000000000000'::uuid),
                    {extraColumns}
                )
                WHERE "Deleted" = false;
                """;
    }
}
