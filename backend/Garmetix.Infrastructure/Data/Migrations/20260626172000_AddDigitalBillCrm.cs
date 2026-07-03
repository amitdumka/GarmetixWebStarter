using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Garmetix.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddDigitalBillCrm : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DigitalInvoices",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    InvoiceId = table.Column<Guid>(type: "uuid", nullable: false),
                    InvoiceType = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    InvoiceNumber = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    InvoiceDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    CustomerName = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    CustomerMobile = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    PublicToken = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    PublicUrl = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    PdfPath = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    HtmlSnapshotJson = table.Column<string>(type: "text", nullable: true),
                    ExpiresAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    DisabledAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    DisabledBy = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    DisableReason = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    LastOpenedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    OpenCount = table.Column<int>(type: "integer", nullable: false),
                    PdfDownloadCount = table.Column<int>(type: "integer", nullable: false),
                    ReviewClickCount = table.Column<int>(type: "integer", nullable: false),
                    FeedbackCount = table.Column<int>(type: "integer", nullable: false),
                    WhatsAppStatus = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    LastWhatsAppSentAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    Synced = table.Column<bool>(type: "boolean", nullable: false),
                    Deleted = table.Column<bool>(type: "boolean", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    StoreGroupId = table.Column<Guid>(type: "uuid", nullable: false),
                    StoreId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table => table.PrimaryKey("PK_DigitalInvoices", x => x.Id));

            migrationBuilder.CreateTable(
                name: "DigitalInvoiceEvents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    DigitalInvoiceId = table.Column<Guid>(type: "uuid", nullable: false),
                    EventType = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    Source = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    IpAddress = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    UserAgent = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    TargetUrl = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    DetailsJson = table.Column<string>(type: "text", nullable: true),
                    EventAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    Synced = table.Column<bool>(type: "boolean", nullable: false),
                    Deleted = table.Column<bool>(type: "boolean", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    StoreGroupId = table.Column<Guid>(type: "uuid", nullable: false),
                    StoreId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table => table.PrimaryKey("PK_DigitalInvoiceEvents", x => x.Id));

            migrationBuilder.CreateTable(
                name: "StoreReviewSettings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    GoogleReviewUrl = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    InstagramUrl = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    FacebookUrl = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    WhatsAppSupportNumber = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    EnableGoogleReview = table.Column<bool>(type: "boolean", nullable: false),
                    EnableInstagram = table.Column<bool>(type: "boolean", nullable: false),
                    EnableFacebook = table.Column<bool>(type: "boolean", nullable: false),
                    EnableWhatsappSupport = table.Column<bool>(type: "boolean", nullable: false),
                    EnablePrivateFeedback = table.Column<bool>(type: "boolean", nullable: false),
                    ReviewButtonText = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: true),
                    FeedbackButtonText = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    Synced = table.Column<bool>(type: "boolean", nullable: false),
                    Deleted = table.Column<bool>(type: "boolean", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    StoreGroupId = table.Column<Guid>(type: "uuid", nullable: false),
                    StoreId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table => table.PrimaryKey("PK_StoreReviewSettings", x => x.Id));

            migrationBuilder.CreateTable(
                name: "CustomerFeedback",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    DigitalInvoiceId = table.Column<Guid>(type: "uuid", nullable: false),
                    CustomerId = table.Column<Guid>(type: "uuid", nullable: true),
                    CustomerName = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    CustomerMobile = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    Rating = table.Column<int>(type: "integer", nullable: false),
                    Message = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    Source = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    SubmittedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    Synced = table.Column<bool>(type: "boolean", nullable: false),
                    Deleted = table.Column<bool>(type: "boolean", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    StoreGroupId = table.Column<Guid>(type: "uuid", nullable: false),
                    StoreId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table => table.PrimaryKey("PK_CustomerFeedback", x => x.Id));

            migrationBuilder.CreateTable(
                name: "WhatsAppMessageLogs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    DigitalInvoiceId = table.Column<Guid>(type: "uuid", nullable: true),
                    CustomerMobile = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    Provider = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    TemplateName = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    MessageBody = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    Status = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    ProviderMessageId = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: true),
                    ErrorMessage = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    RetryCount = table.Column<int>(type: "integer", nullable: false),
                    SentAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    DeliveredAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    ReadAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    Synced = table.Column<bool>(type: "boolean", nullable: false),
                    Deleted = table.Column<bool>(type: "boolean", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    StoreGroupId = table.Column<Guid>(type: "uuid", nullable: false),
                    StoreId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table => table.PrimaryKey("PK_WhatsAppMessageLogs", x => x.Id));

            migrationBuilder.CreateTable(
                name: "InvoiceAdBanners",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    StoreGroupId = table.Column<Guid>(type: "uuid", nullable: true),
                    StoreId = table.Column<Guid>(type: "uuid", nullable: true),
                    Title = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    ImageUrl = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    TargetUrl = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Position = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    StartDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    EndDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    Priority = table.Column<int>(type: "integer", nullable: false),
                    ClickCount = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    Synced = table.Column<bool>(type: "boolean", nullable: false),
                    Deleted = table.Column<bool>(type: "boolean", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table => table.PrimaryKey("PK_InvoiceAdBanners", x => x.Id));

            migrationBuilder.CreateIndex(name: "IX_DigitalInvoices_PublicToken", table: "DigitalInvoices", column: "PublicToken", unique: true);
            migrationBuilder.CreateIndex(name: "IX_DigitalInvoices_CompanyId_StoreId_InvoiceDate", table: "DigitalInvoices", columns: new[] { "CompanyId", "StoreId", "InvoiceDate" });
            migrationBuilder.CreateIndex(name: "IX_DigitalInvoices_CompanyId_InvoiceId_InvoiceType", table: "DigitalInvoices", columns: new[] { "CompanyId", "InvoiceId", "InvoiceType" });
            migrationBuilder.CreateIndex(name: "IX_DigitalInvoiceEvents_CompanyId_DigitalInvoiceId_EventAt", table: "DigitalInvoiceEvents", columns: new[] { "CompanyId", "DigitalInvoiceId", "EventAt" });
            migrationBuilder.CreateIndex(name: "IX_DigitalInvoiceEvents_CompanyId_StoreId_EventType_EventAt", table: "DigitalInvoiceEvents", columns: new[] { "CompanyId", "StoreId", "EventType", "EventAt" });
            migrationBuilder.CreateIndex(name: "IX_StoreReviewSettings_CompanyId_StoreId", table: "StoreReviewSettings", columns: new[] { "CompanyId", "StoreId" });
            migrationBuilder.CreateIndex(name: "IX_CustomerFeedback_CompanyId_StoreId_SubmittedAt", table: "CustomerFeedback", columns: new[] { "CompanyId", "StoreId", "SubmittedAt" });
            migrationBuilder.CreateIndex(name: "IX_CustomerFeedback_CompanyId_DigitalInvoiceId", table: "CustomerFeedback", columns: new[] { "CompanyId", "DigitalInvoiceId" });
            migrationBuilder.CreateIndex(name: "IX_WhatsAppMessageLogs_CompanyId_StoreId_Status_CreatedAt", table: "WhatsAppMessageLogs", columns: new[] { "CompanyId", "StoreId", "Status", "CreatedAt" });
            migrationBuilder.CreateIndex(name: "IX_WhatsAppMessageLogs_CompanyId_DigitalInvoiceId", table: "WhatsAppMessageLogs", columns: new[] { "CompanyId", "DigitalInvoiceId" });
            migrationBuilder.CreateIndex(name: "IX_InvoiceAdBanners_CompanyId_StoreId_Position_IsActive", table: "InvoiceAdBanners", columns: new[] { "CompanyId", "StoreId", "Position", "IsActive" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "CustomerFeedback");
            migrationBuilder.DropTable(name: "DigitalInvoiceEvents");
            migrationBuilder.DropTable(name: "DigitalInvoices");
            migrationBuilder.DropTable(name: "InvoiceAdBanners");
            migrationBuilder.DropTable(name: "StoreReviewSettings");
            migrationBuilder.DropTable(name: "WhatsAppMessageLogs");
        }
    }
}
