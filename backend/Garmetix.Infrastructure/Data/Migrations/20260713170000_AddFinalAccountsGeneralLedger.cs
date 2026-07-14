using System;
using Garmetix.Infrastructure.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Garmetix.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    [DbContext(typeof(GarmetixDbContext))]
    [Migration("20260713170000_AddFinalAccountsGeneralLedger")]
    public partial class AddFinalAccountsGeneralLedger : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(name: "final_accounts");

            migrationBuilder.CreateTable(
                name: "fa_journal_entries",
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
                    EntryNumber = table.Column<string>(type: "character varying(48)", maxLength: 48, nullable: false),
                    OnDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    FiscalPeriodId = table.Column<Guid>(type: "uuid", nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    SourceType = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    SourceId = table.Column<Guid>(type: "uuid", nullable: true),
                    ReferenceNumber = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    Narration = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    IdempotencyKey = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: true),
                    ReversalOfJournalEntryId = table.Column<Guid>(type: "uuid", nullable: true),
                    ReversalJournalEntryId = table.Column<Guid>(type: "uuid", nullable: true),
                    PostedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    PostedBy = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    ReversedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    ReversedBy = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    Revision = table.Column<int>(type: "integer", nullable: false),
                    CreatedBy = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    UpdatedBy = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_fa_journal_entries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_fa_journal_entries_fiscal_period",
                        column: x => x.FiscalPeriodId,
                        principalSchema: "final_accounts",
                        principalTable: "fa_fiscal_periods",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_fa_journal_entries_reversal_of",
                        column: x => x.ReversalOfJournalEntryId,
                        principalSchema: "final_accounts",
                        principalTable: "fa_journal_entries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_fa_journal_entries_reversal_journal",
                        column: x => x.ReversalJournalEntryId,
                        principalSchema: "final_accounts",
                        principalTable: "fa_journal_entries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "fa_journal_lines",
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
                    JournalEntryId = table.Column<Guid>(type: "uuid", nullable: false),
                    AccountId = table.Column<Guid>(type: "uuid", nullable: false),
                    LineNumber = table.Column<int>(type: "integer", nullable: false),
                    Debit = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Credit = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Narration = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Revision = table.Column<int>(type: "integer", nullable: false),
                    CreatedBy = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    UpdatedBy = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_fa_journal_lines", x => x.Id);
                    table.CheckConstraint("CK_fa_journal_lines_debit_credit_non_negative", "\"Debit\" >= 0 AND \"Credit\" >= 0");
                    table.CheckConstraint("CK_fa_journal_lines_single_side", "((\"Debit\" > 0 AND \"Credit\" = 0) OR (\"Credit\" > 0 AND \"Debit\" = 0))");
                    table.ForeignKey(
                        name: "FK_fa_journal_lines_account",
                        column: x => x.AccountId,
                        principalSchema: "final_accounts",
                        principalTable: "fa_accounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_fa_journal_lines_entry",
                        column: x => x.JournalEntryId,
                        principalSchema: "final_accounts",
                        principalTable: "fa_journal_entries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "fa_source_posting_links",
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
                    JournalEntryId = table.Column<Guid>(type: "uuid", nullable: false),
                    SourceType = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    SourceId = table.Column<Guid>(type: "uuid", nullable: false),
                    SourceReference = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    SourceHash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    MappingVersion = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    IdempotencyKey = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: true),
                    PostedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    Revision = table.Column<int>(type: "integer", nullable: false),
                    CreatedBy = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    UpdatedBy = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_fa_source_posting_links", x => x.Id);
                    table.ForeignKey(
                        name: "FK_fa_source_posting_links_entry",
                        column: x => x.JournalEntryId,
                        principalSchema: "final_accounts",
                        principalTable: "fa_journal_entries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(name: "IX_fa_journal_entries_scope_date_status", schema: "final_accounts", table: "fa_journal_entries", columns: new[] { "CompanyId", "StoreGroupId", "StoreId", "OnDate", "Status" });
            migrationBuilder.CreateIndex(name: "IX_fa_journal_entries_source", schema: "final_accounts", table: "fa_journal_entries", columns: new[] { "CompanyId", "StoreGroupId", "StoreId", "SourceType", "SourceId" });
            migrationBuilder.CreateIndex(name: "IX_fa_journal_entries_fiscal_period", schema: "final_accounts", table: "fa_journal_entries", column: "FiscalPeriodId");
            migrationBuilder.CreateIndex(name: "IX_fa_journal_entries_reversal_of", schema: "final_accounts", table: "fa_journal_entries", column: "ReversalOfJournalEntryId");
            migrationBuilder.CreateIndex(name: "IX_fa_journal_entries_reversal_journal", schema: "final_accounts", table: "fa_journal_entries", column: "ReversalJournalEntryId");
            migrationBuilder.CreateIndex(name: "IX_fa_journal_lines_journal_line_number", schema: "final_accounts", table: "fa_journal_lines", columns: new[] { "JournalEntryId", "LineNumber" }, unique: true);
            migrationBuilder.CreateIndex(name: "IX_fa_journal_lines_account_journal", schema: "final_accounts", table: "fa_journal_lines", columns: new[] { "CompanyId", "StoreGroupId", "StoreId", "AccountId", "JournalEntryId" });
            migrationBuilder.CreateIndex(name: "IX_fa_source_posting_links_journal", schema: "final_accounts", table: "fa_source_posting_links", column: "JournalEntryId");

            migrationBuilder.Sql(UniqueScopeIndexSql("IX_fa_journal_entries_scope_number", "fa_journal_entries", @"""EntryNumber"""));
            migrationBuilder.Sql(UniqueScopeIndexSql("IX_fa_journal_entries_scope_idempotency", "fa_journal_entries", @"""IdempotencyKey""", @"""Deleted"" = false AND ""IdempotencyKey"" IS NOT NULL"));
            migrationBuilder.Sql(UniqueScopeIndexSql("IX_fa_source_posting_links_scope_source", "fa_source_posting_links", @"""SourceType"", ""SourceId"""));
            migrationBuilder.Sql(UniqueScopeIndexSql("IX_fa_source_posting_links_scope_idempotency", "fa_source_posting_links", @"""IdempotencyKey""", @"""Deleted"" = false AND ""IdempotencyKey"" IS NOT NULL"));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "fa_source_posting_links", schema: "final_accounts");
            migrationBuilder.DropTable(name: "fa_journal_lines", schema: "final_accounts");
            migrationBuilder.DropTable(name: "fa_journal_entries", schema: "final_accounts");
        }

        private static string UniqueScopeIndexSql(string indexName, string tableName, string extraColumns, string filter = @"""Deleted"" = false")
            => $"""
                CREATE UNIQUE INDEX "{indexName}"
                ON final_accounts.{tableName} (
                    COALESCE("CompanyId", '00000000-0000-0000-0000-000000000000'::uuid),
                    COALESCE("StoreGroupId", '00000000-0000-0000-0000-000000000000'::uuid),
                    COALESCE("StoreId", '00000000-0000-0000-0000-000000000000'::uuid),
                    {extraColumns}
                )
                WHERE {filter};
                """;
    }
}
