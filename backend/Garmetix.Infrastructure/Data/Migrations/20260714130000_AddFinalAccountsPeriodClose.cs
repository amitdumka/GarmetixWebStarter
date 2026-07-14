using System;
using Garmetix.Infrastructure.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Garmetix.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    [DbContext(typeof(GarmetixDbContext))]
    [Migration("20260714130000_AddFinalAccountsPeriodClose")]
    public partial class AddFinalAccountsPeriodClose : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(name: "final_accounts");

            migrationBuilder.CreateTable(
                name: "fa_close_runs",
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
                    CloseType = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    FiscalYearId = table.Column<Guid>(type: "uuid", nullable: false),
                    FiscalPeriodId = table.Column<Guid>(type: "uuid", nullable: true),
                    PeriodStart = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    PeriodEnd = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    CloseDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    ChecklistStatus = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    ReconciliationStatus = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    PendingPostingCount = table.Column<int>(type: "integer", nullable: false),
                    TrialBalanceStatus = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    BalanceSheetStatus = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    InventorySnapshotTotal = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    ProfitAfterTax = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    CurrentYearResultTransferStatus = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    OpeningJournalStatus = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    ReportSnapshotStatus = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    FinancialYearLockId = table.Column<Guid>(type: "uuid", nullable: true),
                    ClosedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    ClosedBy = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    ReopenRequestedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    ReopenRequestedBy = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    ReopenedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    ReopenedBy = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    ReopenReason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    ApprovalNotes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    Revision = table.Column<int>(type: "integer", nullable: false),
                    CreatedBy = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    UpdatedBy = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true)
                },
                constraints: table => table.PrimaryKey("PK_fa_close_runs", x => x.Id));

            migrationBuilder.CreateTable(
                name: "fa_close_balance_snapshots",
                schema: "final_accounts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    Synced = table.Column<bool>(type: "boolean", nullable: false),
                    Deleted = table.Column<bool>(type: "boolean", nullable: false),
                    CloseRunId = table.Column<Guid>(type: "uuid", nullable: false),
                    AccountId = table.Column<Guid>(type: "uuid", nullable: false),
                    AccountCode = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    AccountName = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    AccountType = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    ClosingBalance = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    OpeningBalance = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Revision = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table => table.PrimaryKey("PK_fa_close_balance_snapshots", x => x.Id));

            migrationBuilder.CreateTable(
                name: "fa_close_checklist_items",
                schema: "final_accounts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    Synced = table.Column<bool>(type: "boolean", nullable: false),
                    Deleted = table.Column<bool>(type: "boolean", nullable: false),
                    CloseRunId = table.Column<Guid>(type: "uuid", nullable: false),
                    Key = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    Label = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    Required = table.Column<bool>(type: "boolean", nullable: false),
                    Status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Detail = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    Revision = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table => table.PrimaryKey("PK_fa_close_checklist_items", x => x.Id));

            migrationBuilder.CreateTable(
                name: "fa_close_report_snapshots",
                schema: "final_accounts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    Synced = table.Column<bool>(type: "boolean", nullable: false),
                    Deleted = table.Column<bool>(type: "boolean", nullable: false),
                    CloseRunId = table.Column<Guid>(type: "uuid", nullable: false),
                    ReportType = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    ReportVersion = table.Column<int>(type: "integer", nullable: false),
                    PeriodFrom = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    PeriodTo = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    Status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    PayloadJson = table.Column<string>(type: "text", nullable: false),
                    PayloadHash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    Revision = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table => table.PrimaryKey("PK_fa_close_report_snapshots", x => x.Id));

            migrationBuilder.CreateIndex("IX_fa_close_runs_scope_number", "fa_close_runs", new[] { "CompanyId", "StoreGroupId", "StoreId", "RunNumber" }, "final_accounts", unique: true);
            migrationBuilder.CreateIndex("IX_fa_close_runs_scope_period_status", "fa_close_runs", new[] { "CompanyId", "StoreGroupId", "StoreId", "FiscalYearId", "FiscalPeriodId", "CloseType", "Status" }, "final_accounts");
            migrationBuilder.CreateIndex("IX_fa_close_runs_financial_year_lock", "fa_close_runs", "FinancialYearLockId", "final_accounts");
            migrationBuilder.CreateIndex("IX_fa_close_checklist_items_run_key", "fa_close_checklist_items", new[] { "CloseRunId", "Key" }, "final_accounts", unique: true);
            migrationBuilder.CreateIndex("IX_fa_close_report_snapshots_run_type", "fa_close_report_snapshots", new[] { "CloseRunId", "ReportType" }, "final_accounts", unique: true);
            migrationBuilder.CreateIndex("IX_fa_close_balance_snapshots_run_account", "fa_close_balance_snapshots", new[] { "CloseRunId", "AccountId" }, "final_accounts", unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "fa_close_balance_snapshots", schema: "final_accounts");
            migrationBuilder.DropTable(name: "fa_close_checklist_items", schema: "final_accounts");
            migrationBuilder.DropTable(name: "fa_close_report_snapshots", schema: "final_accounts");
            migrationBuilder.DropTable(name: "fa_close_runs", schema: "final_accounts");
        }
    }
}
