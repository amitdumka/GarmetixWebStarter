using System;
using Garmetix.Infrastructure.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Garmetix.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    [DbContext(typeof(GarmetixDbContext))]
    [Migration("20260713173000_AddFinalAccountsPostingRules")]
    public partial class AddFinalAccountsPostingRules : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(name: "final_accounts");

            migrationBuilder.CreateTable(
                name: "fa_posting_rules",
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
                    SourceType = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    RuleCode = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    Version = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    Name = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    EffectiveFrom = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    IsSystem = table.Column<bool>(type: "boolean", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    Revision = table.Column<int>(type: "integer", nullable: false),
                    CreatedBy = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    UpdatedBy = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_fa_posting_rules", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "fa_posting_rule_lines",
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
                    PostingRuleId = table.Column<Guid>(type: "uuid", nullable: false),
                    MappingKey = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    DisplayName = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    MappingCategory = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    Direction = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    ExpectedAccountType = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    IsRequired = table.Column<bool>(type: "boolean", nullable: false),
                    AllowControlAccount = table.Column<bool>(type: "boolean", nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    Notes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Revision = table.Column<int>(type: "integer", nullable: false),
                    CreatedBy = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    UpdatedBy = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_fa_posting_rule_lines", x => x.Id);
                    table.ForeignKey(
                        name: "FK_fa_posting_rule_lines_rule",
                        column: x => x.PostingRuleId,
                        principalSchema: "final_accounts",
                        principalTable: "fa_posting_rules",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(name: "IX_fa_posting_rules_scope_source_active", schema: "final_accounts", table: "fa_posting_rules", columns: new[] { "CompanyId", "StoreGroupId", "StoreId", "SourceType", "IsActive" });
            migrationBuilder.CreateIndex(name: "IX_fa_posting_rule_lines_rule_key", schema: "final_accounts", table: "fa_posting_rule_lines", columns: new[] { "PostingRuleId", "MappingKey" }, unique: true);
            migrationBuilder.CreateIndex(name: "IX_fa_posting_rule_lines_category_required", schema: "final_accounts", table: "fa_posting_rule_lines", columns: new[] { "CompanyId", "StoreGroupId", "StoreId", "MappingCategory", "IsRequired" });

            migrationBuilder.Sql(UniqueScopeIndexSql("IX_fa_posting_rules_scope_code_version", "fa_posting_rules", @"""SourceType"", ""RuleCode"", ""Version"""));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "fa_posting_rule_lines", schema: "final_accounts");
            migrationBuilder.DropTable(name: "fa_posting_rules", schema: "final_accounts");
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
