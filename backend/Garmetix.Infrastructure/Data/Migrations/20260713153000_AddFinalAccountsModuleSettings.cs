using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Garmetix.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddFinalAccountsModuleSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(name: "final_accounts");

            migrationBuilder.CreateTable(
                name: "fa_module_settings",
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
                    Enabled = table.Column<bool>(type: "boolean", nullable: false),
                    PostingMode = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    StatementTemplate = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    InventoryValuationMethod = table.Column<string>(type: "character varying(48)", maxLength: 48, nullable: false),
                    RoundingScale = table.Column<int>(type: "integer", nullable: false),
                    AllowHistoricalBackfill = table.Column<bool>(type: "boolean", nullable: false),
                    AllowTallyExport = table.Column<bool>(type: "boolean", nullable: false),
                    AllowProjections = table.Column<bool>(type: "boolean", nullable: false),
                    AllowPeriodReopen = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedBy = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    UpdatedBy = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_fa_module_settings", x => x.Id);
                });

            migrationBuilder.Sql("""
                CREATE UNIQUE INDEX "IX_fa_module_settings_scope"
                ON final_accounts.fa_module_settings (
                    COALESCE("CompanyId", '00000000-0000-0000-0000-000000000000'::uuid),
                    COALESCE("StoreGroupId", '00000000-0000-0000-0000-000000000000'::uuid),
                    COALESCE("StoreId", '00000000-0000-0000-0000-000000000000'::uuid)
                );
                """);

            migrationBuilder.CreateIndex(
                name: "IX_fa_module_settings_Enabled_UpdatedAt",
                schema: "final_accounts",
                table: "fa_module_settings",
                columns: new[] { "Enabled", "UpdatedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "fa_module_settings",
                schema: "final_accounts");
        }
    }
}
