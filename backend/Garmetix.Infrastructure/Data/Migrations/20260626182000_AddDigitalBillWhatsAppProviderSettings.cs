using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Garmetix.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddDigitalBillWhatsAppProviderSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "WhatsAppProviderSettings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    AutoSendDigitalBills = table.Column<bool>(type: "boolean", nullable: false),
                    Provider = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    ApiBaseUrl = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    ApiToken = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    PhoneNumberId = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    SenderId = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    TemplateName = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    LanguageCode = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    MessageTemplateText = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    SendPdfLink = table.Column<bool>(type: "boolean", nullable: false),
                    FallbackToManualLog = table.Column<bool>(type: "boolean", nullable: false),
                    RetryLimit = table.Column<int>(type: "integer", nullable: false),
                    LastTestAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    LastError = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    Synced = table.Column<bool>(type: "boolean", nullable: false),
                    Deleted = table.Column<bool>(type: "boolean", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    StoreGroupId = table.Column<Guid>(type: "uuid", nullable: false),
                    StoreId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table => table.PrimaryKey("PK_WhatsAppProviderSettings", x => x.Id));

            migrationBuilder.CreateIndex(
                name: "IX_WhatsAppProviderSettings_CompanyId_StoreId",
                table: "WhatsAppProviderSettings",
                columns: new[] { "CompanyId", "StoreId" });

            migrationBuilder.CreateIndex(
                name: "IX_WhatsAppProviderSettings_CompanyId_StoreId_IsEnabled_AutoSendDigitalBills",
                table: "WhatsAppProviderSettings",
                columns: new[] { "CompanyId", "StoreId", "IsEnabled", "AutoSendDigitalBills" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "WhatsAppProviderSettings");
        }
    }
}
