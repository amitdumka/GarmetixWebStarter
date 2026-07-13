using System;
using Garmetix.Infrastructure.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Garmetix.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    [DbContext(typeof(GarmetixDbContext))]
    [Migration("20260714120000_AddFinalAccountsCaWorkspace")]
    public partial class AddFinalAccountsCaWorkspace : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(name: "final_accounts");

            migrationBuilder.CreateTable(
                name: "fa_ca_adjustment_batches",
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
                    BatchNumber = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Title = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    Description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    AdjustmentDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    FiscalPeriodId = table.Column<Guid>(type: "uuid", nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    AutoReverse = table.Column<bool>(type: "boolean", nullable: false),
                    AutoReverseDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    ReferenceNumber = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    JournalEntryId = table.Column<Guid>(type: "uuid", nullable: true),
                    ReversalJournalEntryId = table.Column<Guid>(type: "uuid", nullable: true),
                    SubmittedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    SubmittedBy = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    ReviewedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    ReviewedBy = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    ApprovedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    ApprovedBy = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    RejectedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    RejectedBy = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    PostedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    PostedBy = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    ReversedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    ReversedBy = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    DecisionNotes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    Revision = table.Column<int>(type: "integer", nullable: false),
                    CreatedBy = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    UpdatedBy = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true)
                },
                constraints: table => table.PrimaryKey("PK_fa_ca_adjustment_batches", x => x.Id));

            migrationBuilder.CreateTable(
                name: "fa_report_versions",
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
                    ReportType = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    VersionKind = table.Column<int>(type: "integer", nullable: false),
                    PeriodFrom = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    PeriodTo = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    GeneratedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    GeneratedBy = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    Notes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Revision = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table => table.PrimaryKey("PK_fa_report_versions", x => x.Id));

            migrationBuilder.CreateTable(
                name: "fa_statement_line_comments",
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
                    StatementType = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    StatementLineKey = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    ReportVersion = table.Column<int>(type: "integer", nullable: false),
                    PeriodFrom = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    PeriodTo = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    Body = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    CreatedBy = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    UpdatedBy = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    Revision = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table => table.PrimaryKey("PK_fa_statement_line_comments", x => x.Id));

            migrationBuilder.CreateTable(
                name: "fa_ca_adjustment_attachments",
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
                    AdjustmentBatchId = table.Column<Guid>(type: "uuid", nullable: false),
                    FileName = table.Column<string>(type: "character varying(260)", maxLength: 260, nullable: false),
                    ContentType = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    StorageReference = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Notes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    UploadedBy = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    Revision = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_fa_ca_adjustment_attachments", x => x.Id);
                    table.ForeignKey("FK_fa_ca_adjustment_attachments_batch", x => x.AdjustmentBatchId, "fa_ca_adjustment_batches", "Id", "final_accounts", onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "fa_ca_adjustment_comments",
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
                    AdjustmentBatchId = table.Column<Guid>(type: "uuid", nullable: false),
                    Body = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    Visibility = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    CreatedBy = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    Revision = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_fa_ca_adjustment_comments", x => x.Id);
                    table.ForeignKey("FK_fa_ca_adjustment_comments_batch", x => x.AdjustmentBatchId, "fa_ca_adjustment_batches", "Id", "final_accounts", onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "fa_ca_adjustment_lines",
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
                    AdjustmentBatchId = table.Column<Guid>(type: "uuid", nullable: false),
                    AccountId = table.Column<Guid>(type: "uuid", nullable: false),
                    LineNumber = table.Column<int>(type: "integer", nullable: false),
                    Debit = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Credit = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Narration = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    StatementLineKey = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    Revision = table.Column<int>(type: "integer", nullable: false),
                    CreatedBy = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    UpdatedBy = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_fa_ca_adjustment_lines", x => x.Id);
                    table.CheckConstraint("CK_fa_ca_adjustment_lines_non_negative", "\"Debit\" >= 0 AND \"Credit\" >= 0");
                    table.CheckConstraint("CK_fa_ca_adjustment_lines_single_side", "((\"Debit\" > 0 AND \"Credit\" = 0) OR (\"Credit\" > 0 AND \"Debit\" = 0))");
                    table.ForeignKey("FK_fa_ca_adjustment_lines_account", x => x.AccountId, "fa_accounts", "Id", "final_accounts", onDelete: ReferentialAction.Restrict);
                    table.ForeignKey("FK_fa_ca_adjustment_lines_batch", x => x.AdjustmentBatchId, "fa_ca_adjustment_batches", "Id", "final_accounts", onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex("IX_fa_ca_adjustment_batches_scope_number", "fa_ca_adjustment_batches", new[] { "CompanyId", "StoreGroupId", "StoreId", "BatchNumber" }, "final_accounts", unique: true);
            migrationBuilder.CreateIndex("IX_fa_ca_adjustment_batches_scope_status_date", "fa_ca_adjustment_batches", new[] { "CompanyId", "StoreGroupId", "StoreId", "Status", "AdjustmentDate" }, "final_accounts");
            migrationBuilder.CreateIndex("IX_fa_ca_adjustment_batches_journal", "fa_ca_adjustment_batches", "JournalEntryId", "final_accounts");
            migrationBuilder.CreateIndex("IX_fa_ca_adjustment_batches_reversal_journal", "fa_ca_adjustment_batches", "ReversalJournalEntryId", "final_accounts");
            migrationBuilder.CreateIndex("IX_fa_ca_adjustment_lines_batch_line", "fa_ca_adjustment_lines", new[] { "AdjustmentBatchId", "LineNumber" }, "final_accounts", unique: true);
            migrationBuilder.CreateIndex("IX_fa_ca_adjustment_lines_scope_account", "fa_ca_adjustment_lines", new[] { "CompanyId", "StoreGroupId", "StoreId", "AccountId" }, "final_accounts");
            migrationBuilder.CreateIndex("IX_fa_ca_adjustment_comments_batch_created", "fa_ca_adjustment_comments", new[] { "AdjustmentBatchId", "CreatedAt" }, "final_accounts");
            migrationBuilder.CreateIndex("IX_fa_ca_adjustment_attachments_batch_created", "fa_ca_adjustment_attachments", new[] { "AdjustmentBatchId", "CreatedAt" }, "final_accounts");
            migrationBuilder.CreateIndex("IX_fa_statement_line_comments_scope_line", "fa_statement_line_comments", new[] { "CompanyId", "StoreGroupId", "StoreId", "StatementType", "StatementLineKey", "ReportVersion" }, "final_accounts");
            migrationBuilder.CreateIndex("IX_fa_report_versions_scope_type", "fa_report_versions", new[] { "CompanyId", "StoreGroupId", "StoreId", "ReportType", "VersionKind", "GeneratedAt" }, "final_accounts");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "fa_ca_adjustment_attachments", schema: "final_accounts");
            migrationBuilder.DropTable(name: "fa_ca_adjustment_comments", schema: "final_accounts");
            migrationBuilder.DropTable(name: "fa_ca_adjustment_lines", schema: "final_accounts");
            migrationBuilder.DropTable(name: "fa_report_versions", schema: "final_accounts");
            migrationBuilder.DropTable(name: "fa_statement_line_comments", schema: "final_accounts");
            migrationBuilder.DropTable(name: "fa_ca_adjustment_batches", schema: "final_accounts");
        }
    }
}
