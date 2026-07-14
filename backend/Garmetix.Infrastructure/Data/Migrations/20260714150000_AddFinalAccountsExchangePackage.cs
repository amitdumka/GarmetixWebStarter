using System;
using Garmetix.Infrastructure.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Garmetix.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    [DbContext(typeof(GarmetixDbContext))]
    [Migration("20260714150000_AddFinalAccountsExchangePackage")]
    public partial class AddFinalAccountsExchangePackage : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(name: "final_accounts");

            migrationBuilder.CreateTable(
                name: "fa_tally_profiles",
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
                    ProfileCode = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Name = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    TallyRelease = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    TestCompanyName = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    BaseCurrency = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    Country = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    GstRegistrationType = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    DuplicatePolicy = table.Column<int>(type: "integer", nullable: false),
                    DirectPostingAllowed = table.Column<bool>(type: "boolean", nullable: false),
                    GroupMappingJson = table.Column<string>(type: "text", nullable: false),
                    LedgerMappingJson = table.Column<string>(type: "text", nullable: false),
                    VoucherTypeMappingJson = table.Column<string>(type: "text", nullable: false),
                    TaxMappingJson = table.Column<string>(type: "text", nullable: false),
                    StockCostCentreMappingJson = table.Column<string>(type: "text", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    Notes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    Revision = table.Column<int>(type: "integer", nullable: false),
                    CreatedBy = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    UpdatedBy = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true)
                },
                constraints: table => table.PrimaryKey("PK_fa_tally_profiles", x => x.Id));

            migrationBuilder.CreateTable(
                name: "fa_exchange_runs",
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
                    RunNumber = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    RunKind = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    TallyProfileId = table.Column<Guid>(type: "uuid", nullable: true),
                    PeriodFrom = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    PeriodTo = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    AsOf = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    Format = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false),
                    MasterCount = table.Column<int>(type: "integer", nullable: false),
                    VoucherCount = table.Column<int>(type: "integer", nullable: false),
                    ExceptionCount = table.Column<int>(type: "integer", nullable: false),
                    ControlDebit = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    ControlCredit = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    PayloadHash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    ZipChecksum = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    FileName = table.Column<string>(type: "character varying(260)", maxLength: 260, nullable: true),
                    GeneratedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    GeneratedBy = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    Notes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    Revision = table.Column<int>(type: "integer", nullable: false),
                    CreatedBy = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    UpdatedBy = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true)
                },
                constraints: table => table.PrimaryKey("PK_fa_exchange_runs", x => x.Id));

            migrationBuilder.CreateTable(
                name: "fa_exchange_exceptions",
                schema: "final_accounts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    Synced = table.Column<bool>(type: "boolean", nullable: false),
                    Deleted = table.Column<bool>(type: "boolean", nullable: false),
                    ExchangeRunId = table.Column<Guid>(type: "uuid", nullable: false),
                    Severity = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false),
                    Code = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    Message = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    SourceType = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    SourceId = table.Column<Guid>(type: "uuid", nullable: true),
                    Resolved = table.Column<bool>(type: "boolean", nullable: false),
                    Revision = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table => table.PrimaryKey("PK_fa_exchange_exceptions", x => x.Id));

            migrationBuilder.CreateIndex(name: "IX_fa_tally_profiles_scope_code", schema: "final_accounts", table: "fa_tally_profiles", columns: new[] { "CompanyId", "StoreGroupId", "StoreId", "ProfileCode" }, unique: true);
            migrationBuilder.CreateIndex(name: "IX_fa_tally_profiles_scope_active", schema: "final_accounts", table: "fa_tally_profiles", columns: new[] { "CompanyId", "StoreGroupId", "StoreId", "IsActive" });
            migrationBuilder.CreateIndex(name: "IX_fa_exchange_runs_scope_number", schema: "final_accounts", table: "fa_exchange_runs", columns: new[] { "CompanyId", "StoreGroupId", "StoreId", "RunNumber" }, unique: true);
            migrationBuilder.CreateIndex(name: "IX_fa_exchange_runs_scope_kind_created", schema: "final_accounts", table: "fa_exchange_runs", columns: new[] { "CompanyId", "StoreGroupId", "StoreId", "RunKind", "CreatedAt" });
            migrationBuilder.CreateIndex(name: "IX_fa_exchange_runs_tally_profile", schema: "final_accounts", table: "fa_exchange_runs", column: "TallyProfileId");
            migrationBuilder.CreateIndex(name: "IX_fa_exchange_exceptions_run_code", schema: "final_accounts", table: "fa_exchange_exceptions", columns: new[] { "ExchangeRunId", "Code" });
            migrationBuilder.CreateIndex(name: "IX_fa_exchange_exceptions_source_resolved", schema: "final_accounts", table: "fa_exchange_exceptions", columns: new[] { "SourceType", "SourceId", "Resolved" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "fa_exchange_exceptions", schema: "final_accounts");
            migrationBuilder.DropTable(name: "fa_exchange_runs", schema: "final_accounts");
            migrationBuilder.DropTable(name: "fa_tally_profiles", schema: "final_accounts");
        }
    }
}
