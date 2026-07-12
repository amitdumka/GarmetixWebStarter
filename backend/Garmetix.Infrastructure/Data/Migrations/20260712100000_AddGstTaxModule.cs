using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Garmetix.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddGstTaxModule : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "GstApiProviders",
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
                    ProviderName = table.Column<string>(type: "text", nullable: false),
                    ProviderType = table.Column<string>(type: "text", nullable: false),
                    Environment = table.Column<string>(type: "text", nullable: false),
                    BaseUrl = table.Column<string>(type: "text", nullable: true),
                    AuthUrl = table.Column<string>(type: "text", nullable: true),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    Priority = table.Column<int>(type: "integer", nullable: false, defaultValue: 100),
                    FallbackEnabled = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    TimeoutSeconds = table.Column<int>(type: "integer", nullable: false, defaultValue: 30),
                    MaxRetries = table.Column<int>(type: "integer", nullable: false, defaultValue: 1),
                    Notes = table.Column<string>(type: "text", nullable: true),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    UpdatedBy = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table => table.PrimaryKey("PK_GstApiProviders", x => x.Id));

            migrationBuilder.CreateTable(
                name: "GstApiProviderFeatures",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    Synced = table.Column<bool>(type: "boolean", nullable: false),
                    Deleted = table.Column<bool>(type: "boolean", nullable: false),
                    ProviderId = table.Column<Guid>(type: "uuid", nullable: false),
                    FeatureCode = table.Column<string>(type: "text", nullable: false),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    Priority = table.Column<int>(type: "integer", nullable: false, defaultValue: 100)
                },
                constraints: table => table.PrimaryKey("PK_GstApiProviderFeatures", x => x.Id));

            migrationBuilder.CreateTable(
                name: "GstApiProviderCredentials",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    Synced = table.Column<bool>(type: "boolean", nullable: false),
                    Deleted = table.Column<bool>(type: "boolean", nullable: false),
                    ProviderId = table.Column<Guid>(type: "uuid", nullable: false),
                    CredentialKey = table.Column<string>(type: "text", nullable: false),
                    EncryptedValue = table.Column<string>(type: "text", nullable: false),
                    MaskedDisplayValue = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table => table.PrimaryKey("PK_GstApiProviderCredentials", x => x.Id));

            migrationBuilder.CreateTable(
                name: "GstApiCallLogs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    Synced = table.Column<bool>(type: "boolean", nullable: false),
                    Deleted = table.Column<bool>(type: "boolean", nullable: false),
                    ProviderId = table.Column<Guid>(type: "uuid", nullable: true),
                    FeatureCode = table.Column<string>(type: "text", nullable: false),
                    RequestMethod = table.Column<string>(type: "text", nullable: true),
                    RequestUrl = table.Column<string>(type: "text", nullable: true),
                    RequestHash = table.Column<string>(type: "text", nullable: true),
                    RequestPayloadJson = table.Column<string>(type: "text", nullable: true),
                    ResponseStatusCode = table.Column<int>(type: "integer", nullable: true),
                    ResponsePayloadJson = table.Column<string>(type: "text", nullable: true),
                    IsSuccess = table.Column<bool>(type: "boolean", nullable: false),
                    ErrorCode = table.Column<string>(type: "text", nullable: true),
                    ErrorMessage = table.Column<string>(type: "text", nullable: true),
                    DurationMs = table.Column<int>(type: "integer", nullable: true),
                    InvoiceId = table.Column<Guid>(type: "uuid", nullable: true),
                    PurchaseInvoiceId = table.Column<Guid>(type: "uuid", nullable: true),
                    CustomerId = table.Column<Guid>(type: "uuid", nullable: true),
                    VendorId = table.Column<Guid>(type: "uuid", nullable: true),
                    ProductId = table.Column<Guid>(type: "uuid", nullable: true),
                    Gstin = table.Column<string>(type: "text", nullable: true),
                    HsnCode = table.Column<string>(type: "text", nullable: true),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: true),
                    StoreId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedBy = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table => table.PrimaryKey("PK_GstApiCallLogs", x => x.Id));

            migrationBuilder.CreateTable(
                name: "GstinVerificationCaches",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    Synced = table.Column<bool>(type: "boolean", nullable: false),
                    Deleted = table.Column<bool>(type: "boolean", nullable: false),
                    Gstin = table.Column<string>(type: "text", nullable: false),
                    LegalName = table.Column<string>(type: "text", nullable: true),
                    TradeName = table.Column<string>(type: "text", nullable: true),
                    TaxpayerType = table.Column<string>(type: "text", nullable: true),
                    RegistrationStatus = table.Column<string>(type: "text", nullable: true),
                    RegistrationDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    CancellationDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    StateCode = table.Column<string>(type: "text", nullable: true),
                    StateName = table.Column<string>(type: "text", nullable: true),
                    PrincipalAddress = table.Column<string>(type: "text", nullable: true),
                    AdditionalAddressJson = table.Column<string>(type: "text", nullable: false, defaultValue: "[]"),
                    NatureOfBusinessJson = table.Column<string>(type: "text", nullable: false, defaultValue: "[]"),
                    LastVerifiedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    VerificationSource = table.Column<string>(type: "text", nullable: true),
                    RawResponseJson = table.Column<string>(type: "text", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: true)
                },
                constraints: table => table.PrimaryKey("PK_GstinVerificationCaches", x => x.Id));

            migrationBuilder.CreateTable(
                name: "GstHsnMasters",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    Synced = table.Column<bool>(type: "boolean", nullable: false),
                    Deleted = table.Column<bool>(type: "boolean", nullable: false),
                    HsnCode = table.Column<string>(type: "text", nullable: false),
                    CodeType = table.Column<string>(type: "text", nullable: false),
                    ChapterCode = table.Column<string>(type: "text", nullable: true),
                    Description = table.Column<string>(type: "text", nullable: true),
                    TechnicalDescription = table.Column<string>(type: "text", nullable: true),
                    CommonTradeDescription = table.Column<string>(type: "text", nullable: true),
                    RelatedCodesJson = table.Column<string>(type: "text", nullable: false, defaultValue: "[]"),
                    DefaultUqc = table.Column<string>(type: "text", nullable: true),
                    DefaultGstRate = table.Column<decimal>(type: "numeric(8,3)", precision: 8, scale: 3, nullable: true),
                    CgstRate = table.Column<decimal>(type: "numeric(8,3)", precision: 8, scale: 3, nullable: true),
                    SgstRate = table.Column<decimal>(type: "numeric(8,3)", precision: 8, scale: 3, nullable: true),
                    IgstRate = table.Column<decimal>(type: "numeric(8,3)", precision: 8, scale: 3, nullable: true),
                    CessRate = table.Column<decimal>(type: "numeric(8,3)", precision: 8, scale: 3, nullable: true),
                    EffectiveFrom = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    EffectiveTo = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    Source = table.Column<string>(type: "text", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true)
                },
                constraints: table => table.PrimaryKey("PK_GstHsnMasters", x => x.Id));

            migrationBuilder.CreateTable(
                name: "GstTaxRateRules",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    Synced = table.Column<bool>(type: "boolean", nullable: false),
                    Deleted = table.Column<bool>(type: "boolean", nullable: false),
                    RuleName = table.Column<string>(type: "text", nullable: false),
                    HsnCode = table.Column<string>(type: "text", nullable: true),
                    ProductCategory = table.Column<string>(type: "text", nullable: true),
                    GoodsOrService = table.Column<string>(type: "text", nullable: false),
                    TaxRate = table.Column<decimal>(type: "numeric(8,3)", precision: 8, scale: 3, nullable: false),
                    CgstRate = table.Column<decimal>(type: "numeric(8,3)", precision: 8, scale: 3, nullable: true),
                    SgstRate = table.Column<decimal>(type: "numeric(8,3)", precision: 8, scale: 3, nullable: true),
                    IgstRate = table.Column<decimal>(type: "numeric(8,3)", precision: 8, scale: 3, nullable: true),
                    CessRate = table.Column<decimal>(type: "numeric(8,3)", precision: 8, scale: 3, nullable: true),
                    PriceThresholdFrom = table.Column<decimal>(type: "numeric(18,3)", precision: 18, scale: 3, nullable: true),
                    PriceThresholdTo = table.Column<decimal>(type: "numeric(18,3)", precision: 18, scale: 3, nullable: true),
                    ThresholdBasis = table.Column<string>(type: "text", nullable: true),
                    IntraStateFormula = table.Column<string>(type: "text", nullable: true),
                    InterStateFormula = table.Column<string>(type: "text", nullable: true),
                    EffectiveFrom = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    EffectiveTo = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    Priority = table.Column<int>(type: "integer", nullable: false, defaultValue: 100),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    Notes = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table => table.PrimaryKey("PK_GstTaxRateRules", x => x.Id));

            migrationBuilder.CreateTable(
                name: "GstStateCodes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    Synced = table.Column<bool>(type: "boolean", nullable: false),
                    Deleted = table.Column<bool>(type: "boolean", nullable: false),
                    StateCode = table.Column<string>(type: "text", nullable: false),
                    StateName = table.Column<string>(type: "text", nullable: false),
                    IsUnionTerritory = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    IsOtherTerritory = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false)
                },
                constraints: table => table.PrimaryKey("PK_GstStateCodes", x => x.Id));

            migrationBuilder.CreateTable(
                name: "GstUqcCodes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    Synced = table.Column<bool>(type: "boolean", nullable: false),
                    Deleted = table.Column<bool>(type: "boolean", nullable: false),
                    UqcCode = table.Column<string>(type: "text", nullable: false),
                    Description = table.Column<string>(type: "text", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true)
                },
                constraints: table => table.PrimaryKey("PK_GstUqcCodes", x => x.Id));

            migrationBuilder.CreateTable(
                name: "GstAuditRules",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    Synced = table.Column<bool>(type: "boolean", nullable: false),
                    Deleted = table.Column<bool>(type: "boolean", nullable: false),
                    RuleCode = table.Column<string>(type: "text", nullable: false),
                    RuleName = table.Column<string>(type: "text", nullable: false),
                    ModuleArea = table.Column<string>(type: "text", nullable: false),
                    Severity = table.Column<string>(type: "text", nullable: false),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    StrictMode = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    ConfigJson = table.Column<string>(type: "text", nullable: false, defaultValue: "{}"),
                    MessageTemplate = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table => table.PrimaryKey("PK_GstAuditRules", x => x.Id));

            migrationBuilder.CreateTable(
                name: "GstAuditFindings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    Synced = table.Column<bool>(type: "boolean", nullable: false),
                    Deleted = table.Column<bool>(type: "boolean", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    RuleCode = table.Column<string>(type: "text", nullable: false),
                    Severity = table.Column<string>(type: "text", nullable: false),
                    ModuleArea = table.Column<string>(type: "text", nullable: false),
                    EntityType = table.Column<string>(type: "text", nullable: false),
                    EntityId = table.Column<Guid>(type: "uuid", nullable: true),
                    InvoiceId = table.Column<Guid>(type: "uuid", nullable: true),
                    PurchaseInvoiceId = table.Column<Guid>(type: "uuid", nullable: true),
                    ProductId = table.Column<Guid>(type: "uuid", nullable: true),
                    CustomerId = table.Column<Guid>(type: "uuid", nullable: true),
                    VendorId = table.Column<Guid>(type: "uuid", nullable: true),
                    LineId = table.Column<Guid>(type: "uuid", nullable: true),
                    Gstin = table.Column<string>(type: "text", nullable: true),
                    HsnCode = table.Column<string>(type: "text", nullable: true),
                    Message = table.Column<string>(type: "text", nullable: false),
                    ExpectedValue = table.Column<string>(type: "text", nullable: true),
                    ActualValue = table.Column<string>(type: "text", nullable: true),
                    SuggestedFixJson = table.Column<string>(type: "text", nullable: true),
                    Status = table.Column<string>(type: "text", nullable: false),
                    ReviewedBy = table.Column<string>(type: "text", nullable: true),
                    ReviewedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table => table.PrimaryKey("PK_GstAuditFindings", x => x.Id));

            migrationBuilder.CreateIndex(name: "IX_GstApiProviders_CompanyId_StoreId_IsEnabled_Priority", table: "GstApiProviders", columns: new[] { "CompanyId", "StoreId", "IsEnabled", "Priority" });
            migrationBuilder.CreateIndex(name: "IX_GstApiProviderFeatures_ProviderId_FeatureCode", table: "GstApiProviderFeatures", columns: new[] { "ProviderId", "FeatureCode" }, unique: true);
            migrationBuilder.CreateIndex(name: "IX_GstApiProviderCredentials_ProviderId_CredentialKey", table: "GstApiProviderCredentials", columns: new[] { "ProviderId", "CredentialKey" }, unique: true);
            migrationBuilder.CreateIndex(name: "IX_GstApiCallLogs_CompanyId_FeatureCode_CreatedAt", table: "GstApiCallLogs", columns: new[] { "CompanyId", "FeatureCode", "CreatedAt" });
            migrationBuilder.CreateIndex(name: "IX_GstApiCallLogs_ProviderId_IsSuccess_CreatedAt", table: "GstApiCallLogs", columns: new[] { "ProviderId", "IsSuccess", "CreatedAt" });
            migrationBuilder.CreateIndex(name: "IX_GstinVerificationCaches_Gstin", table: "GstinVerificationCaches", column: "Gstin", unique: true);
            migrationBuilder.CreateIndex(name: "IX_GstHsnMasters_HsnCode", table: "GstHsnMasters", column: "HsnCode");
            migrationBuilder.CreateIndex(name: "IX_GstHsnMasters_CodeType_IsActive", table: "GstHsnMasters", columns: new[] { "CodeType", "IsActive" });
            migrationBuilder.CreateIndex(name: "IX_GstTaxRateRules_HsnCode_EffectiveFrom", table: "GstTaxRateRules", columns: new[] { "HsnCode", "EffectiveFrom" });
            migrationBuilder.CreateIndex(name: "IX_GstTaxRateRules_ProductCategory_EffectiveFrom", table: "GstTaxRateRules", columns: new[] { "ProductCategory", "EffectiveFrom" });
            migrationBuilder.CreateIndex(name: "IX_GstStateCodes_StateCode", table: "GstStateCodes", column: "StateCode", unique: true);
            migrationBuilder.CreateIndex(name: "IX_GstUqcCodes_UqcCode", table: "GstUqcCodes", column: "UqcCode", unique: true);
            migrationBuilder.CreateIndex(name: "IX_GstAuditRules_RuleCode", table: "GstAuditRules", column: "RuleCode", unique: true);
            migrationBuilder.CreateIndex(name: "IX_GstAuditFindings_CompanyId_Status_Severity_CreatedAt", table: "GstAuditFindings", columns: new[] { "CompanyId", "Status", "Severity", "CreatedAt" });
            migrationBuilder.CreateIndex(name: "IX_GstAuditFindings_CompanyId_ModuleArea_RuleCode", table: "GstAuditFindings", columns: new[] { "CompanyId", "ModuleArea", "RuleCode" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "GstApiProviderFeatures");
            migrationBuilder.DropTable(name: "GstApiProviderCredentials");
            migrationBuilder.DropTable(name: "GstApiCallLogs");
            migrationBuilder.DropTable(name: "GstinVerificationCaches");
            migrationBuilder.DropTable(name: "GstHsnMasters");
            migrationBuilder.DropTable(name: "GstTaxRateRules");
            migrationBuilder.DropTable(name: "GstStateCodes");
            migrationBuilder.DropTable(name: "GstUqcCodes");
            migrationBuilder.DropTable(name: "GstAuditRules");
            migrationBuilder.DropTable(name: "GstAuditFindings");
            migrationBuilder.DropTable(name: "GstApiProviders");
        }
    }
}
