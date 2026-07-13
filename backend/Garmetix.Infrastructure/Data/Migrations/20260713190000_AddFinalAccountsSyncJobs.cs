using System;
using Garmetix.Infrastructure.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Garmetix.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    [DbContext(typeof(GarmetixDbContext))]
    [Migration("20260713190000_AddFinalAccountsSyncJobs")]
    public partial class AddFinalAccountsSyncJobs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(name: "final_accounts");

            migrationBuilder.CreateTable(
                name: "fa_sync_jobs",
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
                    JobNumber = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    Mode = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    DryRun = table.Column<bool>(type: "boolean", nullable: false),
                    Scheduled = table.Column<bool>(type: "boolean", nullable: false),
                    From = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    To = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    ModulesCsv = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    IdempotencyKey = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: true),
                    StopOnError = table.Column<bool>(type: "boolean", nullable: false),
                    MaxAttempts = table.Column<int>(type: "integer", nullable: false),
                    RetryDelaySeconds = table.Column<int>(type: "integer", nullable: false),
                    SourceCount = table.Column<int>(type: "integer", nullable: false),
                    QueuedCount = table.Column<int>(type: "integer", nullable: false),
                    SkippedCount = table.Column<int>(type: "integer", nullable: false),
                    FailedCount = table.Column<int>(type: "integer", nullable: false),
                    DriftCount = table.Column<int>(type: "integer", nullable: false),
                    StartedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    CompletedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    LastCheckpoint = table.Column<string>(type: "character varying(240)", maxLength: 240, nullable: true),
                    ErrorPolicy = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    CreatedBy = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    UpdatedBy = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    Revision = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table => table.PrimaryKey("PK_fa_sync_jobs", x => x.Id));

            migrationBuilder.CreateTable(
                name: "fa_sync_checkpoints",
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
                    Module = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    CheckpointKey = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    LastSourceDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    LastSourceId = table.Column<Guid>(type: "uuid", nullable: true),
                    LastSourceHash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    LastJobId = table.Column<Guid>(type: "uuid", nullable: true),
                    ProcessedCount = table.Column<int>(type: "integer", nullable: false),
                    FailedCount = table.Column<int>(type: "integer", nullable: false),
                    UpdatedBy = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    Revision = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table => table.PrimaryKey("PK_fa_sync_checkpoints", x => x.Id));

            migrationBuilder.CreateTable(
                name: "fa_sync_job_items",
                schema: "final_accounts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    Synced = table.Column<bool>(type: "boolean", nullable: false),
                    Deleted = table.Column<bool>(type: "boolean", nullable: false),
                    JobId = table.Column<Guid>(type: "uuid", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: true),
                    StoreGroupId = table.Column<Guid>(type: "uuid", nullable: true),
                    StoreId = table.Column<Guid>(type: "uuid", nullable: true),
                    SourceType = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    SourceId = table.Column<Guid>(type: "uuid", nullable: false),
                    SourceReference = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    SourceDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    SourceAmount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    SourceHash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    AttemptCount = table.Column<int>(type: "integer", nullable: false),
                    NextAttemptAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    JournalEntryId = table.Column<Guid>(type: "uuid", nullable: true),
                    ErrorCode = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    ErrorMessage = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    Revision = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table => table.PrimaryKey("PK_fa_sync_job_items", x => x.Id));

            migrationBuilder.CreateTable(
                name: "fa_sync_exceptions",
                schema: "final_accounts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    Synced = table.Column<bool>(type: "boolean", nullable: false),
                    Deleted = table.Column<bool>(type: "boolean", nullable: false),
                    JobId = table.Column<Guid>(type: "uuid", nullable: true),
                    JobItemId = table.Column<Guid>(type: "uuid", nullable: true),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: true),
                    StoreGroupId = table.Column<Guid>(type: "uuid", nullable: true),
                    StoreId = table.Column<Guid>(type: "uuid", nullable: true),
                    SourceType = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    SourceId = table.Column<Guid>(type: "uuid", nullable: true),
                    Severity = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false),
                    Category = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    Code = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    Message = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    DetailsJson = table.Column<string>(type: "text", nullable: true),
                    Resolved = table.Column<bool>(type: "boolean", nullable: false),
                    ResolvedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    ResolvedBy = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    ResolutionNotes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Revision = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table => table.PrimaryKey("PK_fa_sync_exceptions", x => x.Id));

            migrationBuilder.CreateIndex(name: "IX_fa_sync_jobs_scope_status_created", schema: "final_accounts", table: "fa_sync_jobs", columns: new[] { "CompanyId", "StoreGroupId", "StoreId", "Status", "CreatedAt" });
            migrationBuilder.CreateIndex(name: "IX_fa_sync_jobs_scope_dryrun_scheduled", schema: "final_accounts", table: "fa_sync_jobs", columns: new[] { "CompanyId", "StoreGroupId", "StoreId", "DryRun", "Scheduled" });
            migrationBuilder.CreateIndex(name: "IX_fa_sync_jobs_scope_job_number", schema: "final_accounts", table: "fa_sync_jobs", columns: new[] { "CompanyId", "StoreGroupId", "StoreId", "JobNumber" }, unique: true);
            migrationBuilder.CreateIndex(name: "IX_fa_sync_jobs_scope_idempotency", schema: "final_accounts", table: "fa_sync_jobs", columns: new[] { "CompanyId", "StoreGroupId", "StoreId", "IdempotencyKey" }, unique: true);

            migrationBuilder.CreateIndex(name: "IX_fa_sync_job_items_job_source", schema: "final_accounts", table: "fa_sync_job_items", columns: new[] { "JobId", "SourceType", "SourceId" }, unique: true);
            migrationBuilder.CreateIndex(name: "IX_fa_sync_job_items_scope_source", schema: "final_accounts", table: "fa_sync_job_items", columns: new[] { "CompanyId", "StoreGroupId", "StoreId", "SourceType", "SourceId" });
            migrationBuilder.CreateIndex(name: "IX_fa_sync_job_items_job_status_next", schema: "final_accounts", table: "fa_sync_job_items", columns: new[] { "JobId", "Status", "NextAttemptAt" });

            migrationBuilder.CreateIndex(name: "IX_fa_sync_checkpoints_scope_module_key", schema: "final_accounts", table: "fa_sync_checkpoints", columns: new[] { "CompanyId", "StoreGroupId", "StoreId", "Module", "CheckpointKey" }, unique: true);
            migrationBuilder.CreateIndex(name: "IX_fa_sync_checkpoints_scope_module_updated", schema: "final_accounts", table: "fa_sync_checkpoints", columns: new[] { "CompanyId", "StoreGroupId", "StoreId", "Module", "UpdatedAt" });

            migrationBuilder.CreateIndex(name: "IX_fa_sync_exceptions_scope_resolved_created", schema: "final_accounts", table: "fa_sync_exceptions", columns: new[] { "CompanyId", "StoreGroupId", "StoreId", "Resolved", "CreatedAt" });
            migrationBuilder.CreateIndex(name: "IX_fa_sync_exceptions_job_item", schema: "final_accounts", table: "fa_sync_exceptions", columns: new[] { "JobId", "JobItemId" });
            migrationBuilder.CreateIndex(name: "IX_fa_sync_exceptions_source_resolved", schema: "final_accounts", table: "fa_sync_exceptions", columns: new[] { "SourceType", "SourceId", "Resolved" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "fa_sync_exceptions", schema: "final_accounts");
            migrationBuilder.DropTable(name: "fa_sync_job_items", schema: "final_accounts");
            migrationBuilder.DropTable(name: "fa_sync_checkpoints", schema: "final_accounts");
            migrationBuilder.DropTable(name: "fa_sync_jobs", schema: "final_accounts");
        }
    }
}
