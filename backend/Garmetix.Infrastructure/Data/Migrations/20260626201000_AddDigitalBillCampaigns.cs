using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Garmetix.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddDigitalBillCampaigns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DigitalBillCampaigns",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    StoreGroupId = table.Column<Guid>(type: "uuid", nullable: true),
                    StoreId = table.Column<Guid>(type: "uuid", nullable: true),
                    Name = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    Segment = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    FromDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    ToDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    SearchText = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: true),
                    Channel = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    Status = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    TemplateName = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: true),
                    MessageTitle = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    MessageBody = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    OfferUrl = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Notes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    RecipientCount = table.Column<int>(type: "integer", nullable: false),
                    PreparedCount = table.Column<int>(type: "integer", nullable: false),
                    SentCount = table.Column<int>(type: "integer", nullable: false),
                    FailedCount = table.Column<int>(type: "integer", nullable: false),
                    OpenCountAtCreate = table.Column<int>(type: "integer", nullable: false),
                    ReviewClickCountAtCreate = table.Column<int>(type: "integer", nullable: false),
                    FeedbackCountAtCreate = table.Column<int>(type: "integer", nullable: false),
                    ScheduledAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    QueuedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    CompletedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    CancelledAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    Synced = table.Column<bool>(type: "boolean", nullable: false),
                    Deleted = table.Column<bool>(type: "boolean", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table => table.PrimaryKey("PK_DigitalBillCampaigns", x => x.Id));

            migrationBuilder.CreateTable(
                name: "DigitalBillCampaignRecipients",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CampaignId = table.Column<Guid>(type: "uuid", nullable: false),
                    DigitalInvoiceId = table.Column<Guid>(type: "uuid", nullable: false),
                    AudienceKey = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    CustomerName = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    CustomerMobile = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    InvoiceNumber = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    PublicPath = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    LastInvoiceAmount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    Status = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    MessageBody = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    ErrorMessage = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    QueuedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    SentAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    FailedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    Synced = table.Column<bool>(type: "boolean", nullable: false),
                    Deleted = table.Column<bool>(type: "boolean", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    StoreGroupId = table.Column<Guid>(type: "uuid", nullable: false),
                    StoreId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table => table.PrimaryKey("PK_DigitalBillCampaignRecipients", x => x.Id));

            migrationBuilder.CreateIndex(
                name: "IX_DigitalBillCampaigns_CompanyId_StoreId_Status_CreatedAt",
                table: "DigitalBillCampaigns",
                columns: new[] { "CompanyId", "StoreId", "Status", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_DigitalBillCampaigns_CompanyId_Segment_FromDate_ToDate",
                table: "DigitalBillCampaigns",
                columns: new[] { "CompanyId", "Segment", "FromDate", "ToDate" });

            migrationBuilder.CreateIndex(
                name: "IX_DigitalBillCampaignRecipients_CompanyId_CampaignId_Status",
                table: "DigitalBillCampaignRecipients",
                columns: new[] { "CompanyId", "CampaignId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_DigitalBillCampaignRecipients_CompanyId_CustomerMobile_CreatedAt",
                table: "DigitalBillCampaignRecipients",
                columns: new[] { "CompanyId", "CustomerMobile", "CreatedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "DigitalBillCampaignRecipients");
            migrationBuilder.DropTable(name: "DigitalBillCampaigns");
        }
    }
}
