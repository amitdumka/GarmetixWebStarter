using Garmetix.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Garmetix.Api.Database;

public static class DatabaseSchemaRepairService
{
    public static async Task RepairPosHeldBillStorageAsync(GarmetixDbContext db, ILogger logger, CancellationToken cancellationToken = default)
    {
        await db.Database.ExecuteSqlRawAsync("""
            CREATE TABLE IF NOT EXISTS "PosHeldBills" (
                "Id" uuid NOT NULL,
                "CreatedAt" timestamp without time zone NOT NULL DEFAULT now(),
                "UpdatedAt" timestamp without time zone NULL,
                "Synced" boolean NOT NULL DEFAULT false,
                "Deleted" boolean NOT NULL DEFAULT false,
                "CompanyId" uuid NOT NULL,
                "CreatedBy" text NULL,
                "StoreGroupId" uuid NOT NULL,
                "StoreId" uuid NOT NULL,
                "ClientHeldBillId" character varying(80) NOT NULL DEFAULT '',
                "HeldAt" timestamp without time zone NOT NULL DEFAULT now(),
                "CustomerName" character varying(160) NOT NULL DEFAULT 'Walk-in Customer',
                "CustomerMobileNumber" character varying(40) NOT NULL DEFAULT '',
                "ItemCount" integer NOT NULL DEFAULT 0,
                "Quantity" numeric(18,2) NOT NULL DEFAULT 0,
                "PayableTotal" numeric(18,2) NOT NULL DEFAULT 0,
                "Note" character varying(500) NOT NULL DEFAULT '',
                "DraftJson" text NOT NULL DEFAULT '{{}}',
                "Status" character varying(40) NOT NULL DEFAULT 'Held',
                "HeldByUserId" uuid NULL,
                "HeldByUserName" character varying(160) NOT NULL DEFAULT '',
                "ResumedAt" timestamp without time zone NULL,
                CONSTRAINT "PK_PosHeldBills" PRIMARY KEY ("Id")
            );

            ALTER TABLE "PosHeldBills" ADD COLUMN IF NOT EXISTS "CreatedAt" timestamp without time zone NOT NULL DEFAULT now();
            ALTER TABLE "PosHeldBills" ADD COLUMN IF NOT EXISTS "UpdatedAt" timestamp without time zone NULL;
            ALTER TABLE "PosHeldBills" ADD COLUMN IF NOT EXISTS "Synced" boolean NOT NULL DEFAULT false;
            ALTER TABLE "PosHeldBills" ADD COLUMN IF NOT EXISTS "Deleted" boolean NOT NULL DEFAULT false;
            ALTER TABLE "PosHeldBills" ADD COLUMN IF NOT EXISTS "CompanyId" uuid NOT NULL DEFAULT '00000000-0000-0000-0000-000000000000';
            ALTER TABLE "PosHeldBills" ADD COLUMN IF NOT EXISTS "CreatedBy" text NULL;
            ALTER TABLE "PosHeldBills" ADD COLUMN IF NOT EXISTS "StoreGroupId" uuid NOT NULL DEFAULT '00000000-0000-0000-0000-000000000000';
            ALTER TABLE "PosHeldBills" ADD COLUMN IF NOT EXISTS "StoreId" uuid NOT NULL DEFAULT '00000000-0000-0000-0000-000000000000';
            ALTER TABLE "PosHeldBills" ADD COLUMN IF NOT EXISTS "ClientHeldBillId" character varying(80) NOT NULL DEFAULT '';
            ALTER TABLE "PosHeldBills" ADD COLUMN IF NOT EXISTS "HeldAt" timestamp without time zone NOT NULL DEFAULT now();
            ALTER TABLE "PosHeldBills" ADD COLUMN IF NOT EXISTS "CustomerName" character varying(160) NOT NULL DEFAULT 'Walk-in Customer';
            ALTER TABLE "PosHeldBills" ADD COLUMN IF NOT EXISTS "CustomerMobileNumber" character varying(40) NOT NULL DEFAULT '';
            ALTER TABLE "PosHeldBills" ADD COLUMN IF NOT EXISTS "ItemCount" integer NOT NULL DEFAULT 0;
            ALTER TABLE "PosHeldBills" ADD COLUMN IF NOT EXISTS "Quantity" numeric(18,2) NOT NULL DEFAULT 0;
            ALTER TABLE "PosHeldBills" ADD COLUMN IF NOT EXISTS "PayableTotal" numeric(18,2) NOT NULL DEFAULT 0;
            ALTER TABLE "PosHeldBills" ADD COLUMN IF NOT EXISTS "Note" character varying(500) NOT NULL DEFAULT '';
            ALTER TABLE "PosHeldBills" ADD COLUMN IF NOT EXISTS "DraftJson" text NOT NULL DEFAULT '{{}}';
            ALTER TABLE "PosHeldBills" ADD COLUMN IF NOT EXISTS "Status" character varying(40) NOT NULL DEFAULT 'Held';
            ALTER TABLE "PosHeldBills" ADD COLUMN IF NOT EXISTS "HeldByUserId" uuid NULL;
            ALTER TABLE "PosHeldBills" ADD COLUMN IF NOT EXISTS "HeldByUserName" character varying(160) NOT NULL DEFAULT '';
            ALTER TABLE "PosHeldBills" ADD COLUMN IF NOT EXISTS "ResumedAt" timestamp without time zone NULL;

            CREATE INDEX IF NOT EXISTS "IX_PosHeldBills_CompanyId_StoreGroupId_StoreId_Status_HeldAt"
                ON "PosHeldBills" ("CompanyId", "StoreGroupId", "StoreId", "Status", "HeldAt");
            CREATE INDEX IF NOT EXISTS "IX_PosHeldBills_CompanyId_ClientHeldBillId"
                ON "PosHeldBills" ("CompanyId", "ClientHeldBillId");
            """, cancellationToken);

        logger.LogInformation("POS held bill storage repair check completed.");
    }

    public static async Task RepairGstReturnStorageAsync(GarmetixDbContext db, ILogger logger, CancellationToken cancellationToken = default)
    {
        // Keep this repair intentionally small and separate from the general schema repair.
        // Older Docker volumes can have EF migration history marked as current while these
        // GST draft tables are missing, so every GST draft endpoint calls this before querying.
        await db.Database.ExecuteSqlRawAsync("""
            CREATE TABLE IF NOT EXISTS "GstReturnDrafts" (
                "Id" uuid NOT NULL,
                "CreatedAt" timestamp without time zone NOT NULL DEFAULT now(),
                "UpdatedAt" timestamp without time zone NULL,
                "Synced" boolean NOT NULL DEFAULT false,
                "Deleted" boolean NOT NULL DEFAULT false,
                "CompanyId" uuid NOT NULL,
                "CreatedBy" text NULL,
                "Form" text NOT NULL DEFAULT '',
                "Gstin" text NOT NULL DEFAULT '',
                "ReturnPeriod" text NOT NULL DEFAULT '',
                "Title" text NOT NULL DEFAULT '',
                "Status" text NOT NULL DEFAULT 'Draft',
                "PayloadJson" text NOT NULL DEFAULT '{{}}',
                "LastPreviewIssuesJson" text NOT NULL DEFAULT '[]',
                "RowCount" integer NOT NULL DEFAULT 0,
                "TaxableValue" numeric(18,2) NOT NULL DEFAULT 0,
                "IntegratedTax" numeric(18,2) NOT NULL DEFAULT 0,
                "CentralTax" numeric(18,2) NOT NULL DEFAULT 0,
                "StateTax" numeric(18,2) NOT NULL DEFAULT 0,
                "Cess" numeric(18,2) NOT NULL DEFAULT 0,
                "CreatedByUserId" uuid NULL,
                "CreatedByUserName" text NOT NULL DEFAULT '',
                "UpdatedByUserId" uuid NULL,
                "UpdatedByUserName" text NOT NULL DEFAULT '',
                "FiledAt" timestamp without time zone NULL,
                "LockedAt" timestamp without time zone NULL,
                CONSTRAINT "PK_GstReturnDrafts" PRIMARY KEY ("Id")
            );
            """, cancellationToken);

        await db.Database.ExecuteSqlRawAsync("""
            CREATE TABLE IF NOT EXISTS "GstReturnAuditEntries" (
                "Id" uuid NOT NULL,
                "CreatedAt" timestamp without time zone NOT NULL DEFAULT now(),
                "UpdatedAt" timestamp without time zone NULL,
                "Synced" boolean NOT NULL DEFAULT false,
                "Deleted" boolean NOT NULL DEFAULT false,
                "CompanyId" uuid NOT NULL,
                "CreatedBy" text NULL,
                "DraftId" uuid NOT NULL,
                "Form" text NOT NULL DEFAULT '',
                "ReturnPeriod" text NOT NULL DEFAULT '',
                "Gstin" text NOT NULL DEFAULT '',
                "Action" text NOT NULL DEFAULT '',
                "Summary" text NOT NULL DEFAULT '',
                "ActorUserId" uuid NULL,
                "ActorName" text NOT NULL DEFAULT '',
                "DetailsJson" text NOT NULL DEFAULT '{{}}',
                CONSTRAINT "PK_GstReturnAuditEntries" PRIMARY KEY ("Id")
            );
            """, cancellationToken);

        await db.Database.ExecuteSqlRawAsync("""
            ALTER TABLE "GstReturnDrafts" ADD COLUMN IF NOT EXISTS "Title" text NOT NULL DEFAULT '';
            ALTER TABLE "GstReturnDrafts" ADD COLUMN IF NOT EXISTS "LastPreviewIssuesJson" text NOT NULL DEFAULT '[]';
            ALTER TABLE "GstReturnDrafts" ADD COLUMN IF NOT EXISTS "CreatedByUserId" uuid NULL;
            ALTER TABLE "GstReturnDrafts" ADD COLUMN IF NOT EXISTS "CreatedByUserName" text NOT NULL DEFAULT '';
            ALTER TABLE "GstReturnDrafts" ADD COLUMN IF NOT EXISTS "UpdatedByUserId" uuid NULL;
            ALTER TABLE "GstReturnDrafts" ADD COLUMN IF NOT EXISTS "UpdatedByUserName" text NOT NULL DEFAULT '';
            ALTER TABLE "GstReturnDrafts" ADD COLUMN IF NOT EXISTS "FiledAt" timestamp without time zone NULL;
            ALTER TABLE "GstReturnDrafts" ADD COLUMN IF NOT EXISTS "LockedAt" timestamp without time zone NULL;

            ALTER TABLE "GstReturnAuditEntries" ADD COLUMN IF NOT EXISTS "ActorUserId" uuid NULL;
            ALTER TABLE "GstReturnAuditEntries" ADD COLUMN IF NOT EXISTS "ActorName" text NOT NULL DEFAULT '';
            ALTER TABLE "GstReturnAuditEntries" ADD COLUMN IF NOT EXISTS "DetailsJson" text NOT NULL DEFAULT '{{}}';
            """, cancellationToken);

        await db.Database.ExecuteSqlRawAsync("""
            CREATE INDEX IF NOT EXISTS "IX_GstReturnDrafts_CompanyId_Form_ReturnPeriod_Gstin" ON "GstReturnDrafts" ("CompanyId", "Form", "ReturnPeriod", "Gstin");
            CREATE INDEX IF NOT EXISTS "IX_GstReturnDrafts_CompanyId_Status_UpdatedAt" ON "GstReturnDrafts" ("CompanyId", "Status", "UpdatedAt");
            CREATE INDEX IF NOT EXISTS "IX_GstReturnAuditEntries_CompanyId_DraftId_CreatedAt" ON "GstReturnAuditEntries" ("CompanyId", "DraftId", "CreatedAt");
            CREATE INDEX IF NOT EXISTS "IX_GstReturnAuditEntries_CompanyId_Form_ReturnPeriod" ON "GstReturnAuditEntries" ("CompanyId", "Form", "ReturnPeriod");
            """, cancellationToken);

        logger.LogInformation("GST return draft storage repair check completed.");
    }

    public static async Task RepairGstTaxStorageAsync(GarmetixDbContext db, ILogger logger, CancellationToken cancellationToken = default)
    {
        // The GST & Taxes module (providers/credentials/logs/HSN+rate master/audit engine) can be added
        // to a Docker volume whose EF migration history was already baselined before these tables existed.
        // Every GST & Taxes endpoint calls this before querying, mirroring RepairGstReturnStorageAsync above.
        await db.Database.ExecuteSqlRawAsync("""
            CREATE TABLE IF NOT EXISTS "GstApiProviders" (
                "Id" uuid NOT NULL,
                "CreatedAt" timestamp without time zone NOT NULL DEFAULT now(),
                "UpdatedAt" timestamp without time zone NULL,
                "Synced" boolean NOT NULL DEFAULT false,
                "Deleted" boolean NOT NULL DEFAULT false,
                "CompanyId" uuid NULL,
                "StoreGroupId" uuid NULL,
                "StoreId" uuid NULL,
                "ProviderName" text NOT NULL DEFAULT '',
                "ProviderType" text NOT NULL DEFAULT 'LocalMasterOnly',
                "Environment" text NOT NULL DEFAULT 'Sandbox',
                "BaseUrl" text NULL,
                "AuthUrl" text NULL,
                "IsEnabled" boolean NOT NULL DEFAULT true,
                "Priority" integer NOT NULL DEFAULT 100,
                "FallbackEnabled" boolean NOT NULL DEFAULT true,
                "TimeoutSeconds" integer NOT NULL DEFAULT 30,
                "MaxRetries" integer NOT NULL DEFAULT 1,
                "Notes" text NULL,
                "CreatedBy" text NULL,
                "UpdatedBy" text NULL,
                CONSTRAINT "PK_GstApiProviders" PRIMARY KEY ("Id")
            );

            CREATE TABLE IF NOT EXISTS "GstApiProviderFeatures" (
                "Id" uuid NOT NULL,
                "CreatedAt" timestamp without time zone NOT NULL DEFAULT now(),
                "UpdatedAt" timestamp without time zone NULL,
                "Synced" boolean NOT NULL DEFAULT false,
                "Deleted" boolean NOT NULL DEFAULT false,
                "ProviderId" uuid NOT NULL,
                "FeatureCode" text NOT NULL DEFAULT '',
                "IsEnabled" boolean NOT NULL DEFAULT true,
                "Priority" integer NOT NULL DEFAULT 100,
                CONSTRAINT "PK_GstApiProviderFeatures" PRIMARY KEY ("Id")
            );

            CREATE TABLE IF NOT EXISTS "GstApiProviderCredentials" (
                "Id" uuid NOT NULL,
                "CreatedAt" timestamp without time zone NOT NULL DEFAULT now(),
                "UpdatedAt" timestamp without time zone NULL,
                "Synced" boolean NOT NULL DEFAULT false,
                "Deleted" boolean NOT NULL DEFAULT false,
                "ProviderId" uuid NOT NULL,
                "CredentialKey" text NOT NULL DEFAULT '',
                "EncryptedValue" text NOT NULL DEFAULT '',
                "MaskedDisplayValue" text NULL,
                CONSTRAINT "PK_GstApiProviderCredentials" PRIMARY KEY ("Id")
            );

            CREATE TABLE IF NOT EXISTS "GstApiCallLogs" (
                "Id" uuid NOT NULL,
                "CreatedAt" timestamp without time zone NOT NULL DEFAULT now(),
                "UpdatedAt" timestamp without time zone NULL,
                "Synced" boolean NOT NULL DEFAULT false,
                "Deleted" boolean NOT NULL DEFAULT false,
                "ProviderId" uuid NULL,
                "FeatureCode" text NOT NULL DEFAULT '',
                "RequestMethod" text NULL,
                "RequestUrl" text NULL,
                "RequestHash" text NULL,
                "RequestPayloadJson" text NULL,
                "ResponseStatusCode" integer NULL,
                "ResponsePayloadJson" text NULL,
                "IsSuccess" boolean NOT NULL DEFAULT false,
                "ErrorCode" text NULL,
                "ErrorMessage" text NULL,
                "DurationMs" integer NULL,
                "InvoiceId" uuid NULL,
                "PurchaseInvoiceId" uuid NULL,
                "CustomerId" uuid NULL,
                "VendorId" uuid NULL,
                "ProductId" uuid NULL,
                "Gstin" text NULL,
                "HsnCode" text NULL,
                "CompanyId" uuid NULL,
                "StoreId" uuid NULL,
                "CreatedBy" text NULL,
                CONSTRAINT "PK_GstApiCallLogs" PRIMARY KEY ("Id")
            );

            CREATE TABLE IF NOT EXISTS "GstinVerificationCaches" (
                "Id" uuid NOT NULL,
                "CreatedAt" timestamp without time zone NOT NULL DEFAULT now(),
                "UpdatedAt" timestamp without time zone NULL,
                "Synced" boolean NOT NULL DEFAULT false,
                "Deleted" boolean NOT NULL DEFAULT false,
                "Gstin" text NOT NULL DEFAULT '',
                "LegalName" text NULL,
                "TradeName" text NULL,
                "TaxpayerType" text NULL,
                "RegistrationStatus" text NULL,
                "RegistrationDate" timestamp without time zone NULL,
                "CancellationDate" timestamp without time zone NULL,
                "StateCode" text NULL,
                "StateName" text NULL,
                "PrincipalAddress" text NULL,
                "AdditionalAddressJson" text NOT NULL DEFAULT '[]',
                "NatureOfBusinessJson" text NOT NULL DEFAULT '[]',
                "LastVerifiedAt" timestamp without time zone NULL,
                "VerificationSource" text NULL,
                "RawResponseJson" text NULL,
                "IsActive" boolean NULL,
                CONSTRAINT "PK_GstinVerificationCaches" PRIMARY KEY ("Id")
            );

            CREATE TABLE IF NOT EXISTS "GstHsnMasters" (
                "Id" uuid NOT NULL,
                "CreatedAt" timestamp without time zone NOT NULL DEFAULT now(),
                "UpdatedAt" timestamp without time zone NULL,
                "Synced" boolean NOT NULL DEFAULT false,
                "Deleted" boolean NOT NULL DEFAULT false,
                "HsnCode" text NOT NULL DEFAULT '',
                "CodeType" text NOT NULL DEFAULT 'Goods',
                "ChapterCode" text NULL,
                "Description" text NULL,
                "TechnicalDescription" text NULL,
                "CommonTradeDescription" text NULL,
                "RelatedCodesJson" text NOT NULL DEFAULT '[]',
                "DefaultUqc" text NULL,
                "DefaultGstRate" numeric(8,3) NULL,
                "CgstRate" numeric(8,3) NULL,
                "SgstRate" numeric(8,3) NULL,
                "IgstRate" numeric(8,3) NULL,
                "CessRate" numeric(8,3) NULL,
                "EffectiveFrom" timestamp without time zone NULL,
                "EffectiveTo" timestamp without time zone NULL,
                "Source" text NULL,
                "IsActive" boolean NOT NULL DEFAULT true,
                CONSTRAINT "PK_GstHsnMasters" PRIMARY KEY ("Id")
            );

            CREATE TABLE IF NOT EXISTS "GstTaxRateRules" (
                "Id" uuid NOT NULL,
                "CreatedAt" timestamp without time zone NOT NULL DEFAULT now(),
                "UpdatedAt" timestamp without time zone NULL,
                "Synced" boolean NOT NULL DEFAULT false,
                "Deleted" boolean NOT NULL DEFAULT false,
                "RuleName" text NOT NULL DEFAULT '',
                "HsnCode" text NULL,
                "ProductCategory" text NULL,
                "GoodsOrService" text NOT NULL DEFAULT 'Goods',
                "TaxRate" numeric(8,3) NOT NULL DEFAULT 0,
                "CgstRate" numeric(8,3) NULL,
                "SgstRate" numeric(8,3) NULL,
                "IgstRate" numeric(8,3) NULL,
                "CessRate" numeric(8,3) NULL,
                "PriceThresholdFrom" numeric(18,3) NULL,
                "PriceThresholdTo" numeric(18,3) NULL,
                "ThresholdBasis" text NULL,
                "IntraStateFormula" text NULL,
                "InterStateFormula" text NULL,
                "EffectiveFrom" timestamp without time zone NOT NULL DEFAULT now(),
                "EffectiveTo" timestamp without time zone NULL,
                "Priority" integer NOT NULL DEFAULT 100,
                "IsActive" boolean NOT NULL DEFAULT true,
                "Notes" text NULL,
                CONSTRAINT "PK_GstTaxRateRules" PRIMARY KEY ("Id")
            );

            CREATE TABLE IF NOT EXISTS "GstStateCodes" (
                "Id" uuid NOT NULL,
                "CreatedAt" timestamp without time zone NOT NULL DEFAULT now(),
                "UpdatedAt" timestamp without time zone NULL,
                "Synced" boolean NOT NULL DEFAULT false,
                "Deleted" boolean NOT NULL DEFAULT false,
                "StateCode" text NOT NULL DEFAULT '',
                "StateName" text NOT NULL DEFAULT '',
                "IsUnionTerritory" boolean NOT NULL DEFAULT false,
                "IsOtherTerritory" boolean NOT NULL DEFAULT false,
                CONSTRAINT "PK_GstStateCodes" PRIMARY KEY ("Id")
            );

            CREATE TABLE IF NOT EXISTS "GstUqcCodes" (
                "Id" uuid NOT NULL,
                "CreatedAt" timestamp without time zone NOT NULL DEFAULT now(),
                "UpdatedAt" timestamp without time zone NULL,
                "Synced" boolean NOT NULL DEFAULT false,
                "Deleted" boolean NOT NULL DEFAULT false,
                "UqcCode" text NOT NULL DEFAULT '',
                "Description" text NOT NULL DEFAULT '',
                "IsActive" boolean NOT NULL DEFAULT true,
                CONSTRAINT "PK_GstUqcCodes" PRIMARY KEY ("Id")
            );

            CREATE TABLE IF NOT EXISTS "GstAuditRules" (
                "Id" uuid NOT NULL,
                "CreatedAt" timestamp without time zone NOT NULL DEFAULT now(),
                "UpdatedAt" timestamp without time zone NULL,
                "Synced" boolean NOT NULL DEFAULT false,
                "Deleted" boolean NOT NULL DEFAULT false,
                "RuleCode" text NOT NULL DEFAULT '',
                "RuleName" text NOT NULL DEFAULT '',
                "ModuleArea" text NOT NULL DEFAULT '',
                "Severity" text NOT NULL DEFAULT 'Warning',
                "IsEnabled" boolean NOT NULL DEFAULT true,
                "StrictMode" boolean NOT NULL DEFAULT false,
                "ConfigJson" text NOT NULL DEFAULT '{{}}',
                "MessageTemplate" text NULL,
                CONSTRAINT "PK_GstAuditRules" PRIMARY KEY ("Id")
            );

            CREATE TABLE IF NOT EXISTS "GstAuditFindings" (
                "Id" uuid NOT NULL,
                "CreatedAt" timestamp without time zone NOT NULL DEFAULT now(),
                "UpdatedAt" timestamp without time zone NULL,
                "Synced" boolean NOT NULL DEFAULT false,
                "Deleted" boolean NOT NULL DEFAULT false,
                "CompanyId" uuid NOT NULL,
                "CreatedBy" text NULL,
                "RuleCode" text NOT NULL DEFAULT '',
                "Severity" text NOT NULL DEFAULT 'Warning',
                "ModuleArea" text NOT NULL DEFAULT '',
                "EntityType" text NOT NULL DEFAULT '',
                "EntityId" uuid NULL,
                "InvoiceId" uuid NULL,
                "PurchaseInvoiceId" uuid NULL,
                "ProductId" uuid NULL,
                "CustomerId" uuid NULL,
                "VendorId" uuid NULL,
                "LineId" uuid NULL,
                "Gstin" text NULL,
                "HsnCode" text NULL,
                "Message" text NOT NULL DEFAULT '',
                "ExpectedValue" text NULL,
                "ActualValue" text NULL,
                "SuggestedFixJson" text NULL,
                "Status" text NOT NULL DEFAULT 'Open',
                "ReviewedBy" text NULL,
                "ReviewedAt" timestamp without time zone NULL,
                CONSTRAINT "PK_GstAuditFindings" PRIMARY KEY ("Id")
            );

            CREATE TABLE IF NOT EXISTS "GstEinvoiceIrnRecords" (
                "Id" uuid NOT NULL,
                "CreatedAt" timestamp without time zone NOT NULL DEFAULT now(),
                "UpdatedAt" timestamp without time zone NULL,
                "Synced" boolean NOT NULL DEFAULT false,
                "Deleted" boolean NOT NULL DEFAULT false,
                "CompanyId" uuid NOT NULL,
                "CreatedBy" text NULL,
                "InvoiceId" uuid NOT NULL,
                "ProviderId" uuid NULL,
                "Irn" text NULL,
                "AckNumber" text NULL,
                "AckDate" timestamp without time zone NULL,
                "SignedInvoiceJson" text NULL,
                "SignedQrCode" text NULL,
                "Status" text NOT NULL DEFAULT 'NotConfigured',
                "CancelReason" text NULL,
                "CancelledAt" timestamp without time zone NULL,
                "ErrorCode" text NULL,
                "ErrorMessage" text NULL,
                "LastAttemptAt" timestamp without time zone NULL,
                CONSTRAINT "PK_GstEinvoiceIrnRecords" PRIMARY KEY ("Id")
            );

            CREATE TABLE IF NOT EXISTS "GstEwaybillRecords" (
                "Id" uuid NOT NULL,
                "CreatedAt" timestamp without time zone NOT NULL DEFAULT now(),
                "UpdatedAt" timestamp without time zone NULL,
                "Synced" boolean NOT NULL DEFAULT false,
                "Deleted" boolean NOT NULL DEFAULT false,
                "CompanyId" uuid NOT NULL,
                "CreatedBy" text NULL,
                "InvoiceId" uuid NULL,
                "PurchaseInvoiceId" uuid NULL,
                "ProviderId" uuid NULL,
                "EwbNumber" text NULL,
                "EwbDate" timestamp without time zone NULL,
                "ValidUpto" timestamp without time zone NULL,
                "VehicleNumber" text NULL,
                "TransporterId" text NULL,
                "TransporterName" text NULL,
                "DistanceKm" integer NULL,
                "Status" text NOT NULL DEFAULT 'NotConfigured',
                "CancelReason" text NULL,
                "CancelledAt" timestamp without time zone NULL,
                "ErrorCode" text NULL,
                "ErrorMessage" text NULL,
                "LastAttemptAt" timestamp without time zone NULL,
                CONSTRAINT "PK_GstEwaybillRecords" PRIMARY KEY ("Id")
            );
            """, cancellationToken);

        await db.Database.ExecuteSqlRawAsync("""
            CREATE INDEX IF NOT EXISTS "IX_GstApiProviders_CompanyId_StoreId_IsEnabled_Priority" ON "GstApiProviders" ("CompanyId", "StoreId", "IsEnabled", "Priority");
            CREATE UNIQUE INDEX IF NOT EXISTS "IX_GstApiProviderFeatures_ProviderId_FeatureCode" ON "GstApiProviderFeatures" ("ProviderId", "FeatureCode");
            CREATE UNIQUE INDEX IF NOT EXISTS "IX_GstApiProviderCredentials_ProviderId_CredentialKey" ON "GstApiProviderCredentials" ("ProviderId", "CredentialKey");
            CREATE INDEX IF NOT EXISTS "IX_GstApiCallLogs_CompanyId_FeatureCode_CreatedAt" ON "GstApiCallLogs" ("CompanyId", "FeatureCode", "CreatedAt");
            CREATE INDEX IF NOT EXISTS "IX_GstApiCallLogs_ProviderId_IsSuccess_CreatedAt" ON "GstApiCallLogs" ("ProviderId", "IsSuccess", "CreatedAt");
            CREATE UNIQUE INDEX IF NOT EXISTS "IX_GstinVerificationCaches_Gstin" ON "GstinVerificationCaches" ("Gstin");
            CREATE INDEX IF NOT EXISTS "IX_GstHsnMasters_HsnCode" ON "GstHsnMasters" ("HsnCode");
            CREATE INDEX IF NOT EXISTS "IX_GstHsnMasters_CodeType_IsActive" ON "GstHsnMasters" ("CodeType", "IsActive");
            CREATE INDEX IF NOT EXISTS "IX_GstTaxRateRules_HsnCode_EffectiveFrom" ON "GstTaxRateRules" ("HsnCode", "EffectiveFrom");
            CREATE INDEX IF NOT EXISTS "IX_GstTaxRateRules_ProductCategory_EffectiveFrom" ON "GstTaxRateRules" ("ProductCategory", "EffectiveFrom");
            CREATE UNIQUE INDEX IF NOT EXISTS "IX_GstStateCodes_StateCode" ON "GstStateCodes" ("StateCode");
            CREATE UNIQUE INDEX IF NOT EXISTS "IX_GstUqcCodes_UqcCode" ON "GstUqcCodes" ("UqcCode");
            CREATE UNIQUE INDEX IF NOT EXISTS "IX_GstAuditRules_RuleCode" ON "GstAuditRules" ("RuleCode");
            CREATE INDEX IF NOT EXISTS "IX_GstAuditFindings_CompanyId_Status_Severity_CreatedAt" ON "GstAuditFindings" ("CompanyId", "Status", "Severity", "CreatedAt");
            CREATE INDEX IF NOT EXISTS "IX_GstAuditFindings_CompanyId_ModuleArea_RuleCode" ON "GstAuditFindings" ("CompanyId", "ModuleArea", "RuleCode");
            CREATE UNIQUE INDEX IF NOT EXISTS "IX_GstEinvoiceIrnRecords_CompanyId_InvoiceId" ON "GstEinvoiceIrnRecords" ("CompanyId", "InvoiceId");
            CREATE INDEX IF NOT EXISTS "IX_GstEinvoiceIrnRecords_CompanyId_Status" ON "GstEinvoiceIrnRecords" ("CompanyId", "Status");
            CREATE INDEX IF NOT EXISTS "IX_GstEwaybillRecords_CompanyId_InvoiceId" ON "GstEwaybillRecords" ("CompanyId", "InvoiceId");
            CREATE INDEX IF NOT EXISTS "IX_GstEwaybillRecords_CompanyId_PurchaseInvoiceId" ON "GstEwaybillRecords" ("CompanyId", "PurchaseInvoiceId");
            CREATE INDEX IF NOT EXISTS "IX_GstEwaybillRecords_CompanyId_Status" ON "GstEwaybillRecords" ("CompanyId", "Status");
            """, cancellationToken);

        logger.LogInformation("GST & Taxes module storage repair check completed.");
    }


public static async Task RepairCashVoucherConversionStorageAsync(GarmetixDbContext db, ILogger logger, CancellationToken cancellationToken = default)
{
    // Older Docker volumes can have the cash-voucher conversion DbSet in the code
    // while the physical table is missing. Voucher edit/delete checks query this table
    // before mutating records, so the absence of the table breaks normal voucher and
    // cash-voucher editing/deleting. Keep this repair idempotent and safe to run at startup.
    await db.Database.ExecuteSqlRawAsync("""
        CREATE TABLE IF NOT EXISTS "CashVoucherConversions" (
            "Id" uuid NOT NULL,
            "CreatedAt" timestamp without time zone NOT NULL DEFAULT now(),
            "UpdatedAt" timestamp without time zone NULL,
            "Synced" boolean NOT NULL DEFAULT false,
            "Deleted" boolean NOT NULL DEFAULT false,
            "CompanyId" uuid NOT NULL,
            "CreatedBy" text NULL,
            "StoreGroupId" uuid NOT NULL,
            "StoreId" uuid NOT NULL,
            "Direction" text NOT NULL DEFAULT '',
            "CashVoucherId" uuid NOT NULL,
            "VoucherId" uuid NOT NULL,
            "CashVoucherNumber" text NOT NULL DEFAULT '',
            "VoucherNumber" text NOT NULL DEFAULT '',
            "VoucherType" integer NOT NULL DEFAULT 0,
            "Amount" numeric(18,2) NOT NULL DEFAULT 0,
            "PartyName" text NOT NULL DEFAULT '',
            "Particulars" text NOT NULL DEFAULT '',
            "Reason" text NOT NULL DEFAULT '',
            "ConvertedByUserId" uuid NOT NULL DEFAULT '00000000-0000-0000-0000-000000000000',
            "ConvertedByUserName" text NOT NULL DEFAULT '',
            "ConvertedAt" timestamp without time zone NOT NULL DEFAULT now(),
            CONSTRAINT "PK_CashVoucherConversions" PRIMARY KEY ("Id")
        );

        ALTER TABLE "CashVoucherConversions" ADD COLUMN IF NOT EXISTS "CreatedAt" timestamp without time zone NOT NULL DEFAULT now();
        ALTER TABLE "CashVoucherConversions" ADD COLUMN IF NOT EXISTS "UpdatedAt" timestamp without time zone NULL;
        ALTER TABLE "CashVoucherConversions" ADD COLUMN IF NOT EXISTS "Synced" boolean NOT NULL DEFAULT false;
        ALTER TABLE "CashVoucherConversions" ADD COLUMN IF NOT EXISTS "Deleted" boolean NOT NULL DEFAULT false;
        ALTER TABLE "CashVoucherConversions" ADD COLUMN IF NOT EXISTS "CompanyId" uuid NOT NULL DEFAULT '00000000-0000-0000-0000-000000000000';
        ALTER TABLE "CashVoucherConversions" ADD COLUMN IF NOT EXISTS "CreatedBy" text NULL;
        ALTER TABLE "CashVoucherConversions" ADD COLUMN IF NOT EXISTS "StoreGroupId" uuid NOT NULL DEFAULT '00000000-0000-0000-0000-000000000000';
        ALTER TABLE "CashVoucherConversions" ADD COLUMN IF NOT EXISTS "StoreId" uuid NOT NULL DEFAULT '00000000-0000-0000-0000-000000000000';
        ALTER TABLE "CashVoucherConversions" ADD COLUMN IF NOT EXISTS "Direction" text NOT NULL DEFAULT '';
        ALTER TABLE "CashVoucherConversions" ADD COLUMN IF NOT EXISTS "CashVoucherId" uuid NOT NULL DEFAULT '00000000-0000-0000-0000-000000000000';
        ALTER TABLE "CashVoucherConversions" ADD COLUMN IF NOT EXISTS "VoucherId" uuid NOT NULL DEFAULT '00000000-0000-0000-0000-000000000000';
        ALTER TABLE "CashVoucherConversions" ADD COLUMN IF NOT EXISTS "CashVoucherNumber" text NOT NULL DEFAULT '';
        ALTER TABLE "CashVoucherConversions" ADD COLUMN IF NOT EXISTS "VoucherNumber" text NOT NULL DEFAULT '';
        ALTER TABLE "CashVoucherConversions" ADD COLUMN IF NOT EXISTS "VoucherType" integer NOT NULL DEFAULT 0;
        ALTER TABLE "CashVoucherConversions" ADD COLUMN IF NOT EXISTS "Amount" numeric(18,2) NOT NULL DEFAULT 0;
        ALTER TABLE "CashVoucherConversions" ADD COLUMN IF NOT EXISTS "PartyName" text NOT NULL DEFAULT '';
        ALTER TABLE "CashVoucherConversions" ADD COLUMN IF NOT EXISTS "Particulars" text NOT NULL DEFAULT '';
        ALTER TABLE "CashVoucherConversions" ADD COLUMN IF NOT EXISTS "Reason" text NOT NULL DEFAULT '';
        ALTER TABLE "CashVoucherConversions" ADD COLUMN IF NOT EXISTS "ConvertedByUserId" uuid NOT NULL DEFAULT '00000000-0000-0000-0000-000000000000';
        ALTER TABLE "CashVoucherConversions" ADD COLUMN IF NOT EXISTS "ConvertedByUserName" text NOT NULL DEFAULT '';
        ALTER TABLE "CashVoucherConversions" ADD COLUMN IF NOT EXISTS "ConvertedAt" timestamp without time zone NOT NULL DEFAULT now();

        CREATE INDEX IF NOT EXISTS "IX_CashVoucherConversions_CompanyId_CashVoucherId" ON "CashVoucherConversions" ("CompanyId", "CashVoucherId");
        CREATE INDEX IF NOT EXISTS "IX_CashVoucherConversions_CompanyId_VoucherId" ON "CashVoucherConversions" ("CompanyId", "VoucherId");
        CREATE INDEX IF NOT EXISTS "IX_CashVoucherConversions_CompanyId_StoreId_ConvertedAt" ON "CashVoucherConversions" ("CompanyId", "StoreId", "ConvertedAt");
        """, cancellationToken);

    logger.LogInformation("Cash voucher conversion storage repair check completed.");
}


public static async Task RepairStoreDayStorageAsync(GarmetixDbContext db, ILogger logger, CancellationToken cancellationToken = default)
{
    await db.Database.ExecuteSqlRawAsync("""
        CREATE TABLE IF NOT EXISTS "CashDetails" (
            "Id" uuid NOT NULL,
            "CreatedAt" timestamp without time zone NOT NULL DEFAULT now(),
            "UpdatedAt" timestamp without time zone NULL,
            "Synced" boolean NOT NULL DEFAULT false,
            "Deleted" boolean NOT NULL DEFAULT false,
            "StoreId" uuid NOT NULL,
            "OnDate" timestamp without time zone NOT NULL,
            "Amount" numeric(18,2) NOT NULL DEFAULT 0,
            "N2000" integer NOT NULL DEFAULT 0,
            "N500" integer NOT NULL DEFAULT 0,
            "N200" integer NOT NULL DEFAULT 0,
            "N100" integer NOT NULL DEFAULT 0,
            "N50" integer NOT NULL DEFAULT 0,
            "NC20" integer NOT NULL DEFAULT 0,
            "NC10" integer NOT NULL DEFAULT 0,
            "NC5" integer NOT NULL DEFAULT 0,
            "NC2" integer NOT NULL DEFAULT 0,
            "NC1" integer NOT NULL DEFAULT 0,
            "CreatedBy" text NULL,
            CONSTRAINT "PK_CashDetails" PRIMARY KEY ("Id")
        );

        CREATE TABLE IF NOT EXISTS "DayBegins" (
            "Id" uuid NOT NULL,
            "CreatedAt" timestamp without time zone NOT NULL DEFAULT now(),
            "UpdatedAt" timestamp without time zone NULL,
            "Synced" boolean NOT NULL DEFAULT false,
            "Deleted" boolean NOT NULL DEFAULT false,
            "StoreId" uuid NOT NULL,
            "OnDate" timestamp without time zone NOT NULL,
            "OpeningBalance" numeric(18,2) NOT NULL DEFAULT 0,
            "CashDetailId" uuid NOT NULL DEFAULT '00000000-0000-0000-0000-000000000000',
            "CreatedBy" text NULL,
            CONSTRAINT "PK_DayBegins" PRIMARY KEY ("Id")
        );

        CREATE TABLE IF NOT EXISTS "DayEnds" (
            "Id" uuid NOT NULL,
            "CreatedAt" timestamp without time zone NOT NULL DEFAULT now(),
            "UpdatedAt" timestamp without time zone NULL,
            "Synced" boolean NOT NULL DEFAULT false,
            "Deleted" boolean NOT NULL DEFAULT false,
            "StoreId" uuid NOT NULL,
            "OnDate" timestamp without time zone NOT NULL,
            "ClosingBalance" numeric(18,2) NOT NULL DEFAULT 0,
            "CashDetailId" uuid NOT NULL DEFAULT '00000000-0000-0000-0000-000000000000',
            "CreatedBy" text NULL,
            CONSTRAINT "PK_DayEnds" PRIMARY KEY ("Id")
        );

        CREATE TABLE IF NOT EXISTS "PettyCashSheets" (
            "Id" uuid NOT NULL,
            "CreatedAt" timestamp without time zone NOT NULL DEFAULT now(),
            "UpdatedAt" timestamp without time zone NULL,
            "Synced" boolean NOT NULL DEFAULT false,
            "Deleted" boolean NOT NULL DEFAULT false,
            "StoreId" uuid NOT NULL,
            "OnDate" timestamp without time zone NOT NULL,
            "OpeningBalance" numeric(18,2) NOT NULL DEFAULT 0,
            "Sales" numeric(18,2) NOT NULL DEFAULT 0,
            "Receipts" numeric(18,2) NOT NULL DEFAULT 0,
            "DueReceipts" numeric(18,2) NOT NULL DEFAULT 0,
            "BankWithdrawal" numeric(18,2) NOT NULL DEFAULT 0,
            "Expenses" numeric(18,2) NOT NULL DEFAULT 0,
            "Payments" numeric(18,2) NOT NULL DEFAULT 0,
            "CustomerDue" numeric(18,2) NOT NULL DEFAULT 0,
            "BankDeposit" numeric(18,2) NOT NULL DEFAULT 0,
            "NonCashSale" numeric(18,2) NOT NULL DEFAULT 0,
            "CashInHand" numeric(18,2) NOT NULL DEFAULT 0,
            "CreatedBy" text NULL,
            CONSTRAINT "PK_PettyCashSheets" PRIMARY KEY ("Id")
        );

        CREATE UNIQUE INDEX IF NOT EXISTS "IX_DayBegins_StoreId_OnDate" ON "DayBegins" ("StoreId", "OnDate") WHERE "Deleted" = false;
        CREATE UNIQUE INDEX IF NOT EXISTS "IX_DayEnds_StoreId_OnDate" ON "DayEnds" ("StoreId", "OnDate") WHERE "Deleted" = false;
        CREATE INDEX IF NOT EXISTS "IX_CashDetails_StoreId_OnDate_CreatedBy" ON "CashDetails" ("StoreId", "OnDate", "CreatedBy");
        CREATE INDEX IF NOT EXISTS "IX_PettyCashSheets_StoreId_OnDate" ON "PettyCashSheets" ("StoreId", "OnDate");
        """, cancellationToken);

    logger.LogInformation("Store day opening/closing storage repair check completed.");
}


public static async Task RepairHrEmployeeMasterAndBenefitsAsync(GarmetixDbContext db, ILogger logger, CancellationToken cancellationToken = default)
{
    // Package 23A hotfix: production Docker volumes can be upgraded with AutoMigrate
    // disabled, or with EF migration history already marked as current while the
    // physical HR columns/table are still missing. /api/employees and HR benefits
    // query these members immediately, so keep this idempotent repair in startup
    // schema drift checks as well as the manual /api/database/repair endpoint.
    await db.Database.ExecuteSqlRawAsync("""
        ALTER TABLE "Employees" ADD COLUMN IF NOT EXISTS "EmployeeCode" character varying(40) NULL;
        ALTER TABLE "Employees" ADD COLUMN IF NOT EXISTS "FatherOrHusbandName" character varying(120) NULL;
        ALTER TABLE "Employees" ADD COLUMN IF NOT EXISTS "Department" character varying(80) NULL;
        ALTER TABLE "Employees" ADD COLUMN IF NOT EXISTS "Designation" character varying(80) NULL;
        ALTER TABLE "Employees" ADD COLUMN IF NOT EXISTS "SalaryType" character varying(30) NOT NULL DEFAULT 'Monthly';
        ALTER TABLE "Employees" ADD COLUMN IF NOT EXISTS "MonthlySalary" numeric(18,2) NOT NULL DEFAULT 0;
        ALTER TABLE "Employees" ADD COLUMN IF NOT EXISTS "DailyWage" numeric(18,2) NOT NULL DEFAULT 0;
        ALTER TABLE "Employees" ADD COLUMN IF NOT EXISTS "EmployeeStatus" character varying(30) NOT NULL DEFAULT 'Active';
        ALTER TABLE "Employees" ADD COLUMN IF NOT EXISTS "ExitReason" character varying(200) NULL;
        ALTER TABLE "Employees" ADD COLUMN IF NOT EXISTS "BloodGroup" character varying(40) NULL;
        ALTER TABLE "Employees" ADD COLUMN IF NOT EXISTS "PhotoDataUrl" text NULL;
        ALTER TABLE "Employees" ADD COLUMN IF NOT EXISTS "BankAccountName" character varying(120) NULL;
        ALTER TABLE "Employees" ADD COLUMN IF NOT EXISTS "BankAccountNumber" character varying(30) NULL;
        ALTER TABLE "Employees" ADD COLUMN IF NOT EXISTS "IFSC" character varying(20) NULL;
        ALTER TABLE "Employees" ADD COLUMN IF NOT EXISTS "ESINumber" character varying(30) NULL;
        ALTER TABLE "Employees" ADD COLUMN IF NOT EXISTS "PFNumber" character varying(30) NULL;
        ALTER TABLE "Employees" ADD COLUMN IF NOT EXISTS "EmergencyContact" character varying(120) NULL;

        UPDATE "Employees"
        SET "EmployeeStatus" = CASE WHEN "Working" THEN 'Active' ELSE 'Inactive' END
        WHERE "EmployeeStatus" IS NULL OR "EmployeeStatus" = '';

        CREATE TABLE IF NOT EXISTS "EmployeePayrollAdjustments" (
            "Id" uuid NOT NULL,
            "CreatedAt" timestamp without time zone NOT NULL DEFAULT now(),
            "UpdatedAt" timestamp without time zone NULL,
            "Synced" boolean NOT NULL DEFAULT false,
            "Deleted" boolean NOT NULL DEFAULT false,
            "CompanyId" uuid NOT NULL,
            "CreatedBy" text NULL,
            "StoreGroupId" uuid NOT NULL,
            "StoreId" uuid NOT NULL,
            "EmployeeId" uuid NOT NULL,
            "AdjustmentType" character varying(40) NOT NULL DEFAULT 'SalaryAdvance',
            "OnDate" timestamp without time zone NOT NULL DEFAULT now(),
            "SalaryMonth" integer NULL,
            "Amount" numeric(18,2) NOT NULL DEFAULT 0,
            "LeaveDays" numeric(18,2) NOT NULL DEFAULT 0,
            "RecoverFromSalary" boolean NOT NULL DEFAULT true,
            "RecoveredAmount" numeric(18,2) NOT NULL DEFAULT 0,
            "PfEmployee" numeric(18,2) NOT NULL DEFAULT 0,
            "PfEmployer" numeric(18,2) NOT NULL DEFAULT 0,
            "GratuityAmount" numeric(18,2) NOT NULL DEFAULT 0,
            "Status" character varying(30) NOT NULL DEFAULT 'Open',
            "Remarks" character varying(200) NULL,
            CONSTRAINT "PK_EmployeePayrollAdjustments" PRIMARY KEY ("Id")
        );

        ALTER TABLE "EmployeePayrollAdjustments" ADD COLUMN IF NOT EXISTS "CreatedAt" timestamp without time zone NOT NULL DEFAULT now();
        ALTER TABLE "EmployeePayrollAdjustments" ADD COLUMN IF NOT EXISTS "UpdatedAt" timestamp without time zone NULL;
        ALTER TABLE "EmployeePayrollAdjustments" ADD COLUMN IF NOT EXISTS "Synced" boolean NOT NULL DEFAULT false;
        ALTER TABLE "EmployeePayrollAdjustments" ADD COLUMN IF NOT EXISTS "Deleted" boolean NOT NULL DEFAULT false;
        ALTER TABLE "EmployeePayrollAdjustments" ADD COLUMN IF NOT EXISTS "CompanyId" uuid NOT NULL DEFAULT '00000000-0000-0000-0000-000000000000';
        ALTER TABLE "EmployeePayrollAdjustments" ADD COLUMN IF NOT EXISTS "CreatedBy" text NULL;
        ALTER TABLE "EmployeePayrollAdjustments" ADD COLUMN IF NOT EXISTS "StoreGroupId" uuid NOT NULL DEFAULT '00000000-0000-0000-0000-000000000000';
        ALTER TABLE "EmployeePayrollAdjustments" ADD COLUMN IF NOT EXISTS "StoreId" uuid NOT NULL DEFAULT '00000000-0000-0000-0000-000000000000';
        ALTER TABLE "EmployeePayrollAdjustments" ADD COLUMN IF NOT EXISTS "EmployeeId" uuid NOT NULL DEFAULT '00000000-0000-0000-0000-000000000000';
        ALTER TABLE "EmployeePayrollAdjustments" ADD COLUMN IF NOT EXISTS "AdjustmentType" character varying(40) NOT NULL DEFAULT 'SalaryAdvance';
        ALTER TABLE "EmployeePayrollAdjustments" ADD COLUMN IF NOT EXISTS "OnDate" timestamp without time zone NOT NULL DEFAULT now();
        ALTER TABLE "EmployeePayrollAdjustments" ADD COLUMN IF NOT EXISTS "SalaryMonth" integer NULL;
        ALTER TABLE "EmployeePayrollAdjustments" ADD COLUMN IF NOT EXISTS "Amount" numeric(18,2) NOT NULL DEFAULT 0;
        ALTER TABLE "EmployeePayrollAdjustments" ADD COLUMN IF NOT EXISTS "LeaveDays" numeric(18,2) NOT NULL DEFAULT 0;
        ALTER TABLE "EmployeePayrollAdjustments" ADD COLUMN IF NOT EXISTS "RecoverFromSalary" boolean NOT NULL DEFAULT true;
        ALTER TABLE "EmployeePayrollAdjustments" ADD COLUMN IF NOT EXISTS "RecoveredAmount" numeric(18,2) NOT NULL DEFAULT 0;
        ALTER TABLE "EmployeePayrollAdjustments" ADD COLUMN IF NOT EXISTS "PfEmployee" numeric(18,2) NOT NULL DEFAULT 0;
        ALTER TABLE "EmployeePayrollAdjustments" ADD COLUMN IF NOT EXISTS "PfEmployer" numeric(18,2) NOT NULL DEFAULT 0;
        ALTER TABLE "EmployeePayrollAdjustments" ADD COLUMN IF NOT EXISTS "GratuityAmount" numeric(18,2) NOT NULL DEFAULT 0;
        ALTER TABLE "EmployeePayrollAdjustments" ADD COLUMN IF NOT EXISTS "Status" character varying(30) NOT NULL DEFAULT 'Open';
        ALTER TABLE "EmployeePayrollAdjustments" ADD COLUMN IF NOT EXISTS "Remarks" character varying(200) NULL;

        CREATE INDEX IF NOT EXISTS "IX_Employees_CompanyId_StoreId_EmployeeCode" ON "Employees" ("CompanyId", "StoreId", "EmployeeCode");
        CREATE INDEX IF NOT EXISTS "IX_EmployeePayrollAdjustments_Company_Store_Employee_Type_Status" ON "EmployeePayrollAdjustments" ("CompanyId", "StoreId", "EmployeeId", "AdjustmentType", "Status");
        CREATE INDEX IF NOT EXISTS "IX_EmployeePayrollAdjustments_Company_OnDate" ON "EmployeePayrollAdjustments" ("CompanyId", "OnDate");
        """, cancellationToken);

    logger.LogInformation("HR employee master and benefits storage repair check completed.");
}


public static async Task RepairAttendanceCoreStorageAsync(GarmetixDbContext db, ILogger logger, CancellationToken cancellationToken = default)
{
    await db.Database.ExecuteSqlRawAsync("""
        CREATE EXTENSION IF NOT EXISTS pgcrypto;

        CREATE TABLE IF NOT EXISTS "AttendanceDevices" (
            "Id" uuid NOT NULL,
            "CreatedAt" timestamp without time zone NOT NULL DEFAULT now(),
            "UpdatedAt" timestamp without time zone NULL,
            "Synced" boolean NOT NULL DEFAULT false,
            "Deleted" boolean NOT NULL DEFAULT false,
            "CompanyId" uuid NOT NULL,
            "CreatedBy" text NULL,
            "StoreGroupId" uuid NOT NULL,
            "StoreId" uuid NOT NULL,
            "DeviceCode" character varying(40) NOT NULL DEFAULT '',
            "DeviceName" character varying(120) NOT NULL DEFAULT '',
            "DeviceType" character varying(40) NOT NULL DEFAULT 'WebKiosk',
            "DeviceTokenHash" character varying(120) NOT NULL DEFAULT '',
            "Status" character varying(40) NOT NULL DEFAULT 'Active',
            "AppVersion" character varying(80) NULL,
            "Notes" character varying(200) NULL,
            "RegisteredAtUtc" timestamp without time zone NOT NULL DEFAULT now(),
            "LastSeenAtUtc" timestamp without time zone NULL,
            "RevokedAtUtc" timestamp without time zone NULL,
            "RegisteredByUserId" uuid NULL,
            "RegisteredByUserName" character varying(120) NULL,
            CONSTRAINT "PK_AttendanceDevices" PRIMARY KEY ("Id")
        );

        CREATE TABLE IF NOT EXISTS "AttendancePunches" (
            "Id" uuid NOT NULL,
            "CreatedAt" timestamp without time zone NOT NULL DEFAULT now(),
            "UpdatedAt" timestamp without time zone NULL,
            "Synced" boolean NOT NULL DEFAULT false,
            "Deleted" boolean NOT NULL DEFAULT false,
            "CompanyId" uuid NOT NULL,
            "CreatedBy" text NULL,
            "StoreGroupId" uuid NOT NULL,
            "StoreId" uuid NOT NULL,
            "EmployeeId" uuid NOT NULL,
            "PunchType" character varying(20) NOT NULL DEFAULT 'CheckIn',
            "PunchTimeUtc" timestamp without time zone NOT NULL DEFAULT now(),
            "LocalPunchTime" timestamp without time zone NOT NULL DEFAULT now(),
            "Source" character varying(40) NOT NULL DEFAULT 'Manual',
            "DeviceId" uuid NULL,
            "DeviceCode" character varying(40) NULL,
            "VerificationStatus" character varying(40) NOT NULL DEFAULT 'ManualApproved',
            "PhotoProofPath" character varying(300) NULL,
            "ClientPunchId" character varying(120) NULL,
            "Latitude" numeric(12,8) NULL,
            "Longitude" numeric(12,8) NULL,
            "ConfidenceScore" numeric(7,4) NULL,
            "IsManual" boolean NOT NULL DEFAULT false,
            "IsSynced" boolean NOT NULL DEFAULT true,
            "DuplicateOfPunchId" uuid NULL,
            "Reason" character varying(300) NULL,
            "Remarks" character varying(300) NULL,
            CONSTRAINT "PK_AttendancePunches" PRIMARY KEY ("Id")
        );

        CREATE TABLE IF NOT EXISTS "AttendanceShifts" (
            "Id" uuid NOT NULL,
            "CreatedAt" timestamp without time zone NOT NULL DEFAULT now(),
            "UpdatedAt" timestamp without time zone NULL,
            "Synced" boolean NOT NULL DEFAULT false,
            "Deleted" boolean NOT NULL DEFAULT false,
            "CompanyId" uuid NOT NULL,
            "CreatedBy" text NULL,
            "StoreGroupId" uuid NOT NULL,
            "StoreId" uuid NOT NULL,
            "Name" character varying(120) NOT NULL DEFAULT 'Default Shift',
            "StartTimeMinutes" integer NOT NULL DEFAULT 600,
            "EndTimeMinutes" integer NOT NULL DEFAULT 1200,
            "GraceMinutes" integer NOT NULL DEFAULT 10,
            "LateAfterMinutes" integer NOT NULL DEFAULT 10,
            "HalfDayAfterMinutes" integer NOT NULL DEFAULT 750,
            "MinimumFullDayMinutes" integer NOT NULL DEFAULT 480,
            "MinimumHalfDayMinutes" integer NOT NULL DEFAULT 240,
            "OvertimeAfterMinutes" integer NOT NULL DEFAULT 540,
            "AutoCheckoutEnabled" boolean NOT NULL DEFAULT false,
            "AutoCheckoutTimeMinutes" integer NULL,
            "WeeklyOffDays" character varying(80) NOT NULL DEFAULT 'Sunday',
            "Active" boolean NOT NULL DEFAULT true,
            "AttendanceMode" character varying(40) NOT NULL DEFAULT 'SessionBased',
            "ShiftCategory" character varying(80) NULL,
            "HasBreak" boolean NOT NULL DEFAULT false,
            "RequiresBreakPunch" boolean NOT NULL DEFAULT false,
            "BreakStartMinutes" integer NULL,
            "BreakEndMinutes" integer NULL,
            "RequiredSessionsForFullDay" integer NOT NULL DEFAULT 1,
            "RequiredSessionsForHalfDay" integer NOT NULL DEFAULT 1,
            "CountBreakAsWork" boolean NOT NULL DEFAULT false,
            CONSTRAINT "PK_AttendanceShifts" PRIMARY KEY ("Id")
        );

        ALTER TABLE "AttendanceShifts" ADD COLUMN IF NOT EXISTS "AttendanceMode" character varying(40) NOT NULL DEFAULT 'SessionBased';
        ALTER TABLE "AttendanceShifts" ADD COLUMN IF NOT EXISTS "ShiftCategory" character varying(80) NULL;
        ALTER TABLE "AttendanceShifts" ADD COLUMN IF NOT EXISTS "HasBreak" boolean NOT NULL DEFAULT false;
        ALTER TABLE "AttendanceShifts" ADD COLUMN IF NOT EXISTS "RequiresBreakPunch" boolean NOT NULL DEFAULT false;
        ALTER TABLE "AttendanceShifts" ADD COLUMN IF NOT EXISTS "BreakStartMinutes" integer NULL;
        ALTER TABLE "AttendanceShifts" ADD COLUMN IF NOT EXISTS "BreakEndMinutes" integer NULL;
        ALTER TABLE "AttendanceShifts" ADD COLUMN IF NOT EXISTS "RequiredSessionsForFullDay" integer NOT NULL DEFAULT 1;
        ALTER TABLE "AttendanceShifts" ADD COLUMN IF NOT EXISTS "RequiredSessionsForHalfDay" integer NOT NULL DEFAULT 1;
        ALTER TABLE "AttendanceShifts" ADD COLUMN IF NOT EXISTS "CountBreakAsWork" boolean NOT NULL DEFAULT false;

        ALTER TABLE IF EXISTS "Attendance" ADD COLUMN IF NOT EXISTS "BreakOutTime" interval NULL;
        ALTER TABLE IF EXISTS "Attendance" ADD COLUMN IF NOT EXISTS "BreakInTime" interval NULL;

        CREATE TABLE IF NOT EXISTS "AttendancePolicies" (
            "Id" uuid NOT NULL,
            "CreatedAt" timestamp without time zone NOT NULL DEFAULT now(),
            "UpdatedAt" timestamp without time zone NULL,
            "Synced" boolean NOT NULL DEFAULT false,
            "Deleted" boolean NOT NULL DEFAULT false,
            "CompanyId" uuid NOT NULL,
            "CreatedBy" text NULL,
            "StoreGroupId" uuid NOT NULL,
            "StoreId" uuid NOT NULL,
            "Name" character varying(120) NOT NULL DEFAULT 'Default Attendance Policy',
            "GraceMinutes" integer NOT NULL DEFAULT 10,
            "LateAfterMinutes" integer NOT NULL DEFAULT 10,
            "HalfDayAfterMinutes" integer NOT NULL DEFAULT 750,
            "MinimumFullDayMinutes" integer NOT NULL DEFAULT 480,
            "MinimumHalfDayMinutes" integer NOT NULL DEFAULT 240,
            "OvertimeAfterMinutes" integer NOT NULL DEFAULT 540,
            "AutoCheckoutEnabled" boolean NOT NULL DEFAULT false,
            "AutoCheckoutAfterMinutes" integer NULL,
            "DuplicateWindowMinutes" integer NOT NULL DEFAULT 5,
            "Active" boolean NOT NULL DEFAULT true,
            CONSTRAINT "PK_AttendancePolicies" PRIMARY KEY ("Id")
        );

        CREATE TABLE IF NOT EXISTS "EmployeeAttendanceShiftRules" (
            "Id" uuid NOT NULL,
            "CreatedAt" timestamp without time zone NOT NULL DEFAULT now(),
            "UpdatedAt" timestamp without time zone NULL,
            "Synced" boolean NOT NULL DEFAULT false,
            "Deleted" boolean NOT NULL DEFAULT false,
            "CompanyId" uuid NOT NULL,
            "CreatedBy" text NULL,
            "StoreGroupId" uuid NOT NULL,
            "StoreId" uuid NOT NULL,
            "RuleType" character varying(40) NOT NULL DEFAULT 'StoreDefault',
            "MatchValue" character varying(120) NULL,
            "EmployeeId" uuid NULL,
            "AttendanceShiftId" uuid NOT NULL,
            "EffectiveFrom" timestamp without time zone NOT NULL DEFAULT CURRENT_DATE,
            "EffectiveTo" timestamp without time zone NULL,
            "Priority" integer NOT NULL DEFAULT 500,
            "Active" boolean NOT NULL DEFAULT true,
            "Notes" character varying(300) NULL,
            CONSTRAINT "PK_EmployeeAttendanceShiftRules" PRIMARY KEY ("Id")
        );

        CREATE TABLE IF NOT EXISTS "EmployeeBiometricEnrollments" (
            "Id" uuid NOT NULL,
            "CreatedAt" timestamp without time zone NOT NULL DEFAULT now(),
            "UpdatedAt" timestamp without time zone NULL,
            "Synced" boolean NOT NULL DEFAULT false,
            "Deleted" boolean NOT NULL DEFAULT false,
            "CompanyId" uuid NOT NULL,
            "CreatedBy" text NULL,
            "StoreGroupId" uuid NOT NULL,
            "StoreId" uuid NOT NULL,
            "EmployeeId" uuid NOT NULL,
            "ConsentGiven" boolean NOT NULL DEFAULT false,
            "ConsentAtUtc" timestamp without time zone NULL,
            "FacePhotoPath" character varying(300) NULL,
            "FaceTemplateRef" character varying(300) NULL,
            "FingerprintTemplateRef" character varying(300) NULL,
            "WebAuthnCredentialId" character varying(300) NULL,
            "EnrollmentStatus" character varying(40) NOT NULL DEFAULT 'NotEnrolled',
            "EnrolledAtUtc" timestamp without time zone NULL,
            "RevokedAtUtc" timestamp without time zone NULL,
            "RevokedReason" character varying(300) NULL,
            "Notes" character varying(300) NULL,
            CONSTRAINT "PK_EmployeeBiometricEnrollments" PRIMARY KEY ("Id")
        );

        CREATE TABLE IF NOT EXISTS "AttendanceRegularizationRequests" (
            "Id" uuid NOT NULL,
            "CreatedAt" timestamp without time zone NOT NULL DEFAULT now(),
            "UpdatedAt" timestamp without time zone NULL,
            "Synced" boolean NOT NULL DEFAULT false,
            "Deleted" boolean NOT NULL DEFAULT false,
            "CompanyId" uuid NOT NULL,
            "CreatedBy" text NULL,
            "StoreGroupId" uuid NOT NULL,
            "StoreId" uuid NOT NULL,
            "EmployeeId" uuid NOT NULL,
            "AttendancePunchId" uuid NULL,
            "RequestType" character varying(40) NOT NULL DEFAULT 'MissedPunch',
            "RequestedPunchType" character varying(20) NOT NULL DEFAULT 'CheckIn',
            "RequestedPunchTimeUtc" timestamp without time zone NULL,
            "RequestedLocalPunchTime" timestamp without time zone NULL,
            "Reason" character varying(300) NOT NULL DEFAULT '',
            "Status" character varying(40) NOT NULL DEFAULT 'Pending',
            "RequestedBy" character varying(120) NULL,
            "ApprovedBy" character varying(120) NULL,
            "ApprovedAtUtc" timestamp without time zone NULL,
            "RejectionReason" character varying(300) NULL,
            CONSTRAINT "PK_AttendanceRegularizationRequests" PRIMARY KEY ("Id")
        );

        CREATE TABLE IF NOT EXISTS "AttendanceApprovals" (
            "Id" uuid NOT NULL,
            "CreatedAt" timestamp without time zone NOT NULL DEFAULT now(),
            "UpdatedAt" timestamp without time zone NULL,
            "Synced" boolean NOT NULL DEFAULT false,
            "Deleted" boolean NOT NULL DEFAULT false,
            "CompanyId" uuid NOT NULL,
            "CreatedBy" text NULL,
            "StoreGroupId" uuid NOT NULL,
            "StoreId" uuid NOT NULL,
            "RequestId" uuid NOT NULL,
            "Approved" boolean NOT NULL DEFAULT false,
            "Decision" character varying(40) NOT NULL DEFAULT 'Approved',
            "Remarks" character varying(300) NULL,
            "ApprovedBy" character varying(120) NULL,
            "ApprovedAtUtc" timestamp without time zone NOT NULL DEFAULT now(),
            CONSTRAINT "PK_AttendanceApprovals" PRIMARY KEY ("Id")
        );

        CREATE TABLE IF NOT EXISTS "AttendanceMonthlySummaries" (
            "Id" uuid NOT NULL,
            "CreatedAt" timestamp without time zone NOT NULL DEFAULT now(),
            "UpdatedAt" timestamp without time zone NULL,
            "Synced" boolean NOT NULL DEFAULT false,
            "Deleted" boolean NOT NULL DEFAULT false,
            "CompanyId" uuid NOT NULL,
            "CreatedBy" text NULL,
            "StoreGroupId" uuid NOT NULL,
            "StoreId" uuid NOT NULL,
            "EmployeeId" uuid NOT NULL,
            "Year" integer NOT NULL,
            "Month" integer NOT NULL,
            "PresentDays" numeric(18,2) NOT NULL DEFAULT 0,
            "AbsentDays" numeric(18,2) NOT NULL DEFAULT 0,
            "LateDays" numeric(18,2) NOT NULL DEFAULT 0,
            "HalfDays" numeric(18,2) NOT NULL DEFAULT 0,
            "LeaveDays" numeric(18,2) NOT NULL DEFAULT 0,
            "WorkingMinutes" integer NOT NULL DEFAULT 0,
            "OvertimeMinutes" integer NOT NULL DEFAULT 0,
            "Locked" boolean NOT NULL DEFAULT false,
            "LockedAtUtc" timestamp without time zone NULL,
            "LockedBy" character varying(120) NULL,
            "SummaryJson" text NULL,
            CONSTRAINT "PK_AttendanceMonthlySummaries" PRIMARY KEY ("Id")
        );

        CREATE INDEX IF NOT EXISTS "IX_AttendanceDevices_CompanyId_StoreId_DeviceCode" ON "AttendanceDevices" ("CompanyId", "StoreId", "DeviceCode");
        CREATE INDEX IF NOT EXISTS "IX_AttendanceDevices_CompanyId_StoreId_Status" ON "AttendanceDevices" ("CompanyId", "StoreId", "Status");
        CREATE INDEX IF NOT EXISTS "IX_AttendancePunches_CompanyId_StoreId_EmployeeId_LocalPunchTime" ON "AttendancePunches" ("CompanyId", "StoreId", "EmployeeId", "LocalPunchTime");
        CREATE INDEX IF NOT EXISTS "IX_AttendancePunches_CompanyId_StoreId_DeviceId_PunchTimeUtc" ON "AttendancePunches" ("CompanyId", "StoreId", "DeviceId", "PunchTimeUtc");
        CREATE INDEX IF NOT EXISTS "IX_AttendancePunches_CompanyId_ClientPunchId" ON "AttendancePunches" ("CompanyId", "ClientPunchId");
        CREATE INDEX IF NOT EXISTS "IX_AttendanceShifts_CompanyId_StoreId_Active" ON "AttendanceShifts" ("CompanyId", "StoreId", "Active");
        CREATE INDEX IF NOT EXISTS "IX_AttendancePolicies_CompanyId_StoreId_Active" ON "AttendancePolicies" ("CompanyId", "StoreId", "Active");
        CREATE INDEX IF NOT EXISTS "IX_EmployeeAttendanceShiftRules_CompanyId_StoreId_Active_Priority" ON "EmployeeAttendanceShiftRules" ("CompanyId", "StoreId", "Active", "Priority");
        CREATE INDEX IF NOT EXISTS "IX_EmployeeAttendanceShiftRules_CompanyId_StoreId_EmployeeId_Active" ON "EmployeeAttendanceShiftRules" ("CompanyId", "StoreId", "EmployeeId", "Active");

        INSERT INTO "AttendanceShifts" ("Id", "CreatedAt", "UpdatedAt", "Synced", "Deleted", "CompanyId", "CreatedBy", "StoreGroupId", "StoreId", "Name", "StartTimeMinutes", "EndTimeMinutes", "GraceMinutes", "LateAfterMinutes", "HalfDayAfterMinutes", "MinimumFullDayMinutes", "MinimumHalfDayMinutes", "OvertimeAfterMinutes", "AutoCheckoutEnabled", "AutoCheckoutTimeMinutes", "WeeklyOffDays", "Active", "AttendanceMode", "ShiftCategory", "HasBreak", "RequiresBreakPunch", "BreakStartMinutes", "BreakEndMinutes", "RequiredSessionsForFullDay", "RequiredSessionsForHalfDay", "CountBreakAsWork")
        SELECT gen_random_uuid(), now(), NULL, false, false, s."CompanyId", 'system-shift-seed', s."StoreGroupId", s."Id", 'Default Store Split Shift 09:00-21:00', 540, 1260, 10, 10, 780, 0, 0, 1260, false, NULL, 'Sunday', true, 'SessionBased', 'StoreDefault', true, true, 780, 870, 2, 1, false
        FROM "Stores" s
        WHERE NOT EXISTS (SELECT 1 FROM "AttendanceShifts" a WHERE a."StoreId" = s."Id" AND a."Name" = 'Default Store Split Shift 09:00-21:00' AND NOT a."Deleted");

        INSERT INTO "AttendanceShifts" ("Id", "CreatedAt", "UpdatedAt", "Synced", "Deleted", "CompanyId", "CreatedBy", "StoreGroupId", "StoreId", "Name", "StartTimeMinutes", "EndTimeMinutes", "GraceMinutes", "LateAfterMinutes", "HalfDayAfterMinutes", "MinimumFullDayMinutes", "MinimumHalfDayMinutes", "OvertimeAfterMinutes", "AutoCheckoutEnabled", "AutoCheckoutTimeMinutes", "WeeklyOffDays", "Active", "AttendanceMode", "ShiftCategory", "HasBreak", "RequiresBreakPunch", "BreakStartMinutes", "BreakEndMinutes", "RequiredSessionsForFullDay", "RequiredSessionsForHalfDay", "CountBreakAsWork")
        SELECT gen_random_uuid(), now(), NULL, false, false, s."CompanyId", 'system-shift-seed', s."StoreGroupId", s."Id", 'Female Staff Shift 10:00-20:00', 600, 1200, 10, 10, 780, 0, 0, 1200, false, NULL, 'Sunday', true, 'SessionBased', 'Female', false, false, NULL, NULL, 1, 1, false
        FROM "Stores" s
        WHERE NOT EXISTS (SELECT 1 FROM "AttendanceShifts" a WHERE a."StoreId" = s."Id" AND a."Name" = 'Female Staff Shift 10:00-20:00' AND NOT a."Deleted");

        INSERT INTO "AttendanceShifts" ("Id", "CreatedAt", "UpdatedAt", "Synced", "Deleted", "CompanyId", "CreatedBy", "StoreGroupId", "StoreId", "Name", "StartTimeMinutes", "EndTimeMinutes", "GraceMinutes", "LateAfterMinutes", "HalfDayAfterMinutes", "MinimumFullDayMinutes", "MinimumHalfDayMinutes", "OvertimeAfterMinutes", "AutoCheckoutEnabled", "AutoCheckoutTimeMinutes", "WeeklyOffDays", "Active", "AttendanceMode", "ShiftCategory", "HasBreak", "RequiresBreakPunch", "BreakStartMinutes", "BreakEndMinutes", "RequiredSessionsForFullDay", "RequiredSessionsForHalfDay", "CountBreakAsWork")
        SELECT gen_random_uuid(), now(), NULL, false, false, s."CompanyId", 'system-shift-seed', s."StoreGroupId", s."Id", 'Accounts Shift 10:00-19:00', 600, 1140, 10, 10, 780, 0, 0, 1140, false, NULL, 'Sunday', true, 'SessionBased', 'Accounts', false, false, NULL, NULL, 1, 1, false
        FROM "Stores" s
        WHERE NOT EXISTS (SELECT 1 FROM "AttendanceShifts" a WHERE a."StoreId" = s."Id" AND a."Name" = 'Accounts Shift 10:00-19:00' AND NOT a."Deleted");

        INSERT INTO "AttendanceShifts" ("Id", "CreatedAt", "UpdatedAt", "Synced", "Deleted", "CompanyId", "CreatedBy", "StoreGroupId", "StoreId", "Name", "StartTimeMinutes", "EndTimeMinutes", "GraceMinutes", "LateAfterMinutes", "HalfDayAfterMinutes", "MinimumFullDayMinutes", "MinimumHalfDayMinutes", "OvertimeAfterMinutes", "AutoCheckoutEnabled", "AutoCheckoutTimeMinutes", "WeeklyOffDays", "Active", "AttendanceMode", "ShiftCategory", "HasBreak", "RequiresBreakPunch", "BreakStartMinutes", "BreakEndMinutes", "RequiredSessionsForFullDay", "RequiredSessionsForHalfDay", "CountBreakAsWork")
        SELECT gen_random_uuid(), now(), NULL, false, false, s."CompanyId", 'system-shift-seed', s."StoreGroupId", s."Id", 'Housekeeping Morning Shift', 420, 900, 10, 10, 720, 0, 0, 900, false, NULL, 'Sunday', true, 'SessionBased', 'HouseKeeping', false, false, NULL, NULL, 1, 1, false
        FROM "Stores" s
        WHERE NOT EXISTS (SELECT 1 FROM "AttendanceShifts" a WHERE a."StoreId" = s."Id" AND a."Name" = 'Housekeeping Morning Shift' AND NOT a."Deleted");

        INSERT INTO "AttendanceShifts" ("Id", "CreatedAt", "UpdatedAt", "Synced", "Deleted", "CompanyId", "CreatedBy", "StoreGroupId", "StoreId", "Name", "StartTimeMinutes", "EndTimeMinutes", "GraceMinutes", "LateAfterMinutes", "HalfDayAfterMinutes", "MinimumFullDayMinutes", "MinimumHalfDayMinutes", "OvertimeAfterMinutes", "AutoCheckoutEnabled", "AutoCheckoutTimeMinutes", "WeeklyOffDays", "Active", "AttendanceMode", "ShiftCategory", "HasBreak", "RequiresBreakPunch", "BreakStartMinutes", "BreakEndMinutes", "RequiredSessionsForFullDay", "RequiredSessionsForHalfDay", "CountBreakAsWork")
        SELECT gen_random_uuid(), now(), NULL, false, false, s."CompanyId", 'system-shift-seed', s."StoreGroupId", s."Id", 'Housekeeping Evening Shift', 840, 1320, 10, 10, 1020, 0, 0, 1320, false, NULL, 'Sunday', true, 'SessionBased', 'HouseKeeping', false, false, NULL, NULL, 1, 1, false
        FROM "Stores" s
        WHERE NOT EXISTS (SELECT 1 FROM "AttendanceShifts" a WHERE a."StoreId" = s."Id" AND a."Name" = 'Housekeeping Evening Shift' AND NOT a."Deleted");


        INSERT INTO "AttendanceShifts" ("Id", "CreatedAt", "UpdatedAt", "Synced", "Deleted", "CompanyId", "CreatedBy", "StoreGroupId", "StoreId", "Name", "StartTimeMinutes", "EndTimeMinutes", "GraceMinutes", "LateAfterMinutes", "HalfDayAfterMinutes", "MinimumFullDayMinutes", "MinimumHalfDayMinutes", "OvertimeAfterMinutes", "AutoCheckoutEnabled", "AutoCheckoutTimeMinutes", "WeeklyOffDays", "Active", "AttendanceMode", "ShiftCategory", "HasBreak", "RequiresBreakPunch", "BreakStartMinutes", "BreakEndMinutes", "RequiredSessionsForFullDay", "RequiredSessionsForHalfDay", "CountBreakAsWork")
        SELECT gen_random_uuid(), now(), NULL, false, false, s."CompanyId", 'system-shift-seed', s."StoreGroupId", s."Id", 'Housekeeping Double Shift', 420, 1320, 10, 10, 900, 0, 0, 1320, false, NULL, 'Sunday', true, 'SessionBased', 'HouseKeeping', true, true, 900, 1080, 2, 1, false
        FROM "Stores" s
        WHERE NOT EXISTS (SELECT 1 FROM "AttendanceShifts" a WHERE a."StoreId" = s."Id" AND a."Name" = 'Housekeeping Double Shift' AND NOT a."Deleted");

        INSERT INTO "EmployeeAttendanceShiftRules" ("Id", "CreatedAt", "UpdatedAt", "Synced", "Deleted", "CompanyId", "CreatedBy", "StoreGroupId", "StoreId", "RuleType", "MatchValue", "EmployeeId", "AttendanceShiftId", "EffectiveFrom", "EffectiveTo", "Priority", "Active", "Notes")
        SELECT gen_random_uuid(), now(), NULL, false, false, sh."CompanyId", 'system-shift-seed', sh."StoreGroupId", sh."StoreId", 'StoreDefault', NULL, NULL, sh."Id", CURRENT_DATE, NULL, 900, true, 'Default male/store timing 09:00-21:00 with 1.5 hour lunch break.'
        FROM "AttendanceShifts" sh
        WHERE sh."Name" = 'Default Store Split Shift 09:00-21:00' AND NOT sh."Deleted"
          AND NOT EXISTS (SELECT 1 FROM "EmployeeAttendanceShiftRules" r WHERE r."StoreId" = sh."StoreId" AND r."RuleType" = 'StoreDefault' AND r."AttendanceShiftId" = sh."Id" AND NOT r."Deleted");

        INSERT INTO "EmployeeAttendanceShiftRules" ("Id", "CreatedAt", "UpdatedAt", "Synced", "Deleted", "CompanyId", "CreatedBy", "StoreGroupId", "StoreId", "RuleType", "MatchValue", "EmployeeId", "AttendanceShiftId", "EffectiveFrom", "EffectiveTo", "Priority", "Active", "Notes")
        SELECT gen_random_uuid(), now(), NULL, false, false, sh."CompanyId", 'system-shift-seed', sh."StoreGroupId", sh."StoreId", 'Gender', 'Female', NULL, sh."Id", CURRENT_DATE, NULL, 300, true, 'Female employee default timing 10:00-20:00.'
        FROM "AttendanceShifts" sh
        WHERE sh."Name" = 'Female Staff Shift 10:00-20:00' AND NOT sh."Deleted"
          AND NOT EXISTS (SELECT 1 FROM "EmployeeAttendanceShiftRules" r WHERE r."StoreId" = sh."StoreId" AND r."RuleType" = 'Gender' AND r."MatchValue" = 'Female' AND NOT r."Deleted");

        INSERT INTO "EmployeeAttendanceShiftRules" ("Id", "CreatedAt", "UpdatedAt", "Synced", "Deleted", "CompanyId", "CreatedBy", "StoreGroupId", "StoreId", "RuleType", "MatchValue", "EmployeeId", "AttendanceShiftId", "EffectiveFrom", "EffectiveTo", "Priority", "Active", "Notes")
        SELECT gen_random_uuid(), now(), NULL, false, false, sh."CompanyId", 'system-shift-seed', sh."StoreGroupId", sh."StoreId", 'Category', 'HouseKeeping', NULL, sh."Id", CURRENT_DATE, NULL, 200, true, 'Housekeeping double-shift rule: one completed shift/session = half day, both sessions = full day.'
        FROM "AttendanceShifts" sh
        WHERE sh."Name" = 'Housekeeping Double Shift' AND NOT sh."Deleted"
          AND NOT EXISTS (SELECT 1 FROM "EmployeeAttendanceShiftRules" r WHERE r."StoreId" = sh."StoreId" AND r."RuleType" = 'Category' AND r."MatchValue" = 'HouseKeeping' AND NOT r."Deleted");
        CREATE INDEX IF NOT EXISTS "IX_EmployeeBiometricEnrollments_CompanyId_StoreId_EmployeeId" ON "EmployeeBiometricEnrollments" ("CompanyId", "StoreId", "EmployeeId");
        CREATE INDEX IF NOT EXISTS "IX_AttendanceRegularizationRequests_CompanyId_StoreId_EmployeeId_Status" ON "AttendanceRegularizationRequests" ("CompanyId", "StoreId", "EmployeeId", "Status");
        CREATE INDEX IF NOT EXISTS "IX_AttendanceApprovals_CompanyId_StoreId_RequestId" ON "AttendanceApprovals" ("CompanyId", "StoreId", "RequestId");
        CREATE INDEX IF NOT EXISTS "IX_AttendanceMonthlySummaries_CompanyId_StoreId_EmployeeId_Year_Month" ON "AttendanceMonthlySummaries" ("CompanyId", "StoreId", "EmployeeId", "Year", "Month");

        CREATE TABLE IF NOT EXISTS "AttendancePhotoProofs" (
            "Id" uuid NOT NULL,
            "CreatedAt" timestamp without time zone NOT NULL DEFAULT now(),
            "UpdatedAt" timestamp without time zone NULL,
            "Synced" boolean NOT NULL DEFAULT false,
            "Deleted" boolean NOT NULL DEFAULT false,
            "CompanyId" uuid NOT NULL,
            "CreatedBy" text NULL,
            "StoreGroupId" uuid NOT NULL,
            "StoreId" uuid NOT NULL,
            "EmployeeId" uuid NOT NULL,
            "DeviceId" uuid NULL,
            "DeviceCode" character varying(40) NULL,
            "ClientPunchId" character varying(120) NULL,
            "ProofPath" character varying(500) NOT NULL DEFAULT '',
            "ContentType" character varying(80) NOT NULL DEFAULT 'image/jpeg',
            "SizeBytes" bigint NOT NULL DEFAULT 0,
            "CapturedAtUtc" timestamp without time zone NOT NULL DEFAULT now(),
            "UploadedAtUtc" timestamp without time zone NOT NULL DEFAULT now(),
            "RetentionUntilUtc" timestamp without time zone NULL,
            "VerificationStatus" character varying(40) NOT NULL DEFAULT 'PhotoProofOnly',
            "ReviewStatus" character varying(40) NOT NULL DEFAULT 'PendingReview',
            "ReviewedAtUtc" timestamp without time zone NULL,
            "ReviewedBy" character varying(120) NULL,
            "ReviewRemarks" character varying(300) NULL,
            "ReviewReason" character varying(80) NULL,
            "RegularizationRequestId" uuid NULL,
            "Remarks" character varying(300) NULL,
            CONSTRAINT "PK_AttendancePhotoProofs" PRIMARY KEY ("Id")
        );

        CREATE TABLE IF NOT EXISTS "AttendanceKioskSyncBatches" (
            "Id" uuid NOT NULL,
            "CreatedAt" timestamp without time zone NOT NULL DEFAULT now(),
            "UpdatedAt" timestamp without time zone NULL,
            "Synced" boolean NOT NULL DEFAULT false,
            "Deleted" boolean NOT NULL DEFAULT false,
            "CompanyId" uuid NOT NULL,
            "CreatedBy" text NULL,
            "StoreGroupId" uuid NOT NULL,
            "StoreId" uuid NOT NULL,
            "DeviceId" uuid NOT NULL,
            "DeviceCode" character varying(40) NOT NULL DEFAULT '',
            "BatchClientId" character varying(120) NULL,
            "TotalCount" integer NOT NULL DEFAULT 0,
            "AcceptedCount" integer NOT NULL DEFAULT 0,
            "DuplicateCount" integer NOT NULL DEFAULT 0,
            "FailedCount" integer NOT NULL DEFAULT 0,
            "Status" character varying(40) NOT NULL DEFAULT 'Received',
            "ReceivedAtUtc" timestamp without time zone NOT NULL DEFAULT now(),
            "CompletedAtUtc" timestamp without time zone NULL,
            "ResultJson" text NULL,
            CONSTRAINT "PK_AttendanceKioskSyncBatches" PRIMARY KEY ("Id")
        );



        CREATE TABLE IF NOT EXISTS "AttendancePayrollReviews" (
            "Id" uuid NOT NULL,
            "CreatedAt" timestamp without time zone NOT NULL DEFAULT now(),
            "UpdatedAt" timestamp without time zone NULL,
            "Synced" boolean NOT NULL DEFAULT false,
            "Deleted" boolean NOT NULL DEFAULT false,
            "CompanyId" uuid NOT NULL,
            "CreatedBy" text NULL,
            "StoreGroupId" uuid NOT NULL,
            "StoreId" uuid NOT NULL,
            "EmployeeId" uuid NOT NULL,
            "Year" integer NOT NULL,
            "Month" integer NOT NULL,
            "PresentDays" numeric(18,2) NOT NULL DEFAULT 0,
            "AbsentDays" numeric(18,2) NOT NULL DEFAULT 0,
            "LateDays" numeric(18,2) NOT NULL DEFAULT 0,
            "HalfDays" numeric(18,2) NOT NULL DEFAULT 0,
            "LeaveDays" numeric(18,2) NOT NULL DEFAULT 0,
            "PayableDays" numeric(18,2) NOT NULL DEFAULT 0,
            "DeductionDays" numeric(18,2) NOT NULL DEFAULT 0,
            "WorkingMinutes" integer NOT NULL DEFAULT 0,
            "OvertimeMinutes" integer NOT NULL DEFAULT 0,
            "EstimatedDailyRate" numeric(18,2) NOT NULL DEFAULT 0,
            "EstimatedGrossPay" numeric(18,2) NOT NULL DEFAULT 0,
            "ReviewStatus" character varying(40) NOT NULL DEFAULT 'Draft',
            "PayrollActionStatus" character varying(40) NOT NULL DEFAULT 'NotPosted',
            "Locked" boolean NOT NULL DEFAULT false,
            "LockedAtUtc" timestamp without time zone NULL,
            "ReviewedBy" character varying(120) NULL,
            "ReviewedAtUtc" timestamp without time zone NULL,
            "Notes" character varying(300) NULL,
            "SourceSummaryJson" text NULL,
            CONSTRAINT "PK_AttendancePayrollReviews" PRIMARY KEY ("Id")
        );
        ALTER TABLE "AttendancePayrollReviews" ADD COLUMN IF NOT EXISTS "PayableDays" numeric(18,2) NOT NULL DEFAULT 0;
        ALTER TABLE "AttendancePayrollReviews" ADD COLUMN IF NOT EXISTS "DeductionDays" numeric(18,2) NOT NULL DEFAULT 0;
        ALTER TABLE "AttendancePayrollReviews" ADD COLUMN IF NOT EXISTS "EstimatedDailyRate" numeric(18,2) NOT NULL DEFAULT 0;
        ALTER TABLE "AttendancePayrollReviews" ADD COLUMN IF NOT EXISTS "EstimatedGrossPay" numeric(18,2) NOT NULL DEFAULT 0;
        ALTER TABLE "AttendancePayrollReviews" ADD COLUMN IF NOT EXISTS "ReviewStatus" character varying(40) NOT NULL DEFAULT 'Draft';
        ALTER TABLE "AttendancePayrollReviews" ADD COLUMN IF NOT EXISTS "PayrollActionStatus" character varying(40) NOT NULL DEFAULT 'NotPosted';
        ALTER TABLE "AttendancePayrollReviews" ADD COLUMN IF NOT EXISTS "Locked" boolean NOT NULL DEFAULT false;
        ALTER TABLE "AttendancePayrollReviews" ADD COLUMN IF NOT EXISTS "LockedAtUtc" timestamp without time zone NULL;
        ALTER TABLE "AttendancePayrollReviews" ADD COLUMN IF NOT EXISTS "ReviewedBy" character varying(120) NULL;
        ALTER TABLE "AttendancePayrollReviews" ADD COLUMN IF NOT EXISTS "ReviewedAtUtc" timestamp without time zone NULL;
        ALTER TABLE "AttendancePayrollReviews" ADD COLUMN IF NOT EXISTS "Notes" character varying(300) NULL;
        ALTER TABLE "AttendancePayrollReviews" ADD COLUMN IF NOT EXISTS "SourceSummaryJson" text NULL;
        CREATE INDEX IF NOT EXISTS "IX_AttendancePayrollReviews_CompanyId_StoreId_EmployeeId_Year_Month" ON "AttendancePayrollReviews" ("CompanyId", "StoreId", "EmployeeId", "Year", "Month");
        CREATE INDEX IF NOT EXISTS "IX_AttendancePayrollReviews_CompanyId_StoreId_Year_Month_ReviewStatus" ON "AttendancePayrollReviews" ("CompanyId", "StoreId", "Year", "Month", "ReviewStatus");


        CREATE TABLE IF NOT EXISTS "AttendanceSalarySlipDrafts" (
            "Id" uuid NOT NULL,
            "CreatedAt" timestamp without time zone NOT NULL DEFAULT now(),
            "UpdatedAt" timestamp without time zone NULL,
            "Synced" boolean NOT NULL DEFAULT false,
            "Deleted" boolean NOT NULL DEFAULT false,
            "CompanyId" uuid NOT NULL,
            "CreatedBy" text NULL,
            "StoreGroupId" uuid NOT NULL,
            "StoreId" uuid NOT NULL,
            "EmployeeId" uuid NOT NULL,
            "PayrollReviewId" uuid NULL,
            "Year" integer NOT NULL,
            "Month" integer NOT NULL,
            "PresentDays" numeric(18,2) NOT NULL DEFAULT 0,
            "AbsentDays" numeric(18,2) NOT NULL DEFAULT 0,
            "LateDays" numeric(18,2) NOT NULL DEFAULT 0,
            "HalfDays" numeric(18,2) NOT NULL DEFAULT 0,
            "LeaveDays" numeric(18,2) NOT NULL DEFAULT 0,
            "PayableDays" numeric(18,2) NOT NULL DEFAULT 0,
            "DeductionDays" numeric(18,2) NOT NULL DEFAULT 0,
            "WorkingMinutes" integer NOT NULL DEFAULT 0,
            "OvertimeMinutes" integer NOT NULL DEFAULT 0,
            "MonthlySalary" numeric(18,2) NOT NULL DEFAULT 0,
            "DailyRate" numeric(18,2) NOT NULL DEFAULT 0,
            "AttendanceGrossPreview" numeric(18,2) NOT NULL DEFAULT 0,
            "AttendanceDeductionPreview" numeric(18,2) NOT NULL DEFAULT 0,
            "BonusPreview" numeric(18,2) NOT NULL DEFAULT 0,
            "LeaveEncashmentPreview" numeric(18,2) NOT NULL DEFAULT 0,
            "SalaryAdvanceRecoveryPreview" numeric(18,2) NOT NULL DEFAULT 0,
            "PfEmployeePreview" numeric(18,2) NOT NULL DEFAULT 0,
            "GratuityPreview" numeric(18,2) NOT NULL DEFAULT 0,
            "OtherDeductionPreview" numeric(18,2) NOT NULL DEFAULT 0,
            "NetPayPreview" numeric(18,2) NOT NULL DEFAULT 0,
            "DraftStatus" character varying(40) NOT NULL DEFAULT 'Draft',
            "PayrollPostStatus" character varying(40) NOT NULL DEFAULT 'PreviewOnly',
            "GeneratedSalaryPaySlipId" uuid NULL,
            "GeneratedAtUtc" timestamp without time zone NULL,
            "GeneratedBy" character varying(120) NULL,
            "GeneratedSalaryPaymentId" uuid NULL,
            "SalaryPaidAtUtc" timestamp without time zone NULL,
            "SalaryPaidBy" character varying(120) NULL,
            "PaymentPostStatus" character varying(40) NOT NULL DEFAULT 'NotPaid',
            "PreparedAtUtc" timestamp without time zone NULL,
            "PreparedBy" character varying(120) NULL,
            "MarkedReadyAtUtc" timestamp without time zone NULL,
            "MarkedReadyBy" character varying(120) NULL,
            "Notes" character varying(300) NULL,
            "SourceJson" text NULL,
            CONSTRAINT "PK_AttendanceSalarySlipDrafts" PRIMARY KEY ("Id")
        );
        ALTER TABLE "AttendanceSalarySlipDrafts" ADD COLUMN IF NOT EXISTS "PayrollReviewId" uuid NULL;
        ALTER TABLE "AttendanceSalarySlipDrafts" ADD COLUMN IF NOT EXISTS "MonthlySalary" numeric(18,2) NOT NULL DEFAULT 0;
        ALTER TABLE "AttendanceSalarySlipDrafts" ADD COLUMN IF NOT EXISTS "DailyRate" numeric(18,2) NOT NULL DEFAULT 0;
        ALTER TABLE "AttendanceSalarySlipDrafts" ADD COLUMN IF NOT EXISTS "AttendanceGrossPreview" numeric(18,2) NOT NULL DEFAULT 0;
        ALTER TABLE "AttendanceSalarySlipDrafts" ADD COLUMN IF NOT EXISTS "AttendanceDeductionPreview" numeric(18,2) NOT NULL DEFAULT 0;
        ALTER TABLE "AttendanceSalarySlipDrafts" ADD COLUMN IF NOT EXISTS "BonusPreview" numeric(18,2) NOT NULL DEFAULT 0;
        ALTER TABLE "AttendanceSalarySlipDrafts" ADD COLUMN IF NOT EXISTS "LeaveEncashmentPreview" numeric(18,2) NOT NULL DEFAULT 0;
        ALTER TABLE "AttendanceSalarySlipDrafts" ADD COLUMN IF NOT EXISTS "SalaryAdvanceRecoveryPreview" numeric(18,2) NOT NULL DEFAULT 0;
        ALTER TABLE "AttendanceSalarySlipDrafts" ADD COLUMN IF NOT EXISTS "PfEmployeePreview" numeric(18,2) NOT NULL DEFAULT 0;
        ALTER TABLE "AttendanceSalarySlipDrafts" ADD COLUMN IF NOT EXISTS "GratuityPreview" numeric(18,2) NOT NULL DEFAULT 0;
        ALTER TABLE "AttendanceSalarySlipDrafts" ADD COLUMN IF NOT EXISTS "OtherDeductionPreview" numeric(18,2) NOT NULL DEFAULT 0;
        ALTER TABLE "AttendanceSalarySlipDrafts" ADD COLUMN IF NOT EXISTS "NetPayPreview" numeric(18,2) NOT NULL DEFAULT 0;
        ALTER TABLE "AttendanceSalarySlipDrafts" ADD COLUMN IF NOT EXISTS "DraftStatus" character varying(40) NOT NULL DEFAULT 'Draft';
        ALTER TABLE "AttendanceSalarySlipDrafts" ADD COLUMN IF NOT EXISTS "PayrollPostStatus" character varying(40) NOT NULL DEFAULT 'PreviewOnly';
        ALTER TABLE "AttendanceSalarySlipDrafts" ADD COLUMN IF NOT EXISTS "GeneratedSalaryPaySlipId" uuid NULL;
        ALTER TABLE "AttendanceSalarySlipDrafts" ADD COLUMN IF NOT EXISTS "GeneratedAtUtc" timestamp without time zone NULL;
        ALTER TABLE "AttendanceSalarySlipDrafts" ADD COLUMN IF NOT EXISTS "GeneratedBy" character varying(120) NULL;
        ALTER TABLE "AttendanceSalarySlipDrafts" ADD COLUMN IF NOT EXISTS "GeneratedSalaryPaymentId" uuid NULL;
        ALTER TABLE "AttendanceSalarySlipDrafts" ADD COLUMN IF NOT EXISTS "SalaryPaidAtUtc" timestamp without time zone NULL;
        ALTER TABLE "AttendanceSalarySlipDrafts" ADD COLUMN IF NOT EXISTS "SalaryPaidBy" character varying(120) NULL;
        ALTER TABLE "AttendanceSalarySlipDrafts" ADD COLUMN IF NOT EXISTS "PaymentPostStatus" character varying(40) NOT NULL DEFAULT 'NotPaid';
        ALTER TABLE "AttendanceSalarySlipDrafts" ADD COLUMN IF NOT EXISTS "PreparedAtUtc" timestamp without time zone NULL;
        ALTER TABLE "AttendanceSalarySlipDrafts" ADD COLUMN IF NOT EXISTS "PreparedBy" character varying(120) NULL;
        ALTER TABLE "AttendanceSalarySlipDrafts" ADD COLUMN IF NOT EXISTS "MarkedReadyAtUtc" timestamp without time zone NULL;
        ALTER TABLE "AttendanceSalarySlipDrafts" ADD COLUMN IF NOT EXISTS "MarkedReadyBy" character varying(120) NULL;
        ALTER TABLE "AttendanceSalarySlipDrafts" ADD COLUMN IF NOT EXISTS "Notes" character varying(300) NULL;
        ALTER TABLE "AttendanceSalarySlipDrafts" ADD COLUMN IF NOT EXISTS "SourceJson" text NULL;
        CREATE INDEX IF NOT EXISTS "IX_AttendanceSalarySlipDrafts_CompanyId_StoreId_EmployeeId_Year_Month" ON "AttendanceSalarySlipDrafts" ("CompanyId", "StoreId", "EmployeeId", "Year", "Month");
        CREATE INDEX IF NOT EXISTS "IX_AttendanceSalarySlipDrafts_CompanyId_StoreId_Year_Month_DraftStatus" ON "AttendanceSalarySlipDrafts" ("CompanyId", "StoreId", "Year", "Month", "DraftStatus");
        CREATE INDEX IF NOT EXISTS "IX_AttendanceSalarySlipDrafts_CompanyId_StoreId_Year_Month_PayrollPostStatus" ON "AttendanceSalarySlipDrafts" ("CompanyId", "StoreId", "Year", "Month", "PayrollPostStatus");
        CREATE INDEX IF NOT EXISTS "IX_AttendanceSalarySlipDrafts_GeneratedSalaryPaySlipId" ON "AttendanceSalarySlipDrafts" ("GeneratedSalaryPaySlipId");
        CREATE INDEX IF NOT EXISTS "IX_AttendanceSalarySlipDrafts_GeneratedSalaryPaymentId" ON "AttendanceSalarySlipDrafts" ("GeneratedSalaryPaymentId");
        CREATE INDEX IF NOT EXISTS "IX_AttendanceSalarySlipDrafts_CompanyId_StoreId_Year_Month_PaymentPostStatus" ON "AttendanceSalarySlipDrafts" ("CompanyId", "StoreId", "Year", "Month", "PaymentPostStatus");

        ALTER TABLE "AttendancePhotoProofs" ADD COLUMN IF NOT EXISTS "ReviewStatus" character varying(40) NOT NULL DEFAULT 'PendingReview';
        ALTER TABLE "AttendancePhotoProofs" ADD COLUMN IF NOT EXISTS "ReviewedAtUtc" timestamp without time zone NULL;
        ALTER TABLE "AttendancePhotoProofs" ADD COLUMN IF NOT EXISTS "ReviewedBy" character varying(120) NULL;
        ALTER TABLE "AttendancePhotoProofs" ADD COLUMN IF NOT EXISTS "ReviewRemarks" character varying(300) NULL;
        ALTER TABLE "AttendancePhotoProofs" ADD COLUMN IF NOT EXISTS "ReviewReason" character varying(80) NULL;
        ALTER TABLE "AttendancePhotoProofs" ADD COLUMN IF NOT EXISTS "RegularizationRequestId" uuid NULL;
        CREATE INDEX IF NOT EXISTS "IX_AttendancePhotoProofs_CompanyId_StoreId_EmployeeId_CapturedAtUtc" ON "AttendancePhotoProofs" ("CompanyId", "StoreId", "EmployeeId", "CapturedAtUtc");
        CREATE INDEX IF NOT EXISTS "IX_AttendancePhotoProofs_CompanyId_StoreId_ReviewStatus_CapturedAtUtc" ON "AttendancePhotoProofs" ("CompanyId", "StoreId", "ReviewStatus", "CapturedAtUtc");
        CREATE INDEX IF NOT EXISTS "IX_AttendancePhotoProofs_CompanyId_ClientPunchId" ON "AttendancePhotoProofs" ("CompanyId", "ClientPunchId");
        CREATE INDEX IF NOT EXISTS "IX_AttendanceKioskSyncBatches_CompanyId_StoreId_DeviceId_ReceivedAtUtc" ON "AttendanceKioskSyncBatches" ("CompanyId", "StoreId", "DeviceId", "ReceivedAtUtc");
        """, cancellationToken);

    logger.LogInformation("Attendance Core storage repair check completed.");
}


public static async Task RepairDigitalBillCrmStorageAsync(GarmetixDbContext db, ILogger logger, CancellationToken cancellationToken = default)
{
    await db.Database.ExecuteSqlRawAsync("""
        CREATE TABLE IF NOT EXISTS "DigitalInvoices" (
            "Id" uuid NOT NULL,
            "CreatedAt" timestamp without time zone NOT NULL DEFAULT now(),
            "UpdatedAt" timestamp without time zone NULL,
            "Synced" boolean NOT NULL DEFAULT false,
            "Deleted" boolean NOT NULL DEFAULT false,
            "CompanyId" uuid NOT NULL,
            "CreatedBy" text NULL,
            "StoreGroupId" uuid NOT NULL,
            "StoreId" uuid NOT NULL,
            "InvoiceId" uuid NOT NULL,
            "InvoiceType" character varying(40) NOT NULL DEFAULT 'Sale',
            "InvoiceNumber" character varying(80) NOT NULL DEFAULT '',
            "InvoiceDate" timestamp without time zone NOT NULL DEFAULT now(),
            "CustomerName" character varying(160) NOT NULL DEFAULT '',
            "CustomerMobile" character varying(30) NOT NULL DEFAULT '',
            "Amount" numeric(18,2) NOT NULL DEFAULT 0,
            "PublicToken" character varying(80) NOT NULL,
            "PublicUrl" character varying(300) NULL,
            "PdfPath" character varying(300) NULL,
            "HtmlSnapshotJson" text NULL,
            "ExpiresAt" timestamp without time zone NULL,
            "IsActive" boolean NOT NULL DEFAULT true,
            "DisabledAt" timestamp without time zone NULL,
            "DisabledBy" character varying(120) NULL,
            "DisableReason" character varying(300) NULL,
            "LastOpenedAt" timestamp without time zone NULL,
            "OpenCount" integer NOT NULL DEFAULT 0,
            "PdfDownloadCount" integer NOT NULL DEFAULT 0,
            "ReviewClickCount" integer NOT NULL DEFAULT 0,
            "FeedbackCount" integer NOT NULL DEFAULT 0,
            "WhatsAppStatus" character varying(40) NOT NULL DEFAULT 'NotConfigured',
            "LastWhatsAppSentAt" timestamp without time zone NULL,
            CONSTRAINT "PK_DigitalInvoices" PRIMARY KEY ("Id")
        );

        CREATE TABLE IF NOT EXISTS "DigitalInvoiceEvents" (
            "Id" uuid NOT NULL,
            "CreatedAt" timestamp without time zone NOT NULL DEFAULT now(),
            "UpdatedAt" timestamp without time zone NULL,
            "Synced" boolean NOT NULL DEFAULT false,
            "Deleted" boolean NOT NULL DEFAULT false,
            "CompanyId" uuid NOT NULL,
            "CreatedBy" text NULL,
            "StoreGroupId" uuid NOT NULL,
            "StoreId" uuid NOT NULL,
            "DigitalInvoiceId" uuid NOT NULL,
            "EventType" character varying(60) NOT NULL DEFAULT '',
            "Source" character varying(80) NULL,
            "IpAddress" character varying(120) NULL,
            "UserAgent" character varying(500) NULL,
            "TargetUrl" character varying(500) NULL,
            "DetailsJson" text NULL,
            "EventAt" timestamp without time zone NOT NULL DEFAULT now(),
            CONSTRAINT "PK_DigitalInvoiceEvents" PRIMARY KEY ("Id")
        );

        CREATE TABLE IF NOT EXISTS "StoreReviewSettings" (
            "Id" uuid NOT NULL,
            "CreatedAt" timestamp without time zone NOT NULL DEFAULT now(),
            "UpdatedAt" timestamp without time zone NULL,
            "Synced" boolean NOT NULL DEFAULT false,
            "Deleted" boolean NOT NULL DEFAULT false,
            "CompanyId" uuid NOT NULL,
            "CreatedBy" text NULL,
            "StoreGroupId" uuid NOT NULL,
            "StoreId" uuid NOT NULL,
            "GoogleReviewUrl" character varying(500) NULL,
            "InstagramUrl" character varying(500) NULL,
            "FacebookUrl" character varying(500) NULL,
            "WhatsAppSupportNumber" character varying(30) NULL,
            "EnableGoogleReview" boolean NOT NULL DEFAULT true,
            "EnableInstagram" boolean NOT NULL DEFAULT true,
            "EnableFacebook" boolean NOT NULL DEFAULT false,
            "EnableWhatsappSupport" boolean NOT NULL DEFAULT true,
            "EnablePrivateFeedback" boolean NOT NULL DEFAULT true,
            "ReviewButtonText" character varying(160) NULL,
            "FeedbackButtonText" character varying(160) NULL,
            CONSTRAINT "PK_StoreReviewSettings" PRIMARY KEY ("Id")
        );

        CREATE TABLE IF NOT EXISTS "CustomerFeedback" (
            "Id" uuid NOT NULL,
            "CreatedAt" timestamp without time zone NOT NULL DEFAULT now(),
            "UpdatedAt" timestamp without time zone NULL,
            "Synced" boolean NOT NULL DEFAULT false,
            "Deleted" boolean NOT NULL DEFAULT false,
            "CompanyId" uuid NOT NULL,
            "CreatedBy" text NULL,
            "StoreGroupId" uuid NOT NULL,
            "StoreId" uuid NOT NULL,
            "DigitalInvoiceId" uuid NOT NULL,
            "CustomerId" uuid NULL,
            "CustomerName" character varying(160) NOT NULL DEFAULT '',
            "CustomerMobile" character varying(30) NOT NULL DEFAULT '',
            "Rating" integer NOT NULL DEFAULT 0,
            "Message" character varying(1000) NULL,
            "Source" character varying(60) NOT NULL DEFAULT 'DigitalInvoice',
            "SubmittedAt" timestamp without time zone NOT NULL DEFAULT now(),
            CONSTRAINT "PK_CustomerFeedback" PRIMARY KEY ("Id")
        );


        CREATE TABLE IF NOT EXISTS "WhatsAppProviderSettings" (
            "Id" uuid NOT NULL,
            "CreatedAt" timestamp without time zone NOT NULL DEFAULT now(),
            "UpdatedAt" timestamp without time zone NULL,
            "Synced" boolean NOT NULL DEFAULT false,
            "Deleted" boolean NOT NULL DEFAULT false,
            "CompanyId" uuid NOT NULL,
            "CreatedBy" text NULL,
            "StoreGroupId" uuid NOT NULL,
            "StoreId" uuid NOT NULL,
            "IsEnabled" boolean NOT NULL DEFAULT false,
            "AutoSendDigitalBills" boolean NOT NULL DEFAULT false,
            "Provider" character varying(80) NOT NULL DEFAULT 'ManualOnly',
            "ApiBaseUrl" character varying(500) NULL,
            "ApiToken" character varying(1000) NULL,
            "PhoneNumberId" character varying(120) NULL,
            "SenderId" character varying(120) NULL,
            "TemplateName" character varying(120) NULL,
            "LanguageCode" character varying(20) NOT NULL DEFAULT 'en',
            "MessageTemplateText" character varying(2000) NOT NULL DEFAULT 'Hello {{customerName}}, thank you for shopping at {{storeName}}. Your invoice {{invoiceNumber}} of ₹{{amount}} is ready. View bill: {{publicUrl}}',
            "SendPdfLink" boolean NOT NULL DEFAULT true,
            "FallbackToManualLog" boolean NOT NULL DEFAULT true,
            "RetryLimit" integer NOT NULL DEFAULT 3,
            "LastTestAt" timestamp without time zone NULL,
            "LastError" character varying(1000) NULL,
            CONSTRAINT "PK_WhatsAppProviderSettings" PRIMARY KEY ("Id")
        );

        CREATE TABLE IF NOT EXISTS "WhatsAppMessageLogs" (
            "Id" uuid NOT NULL,
            "CreatedAt" timestamp without time zone NOT NULL DEFAULT now(),
            "UpdatedAt" timestamp without time zone NULL,
            "Synced" boolean NOT NULL DEFAULT false,
            "Deleted" boolean NOT NULL DEFAULT false,
            "CompanyId" uuid NOT NULL,
            "CreatedBy" text NULL,
            "StoreGroupId" uuid NOT NULL,
            "StoreId" uuid NOT NULL,
            "DigitalInvoiceId" uuid NULL,
            "CustomerMobile" character varying(30) NOT NULL DEFAULT '',
            "Provider" character varying(80) NOT NULL DEFAULT 'ManualOnly',
            "TemplateName" character varying(120) NULL,
            "MessageBody" character varying(2000) NOT NULL DEFAULT '',
            "Status" character varying(60) NOT NULL DEFAULT 'Queued',
            "ProviderMessageId" character varying(160) NULL,
            "ErrorMessage" character varying(1000) NULL,
            "RetryCount" integer NOT NULL DEFAULT 0,
            "SentAt" timestamp without time zone NULL,
            "DeliveredAt" timestamp without time zone NULL,
            "ReadAt" timestamp without time zone NULL,
            CONSTRAINT "PK_WhatsAppMessageLogs" PRIMARY KEY ("Id")
        );

        CREATE TABLE IF NOT EXISTS "InvoiceAdBanners" (
            "Id" uuid NOT NULL,
            "CreatedAt" timestamp without time zone NOT NULL DEFAULT now(),
            "UpdatedAt" timestamp without time zone NULL,
            "Synced" boolean NOT NULL DEFAULT false,
            "Deleted" boolean NOT NULL DEFAULT false,
            "CompanyId" uuid NOT NULL,
            "CreatedBy" text NULL,
            "StoreGroupId" uuid NULL,
            "StoreId" uuid NULL,
            "Title" character varying(160) NOT NULL DEFAULT '',
            "ImageUrl" character varying(500) NOT NULL DEFAULT '',
            "TargetUrl" character varying(500) NULL,
            "Position" character varying(40) NOT NULL DEFAULT 'Footer',
            "StartDate" timestamp without time zone NULL,
            "EndDate" timestamp without time zone NULL,
            "IsActive" boolean NOT NULL DEFAULT true,
            "Priority" integer NOT NULL DEFAULT 0,
            "ClickCount" integer NOT NULL DEFAULT 0,
            CONSTRAINT "PK_InvoiceAdBanners" PRIMARY KEY ("Id")
        );

        CREATE TABLE IF NOT EXISTS "DigitalBillCampaigns" (
            "Id" uuid NOT NULL,
            "CreatedAt" timestamp without time zone NOT NULL DEFAULT now(),
            "UpdatedAt" timestamp without time zone NULL,
            "Synced" boolean NOT NULL DEFAULT false,
            "Deleted" boolean NOT NULL DEFAULT false,
            "CompanyId" uuid NOT NULL,
            "CreatedBy" text NULL,
            "StoreGroupId" uuid NULL,
            "StoreId" uuid NULL,
            "Name" character varying(160) NOT NULL DEFAULT '',
            "Segment" character varying(60) NOT NULL DEFAULT 'all',
            "FromDate" timestamp without time zone NOT NULL DEFAULT now(),
            "ToDate" timestamp without time zone NOT NULL DEFAULT now(),
            "SearchText" character varying(160) NULL,
            "Channel" character varying(60) NOT NULL DEFAULT 'WhatsAppManual',
            "Status" character varying(60) NOT NULL DEFAULT 'Draft',
            "TemplateName" character varying(160) NULL,
            "MessageTitle" character varying(200) NOT NULL DEFAULT 'Digital bill campaign',
            "MessageBody" character varying(2000) NOT NULL DEFAULT '',
            "OfferUrl" character varying(500) NULL,
            "Notes" character varying(2000) NULL,
            "RecipientCount" integer NOT NULL DEFAULT 0,
            "PreparedCount" integer NOT NULL DEFAULT 0,
            "SentCount" integer NOT NULL DEFAULT 0,
            "FailedCount" integer NOT NULL DEFAULT 0,
            "OpenCountAtCreate" integer NOT NULL DEFAULT 0,
            "ReviewClickCountAtCreate" integer NOT NULL DEFAULT 0,
            "FeedbackCountAtCreate" integer NOT NULL DEFAULT 0,
            "ScheduledAt" timestamp without time zone NULL,
            "QueuedAt" timestamp without time zone NULL,
            "CompletedAt" timestamp without time zone NULL,
            "CancelledAt" timestamp without time zone NULL,
            CONSTRAINT "PK_DigitalBillCampaigns" PRIMARY KEY ("Id")
        );

        CREATE TABLE IF NOT EXISTS "DigitalBillCampaignRecipients" (
            "Id" uuid NOT NULL,
            "CreatedAt" timestamp without time zone NOT NULL DEFAULT now(),
            "UpdatedAt" timestamp without time zone NULL,
            "Synced" boolean NOT NULL DEFAULT false,
            "Deleted" boolean NOT NULL DEFAULT false,
            "CompanyId" uuid NOT NULL,
            "CreatedBy" text NULL,
            "StoreGroupId" uuid NOT NULL,
            "StoreId" uuid NOT NULL,
            "CampaignId" uuid NOT NULL,
            "DigitalInvoiceId" uuid NOT NULL,
            "AudienceKey" character varying(120) NOT NULL DEFAULT '',
            "CustomerName" character varying(160) NOT NULL DEFAULT '',
            "CustomerMobile" character varying(30) NOT NULL DEFAULT '',
            "InvoiceNumber" character varying(80) NOT NULL DEFAULT '',
            "PublicPath" character varying(300) NOT NULL DEFAULT '',
            "LastInvoiceAmount" numeric(18,2) NOT NULL DEFAULT 0,
            "Status" character varying(60) NOT NULL DEFAULT 'Prepared',
            "MessageBody" character varying(2000) NOT NULL DEFAULT '',
            "ErrorMessage" character varying(1000) NULL,
            "QueuedAt" timestamp without time zone NULL,
            "SentAt" timestamp without time zone NULL,
            "FailedAt" timestamp without time zone NULL,
            CONSTRAINT "PK_DigitalBillCampaignRecipients" PRIMARY KEY ("Id")
        );

        ALTER TABLE "DigitalBillCampaigns" ADD COLUMN IF NOT EXISTS "StoreGroupId" uuid NULL;
        ALTER TABLE "DigitalBillCampaigns" ADD COLUMN IF NOT EXISTS "StoreId" uuid NULL;
        ALTER TABLE "DigitalBillCampaigns" ADD COLUMN IF NOT EXISTS "Name" character varying(160) NOT NULL DEFAULT '';
        ALTER TABLE "DigitalBillCampaigns" ADD COLUMN IF NOT EXISTS "Segment" character varying(60) NOT NULL DEFAULT 'all';
        ALTER TABLE "DigitalBillCampaigns" ADD COLUMN IF NOT EXISTS "FromDate" timestamp without time zone NOT NULL DEFAULT now();
        ALTER TABLE "DigitalBillCampaigns" ADD COLUMN IF NOT EXISTS "ToDate" timestamp without time zone NOT NULL DEFAULT now();
        ALTER TABLE "DigitalBillCampaigns" ADD COLUMN IF NOT EXISTS "SearchText" character varying(160) NULL;
        ALTER TABLE "DigitalBillCampaigns" ADD COLUMN IF NOT EXISTS "Channel" character varying(60) NOT NULL DEFAULT 'WhatsAppManual';
        ALTER TABLE "DigitalBillCampaigns" ADD COLUMN IF NOT EXISTS "Status" character varying(60) NOT NULL DEFAULT 'Draft';
        ALTER TABLE "DigitalBillCampaigns" ADD COLUMN IF NOT EXISTS "TemplateName" character varying(160) NULL;
        ALTER TABLE "DigitalBillCampaigns" ADD COLUMN IF NOT EXISTS "MessageTitle" character varying(200) NOT NULL DEFAULT 'Digital bill campaign';
        ALTER TABLE "DigitalBillCampaigns" ADD COLUMN IF NOT EXISTS "MessageBody" character varying(2000) NOT NULL DEFAULT '';
        ALTER TABLE "DigitalBillCampaigns" ADD COLUMN IF NOT EXISTS "OfferUrl" character varying(500) NULL;
        ALTER TABLE "DigitalBillCampaigns" ADD COLUMN IF NOT EXISTS "Notes" character varying(2000) NULL;
        ALTER TABLE "DigitalBillCampaigns" ADD COLUMN IF NOT EXISTS "RecipientCount" integer NOT NULL DEFAULT 0;
        ALTER TABLE "DigitalBillCampaigns" ADD COLUMN IF NOT EXISTS "PreparedCount" integer NOT NULL DEFAULT 0;
        ALTER TABLE "DigitalBillCampaigns" ADD COLUMN IF NOT EXISTS "SentCount" integer NOT NULL DEFAULT 0;
        ALTER TABLE "DigitalBillCampaigns" ADD COLUMN IF NOT EXISTS "FailedCount" integer NOT NULL DEFAULT 0;
        ALTER TABLE "DigitalBillCampaigns" ADD COLUMN IF NOT EXISTS "OpenCountAtCreate" integer NOT NULL DEFAULT 0;
        ALTER TABLE "DigitalBillCampaigns" ADD COLUMN IF NOT EXISTS "ReviewClickCountAtCreate" integer NOT NULL DEFAULT 0;
        ALTER TABLE "DigitalBillCampaigns" ADD COLUMN IF NOT EXISTS "FeedbackCountAtCreate" integer NOT NULL DEFAULT 0;
        ALTER TABLE "DigitalBillCampaigns" ADD COLUMN IF NOT EXISTS "ScheduledAt" timestamp without time zone NULL;
        ALTER TABLE "DigitalBillCampaigns" ADD COLUMN IF NOT EXISTS "QueuedAt" timestamp without time zone NULL;
        ALTER TABLE "DigitalBillCampaigns" ADD COLUMN IF NOT EXISTS "CompletedAt" timestamp without time zone NULL;
        ALTER TABLE "DigitalBillCampaigns" ADD COLUMN IF NOT EXISTS "CancelledAt" timestamp without time zone NULL;

        ALTER TABLE "DigitalBillCampaignRecipients" ADD COLUMN IF NOT EXISTS "CampaignId" uuid NOT NULL DEFAULT '00000000-0000-0000-0000-000000000000';
        ALTER TABLE "DigitalBillCampaignRecipients" ADD COLUMN IF NOT EXISTS "DigitalInvoiceId" uuid NOT NULL DEFAULT '00000000-0000-0000-0000-000000000000';
        ALTER TABLE "DigitalBillCampaignRecipients" ADD COLUMN IF NOT EXISTS "AudienceKey" character varying(120) NOT NULL DEFAULT '';
        ALTER TABLE "DigitalBillCampaignRecipients" ADD COLUMN IF NOT EXISTS "CustomerName" character varying(160) NOT NULL DEFAULT '';
        ALTER TABLE "DigitalBillCampaignRecipients" ADD COLUMN IF NOT EXISTS "CustomerMobile" character varying(30) NOT NULL DEFAULT '';
        ALTER TABLE "DigitalBillCampaignRecipients" ADD COLUMN IF NOT EXISTS "InvoiceNumber" character varying(80) NOT NULL DEFAULT '';
        ALTER TABLE "DigitalBillCampaignRecipients" ADD COLUMN IF NOT EXISTS "PublicPath" character varying(300) NOT NULL DEFAULT '';
        ALTER TABLE "DigitalBillCampaignRecipients" ADD COLUMN IF NOT EXISTS "LastInvoiceAmount" numeric(18,2) NOT NULL DEFAULT 0;
        ALTER TABLE "DigitalBillCampaignRecipients" ADD COLUMN IF NOT EXISTS "Status" character varying(60) NOT NULL DEFAULT 'Prepared';
        ALTER TABLE "DigitalBillCampaignRecipients" ADD COLUMN IF NOT EXISTS "MessageBody" character varying(2000) NOT NULL DEFAULT '';
        ALTER TABLE "DigitalBillCampaignRecipients" ADD COLUMN IF NOT EXISTS "ErrorMessage" character varying(1000) NULL;
        ALTER TABLE "DigitalBillCampaignRecipients" ADD COLUMN IF NOT EXISTS "QueuedAt" timestamp without time zone NULL;
        ALTER TABLE "DigitalBillCampaignRecipients" ADD COLUMN IF NOT EXISTS "SentAt" timestamp without time zone NULL;
        ALTER TABLE "DigitalBillCampaignRecipients" ADD COLUMN IF NOT EXISTS "FailedAt" timestamp without time zone NULL;

        CREATE UNIQUE INDEX IF NOT EXISTS "IX_DigitalInvoices_PublicToken" ON "DigitalInvoices" ("PublicToken");
        CREATE INDEX IF NOT EXISTS "IX_DigitalInvoices_CompanyId_StoreId_InvoiceDate" ON "DigitalInvoices" ("CompanyId", "StoreId", "InvoiceDate");
        CREATE INDEX IF NOT EXISTS "IX_DigitalInvoices_CompanyId_InvoiceId_InvoiceType" ON "DigitalInvoices" ("CompanyId", "InvoiceId", "InvoiceType");
        CREATE INDEX IF NOT EXISTS "IX_DigitalInvoiceEvents_CompanyId_DigitalInvoiceId_EventAt" ON "DigitalInvoiceEvents" ("CompanyId", "DigitalInvoiceId", "EventAt");
        CREATE INDEX IF NOT EXISTS "IX_DigitalInvoiceEvents_CompanyId_StoreId_EventType_EventAt" ON "DigitalInvoiceEvents" ("CompanyId", "StoreId", "EventType", "EventAt");
        CREATE INDEX IF NOT EXISTS "IX_StoreReviewSettings_CompanyId_StoreId" ON "StoreReviewSettings" ("CompanyId", "StoreId");
        CREATE INDEX IF NOT EXISTS "IX_CustomerFeedback_CompanyId_StoreId_SubmittedAt" ON "CustomerFeedback" ("CompanyId", "StoreId", "SubmittedAt");
        CREATE INDEX IF NOT EXISTS "IX_CustomerFeedback_CompanyId_DigitalInvoiceId" ON "CustomerFeedback" ("CompanyId", "DigitalInvoiceId");

        CREATE INDEX IF NOT EXISTS "IX_WhatsAppProviderSettings_CompanyId_StoreId" ON "WhatsAppProviderSettings" ("CompanyId", "StoreId");
        CREATE INDEX IF NOT EXISTS "IX_WhatsAppProviderSettings_CompanyId_StoreId_IsEnabled_AutoSendDigitalBills" ON "WhatsAppProviderSettings" ("CompanyId", "StoreId", "IsEnabled", "AutoSendDigitalBills");
        CREATE INDEX IF NOT EXISTS "IX_WhatsAppMessageLogs_CompanyId_StoreId_Status_CreatedAt" ON "WhatsAppMessageLogs" ("CompanyId", "StoreId", "Status", "CreatedAt");
        CREATE INDEX IF NOT EXISTS "IX_WhatsAppMessageLogs_CompanyId_DigitalInvoiceId" ON "WhatsAppMessageLogs" ("CompanyId", "DigitalInvoiceId");
        CREATE INDEX IF NOT EXISTS "IX_InvoiceAdBanners_CompanyId_StoreId_Position_IsActive" ON "InvoiceAdBanners" ("CompanyId", "StoreId", "Position", "IsActive");
        CREATE INDEX IF NOT EXISTS "IX_DigitalBillCampaigns_CompanyId_StoreId_Status_CreatedAt" ON "DigitalBillCampaigns" ("CompanyId", "StoreId", "Status", "CreatedAt");
        CREATE INDEX IF NOT EXISTS "IX_DigitalBillCampaigns_CompanyId_Segment_FromDate_ToDate" ON "DigitalBillCampaigns" ("CompanyId", "Segment", "FromDate", "ToDate");
        CREATE INDEX IF NOT EXISTS "IX_DigitalBillCampaignRecipients_CompanyId_CampaignId_Status" ON "DigitalBillCampaignRecipients" ("CompanyId", "CampaignId", "Status");
        CREATE INDEX IF NOT EXISTS "IX_DigitalBillCampaignRecipients_CompanyId_CustomerMobile_CreatedAt" ON "DigitalBillCampaignRecipients" ("CompanyId", "CustomerMobile", "CreatedAt");
        """, cancellationToken);

    logger.LogInformation("Digital Bill CRM storage repair check completed.");
}


public static async Task RepairDotMatrixPrintStorageAsync(GarmetixDbContext db, ILogger logger, CancellationToken cancellationToken = default)
{
    await db.Database.ExecuteSqlRawAsync("""
        CREATE TABLE IF NOT EXISTS "DotMatrixPrintSettings" (
            "Id" uuid NOT NULL,
            "CreatedAt" timestamp without time zone NOT NULL DEFAULT now(),
            "UpdatedAt" timestamp without time zone NULL,
            "Synced" boolean NOT NULL DEFAULT false,
            "Deleted" boolean NOT NULL DEFAULT false,
            "CompanyId" uuid NOT NULL DEFAULT '00000000-0000-0000-0000-000000000000',
            "CreatedBy" text NULL,
            "StoreGroupId" uuid NOT NULL DEFAULT '00000000-0000-0000-0000-000000000000',
            "StoreId" uuid NOT NULL DEFAULT '00000000-0000-0000-0000-000000000000',
            "PrinterName" character varying(120) NOT NULL DEFAULT 'EPSON_LX810',
            "OutputMode" character varying(40) NOT NULL DEFAULT 'BridgeService',
            "SpoolDirectory" character varying(500) NOT NULL DEFAULT '/app/data/dotmatrix-spool',
            "TimeZoneId" character varying(80) NOT NULL DEFAULT 'Asia/Kolkata',
            "Enabled" boolean NOT NULL DEFAULT false,
            "PrintTransactions" boolean NOT NULL DEFAULT true,
            "PrintDayOpeningClosing" boolean NOT NULL DEFAULT true,
            "PrintEditsAndDeletes" boolean NOT NULL DEFAULT true,
            "PrintAttendanceInDaySummary" boolean NOT NULL DEFAULT true,
            "PrintBankUpiSummary" boolean NOT NULL DEFAULT true,
            "LineWidth" integer NOT NULL DEFAULT 136,
            "RetryLimit" integer NOT NULL DEFAULT 10,
            "PollSeconds" integer NOT NULL DEFAULT 5,
            "Remarks" character varying(300) NULL,
            CONSTRAINT "PK_DotMatrixPrintSettings" PRIMARY KEY ("Id")
        );

        CREATE TABLE IF NOT EXISTS "DotMatrixPrintQueueEntries" (
            "Id" uuid NOT NULL,
            "CreatedAt" timestamp without time zone NOT NULL DEFAULT now(),
            "UpdatedAt" timestamp without time zone NULL,
            "Synced" boolean NOT NULL DEFAULT false,
            "Deleted" boolean NOT NULL DEFAULT false,
            "CompanyId" uuid NOT NULL DEFAULT '00000000-0000-0000-0000-000000000000',
            "CreatedBy" text NULL,
            "StoreGroupId" uuid NOT NULL DEFAULT '00000000-0000-0000-0000-000000000000',
            "StoreId" uuid NOT NULL DEFAULT '00000000-0000-0000-0000-000000000000',
            "BusinessDate" timestamp without time zone NOT NULL,
            "OperationTimeUtc" timestamp without time zone NOT NULL DEFAULT now(),
            "EventType" character varying(40) NOT NULL DEFAULT 'Transaction',
            "ActionType" character varying(40) NOT NULL DEFAULT 'Create',
            "SourceType" character varying(80) NOT NULL DEFAULT '',
            "SourceId" uuid NOT NULL DEFAULT '00000000-0000-0000-0000-000000000000',
            "SourceNumber" character varying(120) NOT NULL DEFAULT '',
            "PartyName" character varying(120) NOT NULL DEFAULT '',
            "PaymentMode" character varying(120) NOT NULL DEFAULT '',
            "Amount" numeric(18,2) NOT NULL DEFAULT 0,
            "SequenceNo" bigint NOT NULL DEFAULT 0,
            "LineWidth" integer NOT NULL DEFAULT 136,
            "PrinterName" character varying(120) NOT NULL DEFAULT 'EPSON_LX810',
            "Status" character varying(40) NOT NULL DEFAULT 'Pending',
            "RetryCount" integer NOT NULL DEFAULT 0,
            "PrintedAtUtc" timestamp without time zone NULL,
            "ErrorMessage" character varying(500) NULL,
            "DeduplicationKey" character varying(240) NOT NULL DEFAULT '',
            "PrintableText" text NOT NULL DEFAULT '',
            CONSTRAINT "PK_DotMatrixPrintQueueEntries" PRIMARY KEY ("Id")
        );

        ALTER TABLE "DotMatrixPrintSettings" ALTER COLUMN "PrinterName" SET DEFAULT 'EPSON_LX810';
        ALTER TABLE "DotMatrixPrintSettings" ALTER COLUMN "OutputMode" SET DEFAULT 'BridgeService';
        ALTER TABLE "DotMatrixPrintQueueEntries" ALTER COLUMN "PrinterName" SET DEFAULT 'EPSON_LX810';
        ALTER TABLE "DotMatrixPrintSettings" ADD COLUMN IF NOT EXISTS "PrintBankUpiSummary" boolean NOT NULL DEFAULT true;
        ALTER TABLE "DotMatrixPrintQueueEntries" ADD COLUMN IF NOT EXISTS "PrintableText" text NOT NULL DEFAULT '';
        ALTER TABLE "DotMatrixPrintQueueEntries" ADD COLUMN IF NOT EXISTS "DeduplicationKey" character varying(240) NOT NULL DEFAULT '';

        CREATE INDEX IF NOT EXISTS "IX_DotMatrixPrintSettings_CompanyId_StoreId" ON "DotMatrixPrintSettings" ("CompanyId", "StoreId");
        CREATE INDEX IF NOT EXISTS "IX_DotMatrixPrintQueueEntries_CompanyId_StoreId_BusinessDate_SequenceNo" ON "DotMatrixPrintQueueEntries" ("CompanyId", "StoreId", "BusinessDate", "SequenceNo");
        CREATE INDEX IF NOT EXISTS "IX_DotMatrixPrintQueueEntries_Status_CreatedAt" ON "DotMatrixPrintQueueEntries" ("Status", "CreatedAt");
        CREATE INDEX IF NOT EXISTS "IX_DotMatrixPrintQueueEntries_SourceType_SourceId_ActionType" ON "DotMatrixPrintQueueEntries" ("SourceType", "SourceId", "ActionType");
        CREATE INDEX IF NOT EXISTS "IX_DotMatrixPrintQueueEntries_DeduplicationKey" ON "DotMatrixPrintQueueEntries" ("DeduplicationKey");
        """, cancellationToken);

    logger.LogInformation("Dot-matrix print storage repair check completed.");
}

public static async Task RepairFinalAccountsStorageAsync(GarmetixDbContext db, ILogger logger, CancellationToken cancellationToken = default)
{
    // Final Accounts module (balance sheet / P&L / GL / CA workspace / period close /
    // projections / Tally exchange), merged from the balancesheet branch. Its 9 EF
    // migrations create a dedicated "final_accounts" Postgres schema, but on this host
    // dated migrations do not apply automatically (see the other Repair*Async methods
    // in this file) - so this idempotent DDL, generated from
    // "dotnet ef migrations script 20260623123000_InitialCreate 20260714150000_AddFinalAccountsExchangePackage --idempotent"
    // and mechanically stripped of its __EFMigrationsHistory guards, is what actually
    // creates the schema here. The module itself stays off by default per workspace
    // (FinalAccountsOptions.DefaultEnabled) regardless of this repair running.
    await db.Database.ExecuteSqlRawAsync("""
CREATE SCHEMA IF NOT EXISTS final_accounts;

CREATE TABLE IF NOT EXISTS final_accounts.fa_module_settings (
    "Id" uuid NOT NULL,
    "CreatedAt" timestamp without time zone NOT NULL,
    "UpdatedAt" timestamp without time zone,
    "Synced" boolean NOT NULL,
    "Deleted" boolean NOT NULL,
    "CompanyId" uuid,
    "StoreGroupId" uuid,
    "StoreId" uuid,
    "Enabled" boolean NOT NULL,
    "PostingMode" character varying(32) NOT NULL,
    "StatementTemplate" character varying(80) NOT NULL,
    "InventoryValuationMethod" character varying(48) NOT NULL,
    "RoundingScale" integer NOT NULL,
    "AllowHistoricalBackfill" boolean NOT NULL,
    "AllowTallyExport" boolean NOT NULL,
    "AllowProjections" boolean NOT NULL,
    "AllowPeriodReopen" boolean NOT NULL,
    "CreatedBy" character varying(120),
    "UpdatedBy" character varying(120),
    CONSTRAINT "PK_fa_module_settings" PRIMARY KEY ("Id")
);

CREATE UNIQUE INDEX IF NOT EXISTS "IX_fa_module_settings_scope"
ON final_accounts.fa_module_settings (
    COALESCE("CompanyId", '00000000-0000-0000-0000-000000000000'::uuid),
    COALESCE("StoreGroupId", '00000000-0000-0000-0000-000000000000'::uuid),
    COALESCE("StoreId", '00000000-0000-0000-0000-000000000000'::uuid)
);

CREATE INDEX IF NOT EXISTS "IX_fa_module_settings_Enabled_UpdatedAt" ON final_accounts.fa_module_settings ("Enabled", "UpdatedAt");

CREATE TABLE IF NOT EXISTS final_accounts.fa_account_groups (
    "Id" uuid NOT NULL,
    "CreatedAt" timestamp without time zone NOT NULL,
    "UpdatedAt" timestamp without time zone,
    "Synced" boolean NOT NULL,
    "Deleted" boolean NOT NULL,
    "CompanyId" uuid,
    "StoreGroupId" uuid,
    "StoreId" uuid,
    "ParentGroupId" uuid,
    "Code" character varying(40) NOT NULL,
    "Name" character varying(160) NOT NULL,
    "AccountType" integer NOT NULL,
    "NaturalBalance" integer NOT NULL,
    "SortOrder" integer NOT NULL,
    "IsSystem" boolean NOT NULL,
    "IsActive" boolean NOT NULL,
    "Description" character varying(500),
    "Revision" integer NOT NULL,
    "CreatedBy" character varying(120),
    "UpdatedBy" character varying(120),
    CONSTRAINT "PK_fa_account_groups" PRIMARY KEY ("Id"),
    CONSTRAINT "FK_fa_account_groups_parent" FOREIGN KEY ("ParentGroupId") REFERENCES final_accounts.fa_account_groups ("Id") ON DELETE RESTRICT
);

CREATE TABLE IF NOT EXISTS final_accounts.fa_fiscal_years (
    "Id" uuid NOT NULL,
    "CreatedAt" timestamp without time zone NOT NULL,
    "UpdatedAt" timestamp without time zone,
    "Synced" boolean NOT NULL,
    "Deleted" boolean NOT NULL,
    "CompanyId" uuid,
    "StoreGroupId" uuid,
    "StoreId" uuid,
    "Name" character varying(80) NOT NULL,
    "StartDate" timestamp without time zone NOT NULL,
    "EndDate" timestamp without time zone NOT NULL,
    "Status" integer NOT NULL,
    "ClosedAt" timestamp without time zone,
    "Revision" integer NOT NULL,
    "CreatedBy" character varying(120),
    "UpdatedBy" character varying(120),
    CONSTRAINT "PK_fa_fiscal_years" PRIMARY KEY ("Id"),
    CONSTRAINT "CK_fa_fiscal_years_dates" CHECK ("EndDate" >= "StartDate")
);

CREATE TABLE IF NOT EXISTS final_accounts.fa_accounts (
    "Id" uuid NOT NULL,
    "CreatedAt" timestamp without time zone NOT NULL,
    "UpdatedAt" timestamp without time zone,
    "Synced" boolean NOT NULL,
    "Deleted" boolean NOT NULL,
    "CompanyId" uuid,
    "StoreGroupId" uuid,
    "StoreId" uuid,
    "AccountGroupId" uuid NOT NULL,
    "ParentAccountId" uuid,
    "Code" character varying(40) NOT NULL,
    "Name" character varying(160) NOT NULL,
    "AccountType" integer NOT NULL,
    "NaturalBalance" integer NOT NULL,
    "OpeningBalance" numeric(18,2) NOT NULL,
    "IsControlAccount" boolean NOT NULL,
    "IsSystem" boolean NOT NULL,
    "IsActive" boolean NOT NULL,
    "Description" character varying(500),
    "SortOrder" integer NOT NULL,
    "Revision" integer NOT NULL,
    "CreatedBy" character varying(120),
    "UpdatedBy" character varying(120),
    CONSTRAINT "PK_fa_accounts" PRIMARY KEY ("Id"),
    CONSTRAINT "FK_fa_accounts_group" FOREIGN KEY ("AccountGroupId") REFERENCES final_accounts.fa_account_groups ("Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_fa_accounts_parent" FOREIGN KEY ("ParentAccountId") REFERENCES final_accounts.fa_accounts ("Id") ON DELETE RESTRICT
);

CREATE TABLE IF NOT EXISTS final_accounts.fa_fiscal_periods (
    "Id" uuid NOT NULL,
    "CreatedAt" timestamp without time zone NOT NULL,
    "UpdatedAt" timestamp without time zone,
    "Synced" boolean NOT NULL,
    "Deleted" boolean NOT NULL,
    "CompanyId" uuid,
    "StoreGroupId" uuid,
    "StoreId" uuid,
    "FiscalYearId" uuid NOT NULL,
    "PeriodNumber" integer NOT NULL,
    "Name" character varying(80) NOT NULL,
    "StartDate" timestamp without time zone NOT NULL,
    "EndDate" timestamp without time zone NOT NULL,
    "Status" integer NOT NULL,
    "ClosedAt" timestamp without time zone,
    "Revision" integer NOT NULL,
    "CreatedBy" character varying(120),
    "UpdatedBy" character varying(120),
    CONSTRAINT "PK_fa_fiscal_periods" PRIMARY KEY ("Id"),
    CONSTRAINT "CK_fa_fiscal_periods_dates" CHECK ("EndDate" >= "StartDate"),
    CONSTRAINT "FK_fa_fiscal_periods_year" FOREIGN KEY ("FiscalYearId") REFERENCES final_accounts.fa_fiscal_years ("Id") ON DELETE RESTRICT
);

CREATE TABLE IF NOT EXISTS final_accounts.fa_account_mappings (
    "Id" uuid NOT NULL,
    "CreatedAt" timestamp without time zone NOT NULL,
    "UpdatedAt" timestamp without time zone,
    "Synced" boolean NOT NULL,
    "Deleted" boolean NOT NULL,
    "CompanyId" uuid,
    "StoreGroupId" uuid,
    "StoreId" uuid,
    "SourceType" integer NOT NULL,
    "MappingKey" character varying(120) NOT NULL,
    "DisplayName" character varying(160) NOT NULL,
    "AccountId" uuid NOT NULL,
    "IsRequired" boolean NOT NULL,
    "IsSystem" boolean NOT NULL,
    "IsActive" boolean NOT NULL,
    "Notes" character varying(500),
    "Revision" integer NOT NULL,
    "CreatedBy" character varying(120),
    "UpdatedBy" character varying(120),
    CONSTRAINT "PK_fa_account_mappings" PRIMARY KEY ("Id"),
    CONSTRAINT "FK_fa_account_mappings_account" FOREIGN KEY ("AccountId") REFERENCES final_accounts.fa_accounts ("Id") ON DELETE RESTRICT
);

CREATE INDEX IF NOT EXISTS "IX_fa_account_groups_parent" ON final_accounts.fa_account_groups ("ParentGroupId");

CREATE INDEX IF NOT EXISTS "IX_fa_account_groups_scope_type_active" ON final_accounts.fa_account_groups ("CompanyId", "StoreGroupId", "StoreId", "AccountType", "IsActive");

CREATE INDEX IF NOT EXISTS "IX_fa_accounts_group_active" ON final_accounts.fa_accounts ("CompanyId", "StoreGroupId", "StoreId", "AccountGroupId", "IsActive");

CREATE INDEX IF NOT EXISTS "IX_fa_accounts_parent" ON final_accounts.fa_accounts ("ParentAccountId");

CREATE INDEX IF NOT EXISTS "IX_fa_account_mappings_account_active" ON final_accounts.fa_account_mappings ("AccountId", "IsActive");

CREATE INDEX IF NOT EXISTS "IX_fa_fiscal_years_scope_dates" ON final_accounts.fa_fiscal_years ("CompanyId", "StoreGroupId", "StoreId", "StartDate", "EndDate");

CREATE UNIQUE INDEX IF NOT EXISTS "IX_fa_fiscal_periods_year_number" ON final_accounts.fa_fiscal_periods ("FiscalYearId", "PeriodNumber");

CREATE INDEX IF NOT EXISTS "IX_fa_fiscal_periods_scope_dates" ON final_accounts.fa_fiscal_periods ("CompanyId", "StoreGroupId", "StoreId", "StartDate", "EndDate");

CREATE UNIQUE INDEX IF NOT EXISTS "IX_fa_account_groups_scope_code"
ON final_accounts.fa_account_groups (
    COALESCE("CompanyId", '00000000-0000-0000-0000-000000000000'::uuid),
    COALESCE("StoreGroupId", '00000000-0000-0000-0000-000000000000'::uuid),
    COALESCE("StoreId", '00000000-0000-0000-0000-000000000000'::uuid),
    "Code"
)
WHERE "Deleted" = false;

CREATE UNIQUE INDEX IF NOT EXISTS "IX_fa_accounts_scope_code"
ON final_accounts.fa_accounts (
    COALESCE("CompanyId", '00000000-0000-0000-0000-000000000000'::uuid),
    COALESCE("StoreGroupId", '00000000-0000-0000-0000-000000000000'::uuid),
    COALESCE("StoreId", '00000000-0000-0000-0000-000000000000'::uuid),
    "Code"
)
WHERE "Deleted" = false;

CREATE UNIQUE INDEX IF NOT EXISTS "IX_fa_fiscal_years_scope_name"
ON final_accounts.fa_fiscal_years (
    COALESCE("CompanyId", '00000000-0000-0000-0000-000000000000'::uuid),
    COALESCE("StoreGroupId", '00000000-0000-0000-0000-000000000000'::uuid),
    COALESCE("StoreId", '00000000-0000-0000-0000-000000000000'::uuid),
    "Name"
)
WHERE "Deleted" = false;

CREATE UNIQUE INDEX IF NOT EXISTS "IX_fa_account_mappings_scope_key"
ON final_accounts.fa_account_mappings (
    COALESCE("CompanyId", '00000000-0000-0000-0000-000000000000'::uuid),
    COALESCE("StoreGroupId", '00000000-0000-0000-0000-000000000000'::uuid),
    COALESCE("StoreId", '00000000-0000-0000-0000-000000000000'::uuid),
    "SourceType", "MappingKey"
)
WHERE "Deleted" = false;

CREATE TABLE IF NOT EXISTS final_accounts.fa_journal_entries (
    "Id" uuid NOT NULL,
    "CreatedAt" timestamp without time zone NOT NULL,
    "UpdatedAt" timestamp without time zone,
    "Synced" boolean NOT NULL,
    "Deleted" boolean NOT NULL,
    "CompanyId" uuid,
    "StoreGroupId" uuid,
    "StoreId" uuid,
    "EntryNumber" character varying(48) NOT NULL,
    "OnDate" timestamp without time zone NOT NULL,
    "FiscalPeriodId" uuid,
    "Status" integer NOT NULL,
    "SourceType" character varying(80) NOT NULL,
    "SourceId" uuid,
    "ReferenceNumber" character varying(120),
    "Narration" character varying(500) NOT NULL,
    "IdempotencyKey" character varying(160),
    "ReversalOfJournalEntryId" uuid,
    "ReversalJournalEntryId" uuid,
    "PostedAt" timestamp without time zone,
    "PostedBy" character varying(120),
    "ReversedAt" timestamp without time zone,
    "ReversedBy" character varying(120),
    "Revision" integer NOT NULL,
    "CreatedBy" character varying(120),
    "UpdatedBy" character varying(120),
    CONSTRAINT "PK_fa_journal_entries" PRIMARY KEY ("Id"),
    CONSTRAINT "FK_fa_journal_entries_fiscal_period" FOREIGN KEY ("FiscalPeriodId") REFERENCES final_accounts.fa_fiscal_periods ("Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_fa_journal_entries_reversal_of" FOREIGN KEY ("ReversalOfJournalEntryId") REFERENCES final_accounts.fa_journal_entries ("Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_fa_journal_entries_reversal_journal" FOREIGN KEY ("ReversalJournalEntryId") REFERENCES final_accounts.fa_journal_entries ("Id") ON DELETE RESTRICT
);

CREATE TABLE IF NOT EXISTS final_accounts.fa_journal_lines (
    "Id" uuid NOT NULL,
    "CreatedAt" timestamp without time zone NOT NULL,
    "UpdatedAt" timestamp without time zone,
    "Synced" boolean NOT NULL,
    "Deleted" boolean NOT NULL,
    "CompanyId" uuid,
    "StoreGroupId" uuid,
    "StoreId" uuid,
    "JournalEntryId" uuid NOT NULL,
    "AccountId" uuid NOT NULL,
    "LineNumber" integer NOT NULL,
    "Debit" numeric(18,2) NOT NULL,
    "Credit" numeric(18,2) NOT NULL,
    "Narration" character varying(500),
    "Revision" integer NOT NULL,
    "CreatedBy" character varying(120),
    "UpdatedBy" character varying(120),
    CONSTRAINT "PK_fa_journal_lines" PRIMARY KEY ("Id"),
    CONSTRAINT "CK_fa_journal_lines_debit_credit_non_negative" CHECK ("Debit" >= 0 AND "Credit" >= 0),
    CONSTRAINT "CK_fa_journal_lines_single_side" CHECK ((("Debit" > 0 AND "Credit" = 0) OR ("Credit" > 0 AND "Debit" = 0))),
    CONSTRAINT "FK_fa_journal_lines_account" FOREIGN KEY ("AccountId") REFERENCES final_accounts.fa_accounts ("Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_fa_journal_lines_entry" FOREIGN KEY ("JournalEntryId") REFERENCES final_accounts.fa_journal_entries ("Id") ON DELETE RESTRICT
);

CREATE TABLE IF NOT EXISTS final_accounts.fa_source_posting_links (
    "Id" uuid NOT NULL,
    "CreatedAt" timestamp without time zone NOT NULL,
    "UpdatedAt" timestamp without time zone,
    "Synced" boolean NOT NULL,
    "Deleted" boolean NOT NULL,
    "CompanyId" uuid,
    "StoreGroupId" uuid,
    "StoreId" uuid,
    "JournalEntryId" uuid NOT NULL,
    "SourceType" character varying(80) NOT NULL,
    "SourceId" uuid NOT NULL,
    "SourceReference" character varying(120),
    "SourceHash" character varying(128),
    "MappingVersion" character varying(80),
    "IdempotencyKey" character varying(160),
    "PostedAt" timestamp without time zone NOT NULL,
    "Revision" integer NOT NULL,
    "CreatedBy" character varying(120),
    "UpdatedBy" character varying(120),
    CONSTRAINT "PK_fa_source_posting_links" PRIMARY KEY ("Id"),
    CONSTRAINT "FK_fa_source_posting_links_entry" FOREIGN KEY ("JournalEntryId") REFERENCES final_accounts.fa_journal_entries ("Id") ON DELETE RESTRICT
);

CREATE INDEX IF NOT EXISTS "IX_fa_journal_entries_scope_date_status" ON final_accounts.fa_journal_entries ("CompanyId", "StoreGroupId", "StoreId", "OnDate", "Status");

CREATE INDEX IF NOT EXISTS "IX_fa_journal_entries_source" ON final_accounts.fa_journal_entries ("CompanyId", "StoreGroupId", "StoreId", "SourceType", "SourceId");

CREATE INDEX IF NOT EXISTS "IX_fa_journal_entries_fiscal_period" ON final_accounts.fa_journal_entries ("FiscalPeriodId");

CREATE INDEX IF NOT EXISTS "IX_fa_journal_entries_reversal_of" ON final_accounts.fa_journal_entries ("ReversalOfJournalEntryId");

CREATE INDEX IF NOT EXISTS "IX_fa_journal_entries_reversal_journal" ON final_accounts.fa_journal_entries ("ReversalJournalEntryId");

CREATE UNIQUE INDEX IF NOT EXISTS "IX_fa_journal_lines_journal_line_number" ON final_accounts.fa_journal_lines ("JournalEntryId", "LineNumber");

CREATE INDEX IF NOT EXISTS "IX_fa_journal_lines_account_journal" ON final_accounts.fa_journal_lines ("CompanyId", "StoreGroupId", "StoreId", "AccountId", "JournalEntryId");

CREATE INDEX IF NOT EXISTS "IX_fa_source_posting_links_journal" ON final_accounts.fa_source_posting_links ("JournalEntryId");

CREATE UNIQUE INDEX IF NOT EXISTS "IX_fa_journal_entries_scope_number"
ON final_accounts.fa_journal_entries (
    COALESCE("CompanyId", '00000000-0000-0000-0000-000000000000'::uuid),
    COALESCE("StoreGroupId", '00000000-0000-0000-0000-000000000000'::uuid),
    COALESCE("StoreId", '00000000-0000-0000-0000-000000000000'::uuid),
    "EntryNumber"
)
WHERE "Deleted" = false;

CREATE UNIQUE INDEX IF NOT EXISTS "IX_fa_journal_entries_scope_idempotency"
ON final_accounts.fa_journal_entries (
    COALESCE("CompanyId", '00000000-0000-0000-0000-000000000000'::uuid),
    COALESCE("StoreGroupId", '00000000-0000-0000-0000-000000000000'::uuid),
    COALESCE("StoreId", '00000000-0000-0000-0000-000000000000'::uuid),
    "IdempotencyKey"
)
WHERE "Deleted" = false AND "IdempotencyKey" IS NOT NULL;

CREATE UNIQUE INDEX IF NOT EXISTS "IX_fa_source_posting_links_scope_source"
ON final_accounts.fa_source_posting_links (
    COALESCE("CompanyId", '00000000-0000-0000-0000-000000000000'::uuid),
    COALESCE("StoreGroupId", '00000000-0000-0000-0000-000000000000'::uuid),
    COALESCE("StoreId", '00000000-0000-0000-0000-000000000000'::uuid),
    "SourceType", "SourceId"
)
WHERE "Deleted" = false;

CREATE UNIQUE INDEX IF NOT EXISTS "IX_fa_source_posting_links_scope_idempotency"
ON final_accounts.fa_source_posting_links (
    COALESCE("CompanyId", '00000000-0000-0000-0000-000000000000'::uuid),
    COALESCE("StoreGroupId", '00000000-0000-0000-0000-000000000000'::uuid),
    COALESCE("StoreId", '00000000-0000-0000-0000-000000000000'::uuid),
    "IdempotencyKey"
)
WHERE "Deleted" = false AND "IdempotencyKey" IS NOT NULL;

CREATE TABLE IF NOT EXISTS final_accounts.fa_posting_rules (
    "Id" uuid NOT NULL,
    "CreatedAt" timestamp without time zone NOT NULL,
    "UpdatedAt" timestamp without time zone,
    "Synced" boolean NOT NULL,
    "Deleted" boolean NOT NULL,
    "CompanyId" uuid,
    "StoreGroupId" uuid,
    "StoreId" uuid,
    "SourceType" character varying(80) NOT NULL,
    "RuleCode" character varying(80) NOT NULL,
    "Version" character varying(80) NOT NULL,
    "Name" character varying(160) NOT NULL,
    "Description" character varying(500),
    "EffectiveFrom" timestamp without time zone NOT NULL,
    "IsSystem" boolean NOT NULL,
    "IsActive" boolean NOT NULL,
    "Revision" integer NOT NULL,
    "CreatedBy" character varying(120),
    "UpdatedBy" character varying(120),
    CONSTRAINT "PK_fa_posting_rules" PRIMARY KEY ("Id")
);

CREATE TABLE IF NOT EXISTS final_accounts.fa_posting_rule_lines (
    "Id" uuid NOT NULL,
    "CreatedAt" timestamp without time zone NOT NULL,
    "UpdatedAt" timestamp without time zone,
    "Synced" boolean NOT NULL,
    "Deleted" boolean NOT NULL,
    "CompanyId" uuid,
    "StoreGroupId" uuid,
    "StoreId" uuid,
    "PostingRuleId" uuid NOT NULL,
    "MappingKey" character varying(120) NOT NULL,
    "DisplayName" character varying(160) NOT NULL,
    "MappingCategory" character varying(80) NOT NULL,
    "Direction" character varying(16) NOT NULL,
    "ExpectedAccountType" character varying(40),
    "IsRequired" boolean NOT NULL,
    "AllowControlAccount" boolean NOT NULL,
    "SortOrder" integer NOT NULL,
    "Notes" character varying(500),
    "Revision" integer NOT NULL,
    "CreatedBy" character varying(120),
    "UpdatedBy" character varying(120),
    CONSTRAINT "PK_fa_posting_rule_lines" PRIMARY KEY ("Id"),
    CONSTRAINT "FK_fa_posting_rule_lines_rule" FOREIGN KEY ("PostingRuleId") REFERENCES final_accounts.fa_posting_rules ("Id") ON DELETE RESTRICT
);

CREATE INDEX IF NOT EXISTS "IX_fa_posting_rules_scope_source_active" ON final_accounts.fa_posting_rules ("CompanyId", "StoreGroupId", "StoreId", "SourceType", "IsActive");

CREATE UNIQUE INDEX IF NOT EXISTS "IX_fa_posting_rule_lines_rule_key" ON final_accounts.fa_posting_rule_lines ("PostingRuleId", "MappingKey");

CREATE INDEX IF NOT EXISTS "IX_fa_posting_rule_lines_category_required" ON final_accounts.fa_posting_rule_lines ("CompanyId", "StoreGroupId", "StoreId", "MappingCategory", "IsRequired");

CREATE UNIQUE INDEX IF NOT EXISTS "IX_fa_posting_rules_scope_code_version"
ON final_accounts.fa_posting_rules (
    COALESCE("CompanyId", '00000000-0000-0000-0000-000000000000'::uuid),
    COALESCE("StoreGroupId", '00000000-0000-0000-0000-000000000000'::uuid),
    COALESCE("StoreId", '00000000-0000-0000-0000-000000000000'::uuid),
    "SourceType", "RuleCode", "Version"
)
WHERE "Deleted" = false;

CREATE TABLE IF NOT EXISTS final_accounts.fa_sync_jobs (
    "Id" uuid NOT NULL,
    "CreatedAt" timestamp without time zone NOT NULL,
    "UpdatedAt" timestamp without time zone,
    "Synced" boolean NOT NULL,
    "Deleted" boolean NOT NULL,
    "CompanyId" uuid,
    "StoreGroupId" uuid,
    "StoreId" uuid,
    "JobNumber" character varying(64) NOT NULL,
    "Status" integer NOT NULL,
    "Mode" character varying(32) NOT NULL,
    "DryRun" boolean NOT NULL,
    "Scheduled" boolean NOT NULL,
    "From" timestamp without time zone,
    "To" timestamp without time zone,
    "ModulesCsv" character varying(300) NOT NULL,
    "IdempotencyKey" character varying(160),
    "StopOnError" boolean NOT NULL,
    "MaxAttempts" integer NOT NULL,
    "RetryDelaySeconds" integer NOT NULL,
    "SourceCount" integer NOT NULL,
    "QueuedCount" integer NOT NULL,
    "SkippedCount" integer NOT NULL,
    "FailedCount" integer NOT NULL,
    "DriftCount" integer NOT NULL,
    "StartedAt" timestamp without time zone,
    "CompletedAt" timestamp without time zone,
    "LastCheckpoint" character varying(240),
    "ErrorPolicy" character varying(32) NOT NULL,
    "CreatedBy" character varying(120),
    "UpdatedBy" character varying(120),
    "Revision" integer NOT NULL,
    CONSTRAINT "PK_fa_sync_jobs" PRIMARY KEY ("Id")
);

CREATE TABLE IF NOT EXISTS final_accounts.fa_sync_checkpoints (
    "Id" uuid NOT NULL,
    "CreatedAt" timestamp without time zone NOT NULL,
    "UpdatedAt" timestamp without time zone,
    "Synced" boolean NOT NULL,
    "Deleted" boolean NOT NULL,
    "CompanyId" uuid,
    "StoreGroupId" uuid,
    "StoreId" uuid,
    "Module" character varying(80) NOT NULL,
    "CheckpointKey" character varying(160) NOT NULL,
    "LastSourceDate" timestamp without time zone,
    "LastSourceId" uuid,
    "LastSourceHash" character varying(128),
    "LastJobId" uuid,
    "ProcessedCount" integer NOT NULL,
    "FailedCount" integer NOT NULL,
    "UpdatedBy" character varying(120),
    "Revision" integer NOT NULL,
    CONSTRAINT "PK_fa_sync_checkpoints" PRIMARY KEY ("Id")
);

CREATE TABLE IF NOT EXISTS final_accounts.fa_sync_job_items (
    "Id" uuid NOT NULL,
    "CreatedAt" timestamp without time zone NOT NULL,
    "UpdatedAt" timestamp without time zone,
    "Synced" boolean NOT NULL,
    "Deleted" boolean NOT NULL,
    "JobId" uuid NOT NULL,
    "CompanyId" uuid,
    "StoreGroupId" uuid,
    "StoreId" uuid,
    "SourceType" character varying(80) NOT NULL,
    "SourceId" uuid NOT NULL,
    "SourceReference" character varying(120),
    "SourceDate" timestamp without time zone,
    "SourceAmount" numeric(18,2) NOT NULL,
    "SourceHash" character varying(128) NOT NULL,
    "Status" integer NOT NULL,
    "AttemptCount" integer NOT NULL,
    "NextAttemptAt" timestamp without time zone,
    "JournalEntryId" uuid,
    "ErrorCode" character varying(80),
    "ErrorMessage" character varying(1000),
    "Revision" integer NOT NULL,
    CONSTRAINT "PK_fa_sync_job_items" PRIMARY KEY ("Id")
);

CREATE TABLE IF NOT EXISTS final_accounts.fa_sync_exceptions (
    "Id" uuid NOT NULL,
    "CreatedAt" timestamp without time zone NOT NULL,
    "UpdatedAt" timestamp without time zone,
    "Synced" boolean NOT NULL,
    "Deleted" boolean NOT NULL,
    "JobId" uuid,
    "JobItemId" uuid,
    "CompanyId" uuid,
    "StoreGroupId" uuid,
    "StoreId" uuid,
    "SourceType" character varying(80) NOT NULL,
    "SourceId" uuid,
    "Severity" character varying(24) NOT NULL,
    "Category" character varying(80) NOT NULL,
    "Code" character varying(80) NOT NULL,
    "Message" character varying(1000) NOT NULL,
    "DetailsJson" text,
    "Resolved" boolean NOT NULL,
    "ResolvedAt" timestamp without time zone,
    "ResolvedBy" character varying(120),
    "ResolutionNotes" character varying(500),
    "Revision" integer NOT NULL,
    CONSTRAINT "PK_fa_sync_exceptions" PRIMARY KEY ("Id")
);

CREATE INDEX IF NOT EXISTS "IX_fa_sync_jobs_scope_status_created" ON final_accounts.fa_sync_jobs ("CompanyId", "StoreGroupId", "StoreId", "Status", "CreatedAt");

CREATE INDEX IF NOT EXISTS "IX_fa_sync_jobs_scope_dryrun_scheduled" ON final_accounts.fa_sync_jobs ("CompanyId", "StoreGroupId", "StoreId", "DryRun", "Scheduled");

CREATE UNIQUE INDEX IF NOT EXISTS "IX_fa_sync_jobs_scope_job_number" ON final_accounts.fa_sync_jobs ("CompanyId", "StoreGroupId", "StoreId", "JobNumber");

CREATE UNIQUE INDEX IF NOT EXISTS "IX_fa_sync_jobs_scope_idempotency" ON final_accounts.fa_sync_jobs ("CompanyId", "StoreGroupId", "StoreId", "IdempotencyKey");

CREATE UNIQUE INDEX IF NOT EXISTS "IX_fa_sync_job_items_job_source" ON final_accounts.fa_sync_job_items ("JobId", "SourceType", "SourceId");

CREATE INDEX IF NOT EXISTS "IX_fa_sync_job_items_scope_source" ON final_accounts.fa_sync_job_items ("CompanyId", "StoreGroupId", "StoreId", "SourceType", "SourceId");

CREATE INDEX IF NOT EXISTS "IX_fa_sync_job_items_job_status_next" ON final_accounts.fa_sync_job_items ("JobId", "Status", "NextAttemptAt");

CREATE UNIQUE INDEX IF NOT EXISTS "IX_fa_sync_checkpoints_scope_module_key" ON final_accounts.fa_sync_checkpoints ("CompanyId", "StoreGroupId", "StoreId", "Module", "CheckpointKey");

CREATE INDEX IF NOT EXISTS "IX_fa_sync_checkpoints_scope_module_updated" ON final_accounts.fa_sync_checkpoints ("CompanyId", "StoreGroupId", "StoreId", "Module", "UpdatedAt");

CREATE INDEX IF NOT EXISTS "IX_fa_sync_exceptions_scope_resolved_created" ON final_accounts.fa_sync_exceptions ("CompanyId", "StoreGroupId", "StoreId", "Resolved", "CreatedAt");

CREATE INDEX IF NOT EXISTS "IX_fa_sync_exceptions_job_item" ON final_accounts.fa_sync_exceptions ("JobId", "JobItemId");

CREATE INDEX IF NOT EXISTS "IX_fa_sync_exceptions_source_resolved" ON final_accounts.fa_sync_exceptions ("SourceType", "SourceId", "Resolved");

CREATE TABLE IF NOT EXISTS final_accounts.fa_ca_adjustment_batches (
    "Id" uuid NOT NULL,
    "CreatedAt" timestamp without time zone NOT NULL,
    "UpdatedAt" timestamp without time zone,
    "Synced" boolean NOT NULL,
    "Deleted" boolean NOT NULL,
    "CompanyId" uuid,
    "StoreGroupId" uuid,
    "StoreId" uuid,
    "BatchNumber" character varying(64) NOT NULL,
    "Title" character varying(160) NOT NULL,
    "Description" character varying(1000),
    "AdjustmentDate" timestamp without time zone NOT NULL,
    "FiscalPeriodId" uuid,
    "Status" integer NOT NULL,
    "AutoReverse" boolean NOT NULL,
    "AutoReverseDate" timestamp without time zone,
    "ReferenceNumber" character varying(120),
    "JournalEntryId" uuid,
    "ReversalJournalEntryId" uuid,
    "SubmittedAt" timestamp without time zone,
    "SubmittedBy" character varying(120),
    "ReviewedAt" timestamp without time zone,
    "ReviewedBy" character varying(120),
    "ApprovedAt" timestamp without time zone,
    "ApprovedBy" character varying(120),
    "RejectedAt" timestamp without time zone,
    "RejectedBy" character varying(120),
    "PostedAt" timestamp without time zone,
    "PostedBy" character varying(120),
    "ReversedAt" timestamp without time zone,
    "ReversedBy" character varying(120),
    "DecisionNotes" character varying(1000),
    "Revision" integer NOT NULL,
    "CreatedBy" character varying(120),
    "UpdatedBy" character varying(120),
    CONSTRAINT "PK_fa_ca_adjustment_batches" PRIMARY KEY ("Id")
);

CREATE TABLE IF NOT EXISTS final_accounts.fa_report_versions (
    "Id" uuid NOT NULL,
    "CreatedAt" timestamp without time zone NOT NULL,
    "UpdatedAt" timestamp without time zone,
    "Synced" boolean NOT NULL,
    "Deleted" boolean NOT NULL,
    "CompanyId" uuid,
    "StoreGroupId" uuid,
    "StoreId" uuid,
    "ReportType" character varying(80) NOT NULL,
    "VersionKind" integer NOT NULL,
    "PeriodFrom" timestamp without time zone,
    "PeriodTo" timestamp without time zone,
    "GeneratedAt" timestamp without time zone NOT NULL,
    "GeneratedBy" character varying(120),
    "Notes" character varying(500),
    "Revision" integer NOT NULL,
    CONSTRAINT "PK_fa_report_versions" PRIMARY KEY ("Id")
);

CREATE TABLE IF NOT EXISTS final_accounts.fa_statement_line_comments (
    "Id" uuid NOT NULL,
    "CreatedAt" timestamp without time zone NOT NULL,
    "UpdatedAt" timestamp without time zone,
    "Synced" boolean NOT NULL,
    "Deleted" boolean NOT NULL,
    "CompanyId" uuid,
    "StoreGroupId" uuid,
    "StoreId" uuid,
    "StatementType" character varying(80) NOT NULL,
    "StatementLineKey" character varying(120) NOT NULL,
    "ReportVersion" integer NOT NULL,
    "PeriodFrom" timestamp without time zone,
    "PeriodTo" timestamp without time zone,
    "Body" character varying(2000) NOT NULL,
    "CreatedBy" character varying(120),
    "UpdatedBy" character varying(120),
    "Revision" integer NOT NULL,
    CONSTRAINT "PK_fa_statement_line_comments" PRIMARY KEY ("Id")
);

CREATE TABLE IF NOT EXISTS final_accounts.fa_ca_adjustment_attachments (
    "Id" uuid NOT NULL,
    "CreatedAt" timestamp without time zone NOT NULL,
    "UpdatedAt" timestamp without time zone,
    "Synced" boolean NOT NULL,
    "Deleted" boolean NOT NULL,
    "CompanyId" uuid,
    "StoreGroupId" uuid,
    "StoreId" uuid,
    "AdjustmentBatchId" uuid NOT NULL,
    "FileName" character varying(260) NOT NULL,
    "ContentType" character varying(120),
    "StorageReference" character varying(500) NOT NULL,
    "Notes" character varying(500),
    "UploadedBy" character varying(120),
    "Revision" integer NOT NULL,
    CONSTRAINT "PK_fa_ca_adjustment_attachments" PRIMARY KEY ("Id"),
    CONSTRAINT "FK_fa_ca_adjustment_attachments_batch" FOREIGN KEY ("AdjustmentBatchId") REFERENCES final_accounts.fa_ca_adjustment_batches ("Id") ON DELETE RESTRICT
);

CREATE TABLE IF NOT EXISTS final_accounts.fa_ca_adjustment_comments (
    "Id" uuid NOT NULL,
    "CreatedAt" timestamp without time zone NOT NULL,
    "UpdatedAt" timestamp without time zone,
    "Synced" boolean NOT NULL,
    "Deleted" boolean NOT NULL,
    "CompanyId" uuid,
    "StoreGroupId" uuid,
    "StoreId" uuid,
    "AdjustmentBatchId" uuid NOT NULL,
    "Body" character varying(2000) NOT NULL,
    "Visibility" character varying(40) NOT NULL,
    "CreatedBy" character varying(120),
    "Revision" integer NOT NULL,
    CONSTRAINT "PK_fa_ca_adjustment_comments" PRIMARY KEY ("Id"),
    CONSTRAINT "FK_fa_ca_adjustment_comments_batch" FOREIGN KEY ("AdjustmentBatchId") REFERENCES final_accounts.fa_ca_adjustment_batches ("Id") ON DELETE RESTRICT
);

CREATE TABLE IF NOT EXISTS final_accounts.fa_ca_adjustment_lines (
    "Id" uuid NOT NULL,
    "CreatedAt" timestamp without time zone NOT NULL,
    "UpdatedAt" timestamp without time zone,
    "Synced" boolean NOT NULL,
    "Deleted" boolean NOT NULL,
    "CompanyId" uuid,
    "StoreGroupId" uuid,
    "StoreId" uuid,
    "AdjustmentBatchId" uuid NOT NULL,
    "AccountId" uuid NOT NULL,
    "LineNumber" integer NOT NULL,
    "Debit" numeric(18,2) NOT NULL,
    "Credit" numeric(18,2) NOT NULL,
    "Narration" character varying(500),
    "StatementLineKey" character varying(120),
    "Revision" integer NOT NULL,
    "CreatedBy" character varying(120),
    "UpdatedBy" character varying(120),
    CONSTRAINT "PK_fa_ca_adjustment_lines" PRIMARY KEY ("Id"),
    CONSTRAINT "CK_fa_ca_adjustment_lines_non_negative" CHECK ("Debit" >= 0 AND "Credit" >= 0),
    CONSTRAINT "CK_fa_ca_adjustment_lines_single_side" CHECK ((("Debit" > 0 AND "Credit" = 0) OR ("Credit" > 0 AND "Debit" = 0))),
    CONSTRAINT "FK_fa_ca_adjustment_lines_account" FOREIGN KEY ("AccountId") REFERENCES final_accounts.fa_accounts ("Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_fa_ca_adjustment_lines_batch" FOREIGN KEY ("AdjustmentBatchId") REFERENCES final_accounts.fa_ca_adjustment_batches ("Id") ON DELETE RESTRICT
);

CREATE UNIQUE INDEX IF NOT EXISTS "IX_fa_ca_adjustment_batches_scope_number" ON final_accounts.fa_ca_adjustment_batches ("CompanyId", "StoreGroupId", "StoreId", "BatchNumber");

CREATE INDEX IF NOT EXISTS "IX_fa_ca_adjustment_batches_scope_status_date" ON final_accounts.fa_ca_adjustment_batches ("CompanyId", "StoreGroupId", "StoreId", "Status", "AdjustmentDate");

CREATE INDEX IF NOT EXISTS "IX_fa_ca_adjustment_batches_journal" ON final_accounts.fa_ca_adjustment_batches ("JournalEntryId");

CREATE INDEX IF NOT EXISTS "IX_fa_ca_adjustment_batches_reversal_journal" ON final_accounts.fa_ca_adjustment_batches ("ReversalJournalEntryId");

CREATE UNIQUE INDEX IF NOT EXISTS "IX_fa_ca_adjustment_lines_batch_line" ON final_accounts.fa_ca_adjustment_lines ("AdjustmentBatchId", "LineNumber");

CREATE INDEX IF NOT EXISTS "IX_fa_ca_adjustment_lines_scope_account" ON final_accounts.fa_ca_adjustment_lines ("CompanyId", "StoreGroupId", "StoreId", "AccountId");

CREATE INDEX IF NOT EXISTS "IX_fa_ca_adjustment_comments_batch_created" ON final_accounts.fa_ca_adjustment_comments ("AdjustmentBatchId", "CreatedAt");

CREATE INDEX IF NOT EXISTS "IX_fa_ca_adjustment_attachments_batch_created" ON final_accounts.fa_ca_adjustment_attachments ("AdjustmentBatchId", "CreatedAt");

CREATE INDEX IF NOT EXISTS "IX_fa_statement_line_comments_scope_line" ON final_accounts.fa_statement_line_comments ("CompanyId", "StoreGroupId", "StoreId", "StatementType", "StatementLineKey", "ReportVersion");

CREATE INDEX IF NOT EXISTS "IX_fa_report_versions_scope_type" ON final_accounts.fa_report_versions ("CompanyId", "StoreGroupId", "StoreId", "ReportType", "VersionKind", "GeneratedAt");

CREATE TABLE IF NOT EXISTS final_accounts.fa_close_runs (
    "Id" uuid NOT NULL,
    "CreatedAt" timestamp without time zone NOT NULL,
    "UpdatedAt" timestamp without time zone,
    "Synced" boolean NOT NULL,
    "Deleted" boolean NOT NULL,
    "CompanyId" uuid,
    "StoreGroupId" uuid,
    "StoreId" uuid,
    "RunNumber" character varying(64) NOT NULL,
    "CloseType" integer NOT NULL,
    "Status" integer NOT NULL,
    "FiscalYearId" uuid NOT NULL,
    "FiscalPeriodId" uuid,
    "PeriodStart" timestamp without time zone NOT NULL,
    "PeriodEnd" timestamp without time zone NOT NULL,
    "CloseDate" timestamp without time zone NOT NULL,
    "ChecklistStatus" character varying(32) NOT NULL,
    "ReconciliationStatus" character varying(32) NOT NULL,
    "PendingPostingCount" integer NOT NULL,
    "TrialBalanceStatus" character varying(32) NOT NULL,
    "BalanceSheetStatus" character varying(32) NOT NULL,
    "InventorySnapshotTotal" numeric(18,2) NOT NULL,
    "ProfitAfterTax" numeric(18,2) NOT NULL,
    "CurrentYearResultTransferStatus" character varying(32) NOT NULL,
    "OpeningJournalStatus" character varying(32) NOT NULL,
    "ReportSnapshotStatus" character varying(32) NOT NULL,
    "FinancialYearLockId" uuid,
    "ClosedAt" timestamp without time zone,
    "ClosedBy" character varying(120),
    "ReopenRequestedAt" timestamp without time zone,
    "ReopenRequestedBy" character varying(120),
    "ReopenedAt" timestamp without time zone,
    "ReopenedBy" character varying(120),
    "ReopenReason" character varying(1000),
    "ApprovalNotes" character varying(1000),
    "Revision" integer NOT NULL,
    "CreatedBy" character varying(120),
    "UpdatedBy" character varying(120),
    CONSTRAINT "PK_fa_close_runs" PRIMARY KEY ("Id")
);

CREATE TABLE IF NOT EXISTS final_accounts.fa_close_balance_snapshots (
    "Id" uuid NOT NULL,
    "CreatedAt" timestamp without time zone NOT NULL,
    "UpdatedAt" timestamp without time zone,
    "Synced" boolean NOT NULL,
    "Deleted" boolean NOT NULL,
    "CloseRunId" uuid NOT NULL,
    "AccountId" uuid NOT NULL,
    "AccountCode" character varying(40) NOT NULL,
    "AccountName" character varying(160) NOT NULL,
    "AccountType" character varying(40) NOT NULL,
    "ClosingBalance" numeric(18,2) NOT NULL,
    "OpeningBalance" numeric(18,2) NOT NULL,
    "Revision" integer NOT NULL,
    CONSTRAINT "PK_fa_close_balance_snapshots" PRIMARY KEY ("Id")
);

CREATE TABLE IF NOT EXISTS final_accounts.fa_close_checklist_items (
    "Id" uuid NOT NULL,
    "CreatedAt" timestamp without time zone NOT NULL,
    "UpdatedAt" timestamp without time zone,
    "Synced" boolean NOT NULL,
    "Deleted" boolean NOT NULL,
    "CloseRunId" uuid NOT NULL,
    "Key" character varying(80) NOT NULL,
    "Label" character varying(160) NOT NULL,
    "Required" boolean NOT NULL,
    "Status" character varying(32) NOT NULL,
    "Detail" character varying(1000) NOT NULL,
    "Amount" numeric(18,2),
    "SortOrder" integer NOT NULL,
    "Revision" integer NOT NULL,
    CONSTRAINT "PK_fa_close_checklist_items" PRIMARY KEY ("Id")
);

CREATE TABLE IF NOT EXISTS final_accounts.fa_close_report_snapshots (
    "Id" uuid NOT NULL,
    "CreatedAt" timestamp without time zone NOT NULL,
    "UpdatedAt" timestamp without time zone,
    "Synced" boolean NOT NULL,
    "Deleted" boolean NOT NULL,
    "CloseRunId" uuid NOT NULL,
    "ReportType" character varying(80) NOT NULL,
    "ReportVersion" integer NOT NULL,
    "PeriodFrom" timestamp without time zone,
    "PeriodTo" timestamp without time zone,
    "Status" character varying(32) NOT NULL,
    "PayloadJson" text NOT NULL,
    "PayloadHash" character varying(128) NOT NULL,
    "Revision" integer NOT NULL,
    CONSTRAINT "PK_fa_close_report_snapshots" PRIMARY KEY ("Id")
);

CREATE UNIQUE INDEX IF NOT EXISTS "IX_fa_close_runs_scope_number" ON final_accounts.fa_close_runs ("CompanyId", "StoreGroupId", "StoreId", "RunNumber");

CREATE INDEX IF NOT EXISTS "IX_fa_close_runs_scope_period_status" ON final_accounts.fa_close_runs ("CompanyId", "StoreGroupId", "StoreId", "FiscalYearId", "FiscalPeriodId", "CloseType", "Status");

CREATE INDEX IF NOT EXISTS "IX_fa_close_runs_financial_year_lock" ON final_accounts.fa_close_runs ("FinancialYearLockId");

CREATE UNIQUE INDEX IF NOT EXISTS "IX_fa_close_checklist_items_run_key" ON final_accounts.fa_close_checklist_items ("CloseRunId", "Key");

CREATE UNIQUE INDEX IF NOT EXISTS "IX_fa_close_report_snapshots_run_type" ON final_accounts.fa_close_report_snapshots ("CloseRunId", "ReportType");

CREATE UNIQUE INDEX IF NOT EXISTS "IX_fa_close_balance_snapshots_run_account" ON final_accounts.fa_close_balance_snapshots ("CloseRunId", "AccountId");

CREATE TABLE IF NOT EXISTS final_accounts.fa_projection_scenarios (
    "Id" uuid NOT NULL,
    "CreatedAt" timestamp without time zone NOT NULL,
    "UpdatedAt" timestamp without time zone,
    "Synced" boolean NOT NULL,
    "Deleted" boolean NOT NULL,
    "CompanyId" uuid,
    "StoreGroupId" uuid,
    "StoreId" uuid,
    "ScenarioNumber" character varying(64) NOT NULL,
    "Name" character varying(160) NOT NULL,
    "Description" character varying(1000),
    "ScenarioType" integer NOT NULL,
    "Status" integer NOT NULL,
    "BaselineSource" integer NOT NULL,
    "BaselineFrom" timestamp without time zone,
    "BaselineTo" timestamp without time zone,
    "ProjectionStart" timestamp without time zone NOT NULL,
    "HorizonMonths" integer NOT NULL,
    "ActiveAssumptionVersion" integer NOT NULL,
    "BaselineMonthlyRevenue" numeric(18,2) NOT NULL,
    "BaselineMonthlyGrossProfit" numeric(18,2) NOT NULL,
    "BaselineMonthlyProfitAfterTax" numeric(18,2) NOT NULL,
    "BaselineCash" numeric(18,2) NOT NULL,
    "BaselineInventory" numeric(18,2) NOT NULL,
    "BaselineDebtors" numeric(18,2) NOT NULL,
    "BaselineCreditors" numeric(18,2) NOT NULL,
    "BaselineFixedAssets" numeric(18,2) NOT NULL,
    "BaselineDebt" numeric(18,2) NOT NULL,
    "BaselineCapital" numeric(18,2) NOT NULL,
    "SubmittedAt" timestamp without time zone,
    "SubmittedBy" character varying(120),
    "ApprovedAt" timestamp without time zone,
    "ApprovedBy" character varying(120),
    "ArchivedAt" timestamp without time zone,
    "ArchivedBy" character varying(120),
    "DecisionNotes" character varying(1000),
    "Revision" integer NOT NULL,
    "CreatedBy" character varying(120),
    "UpdatedBy" character varying(120),
    CONSTRAINT "PK_fa_projection_scenarios" PRIMARY KEY ("Id")
);

CREATE TABLE IF NOT EXISTS final_accounts.fa_projection_assumption_versions (
    "Id" uuid NOT NULL,
    "CreatedAt" timestamp without time zone NOT NULL,
    "UpdatedAt" timestamp without time zone,
    "Synced" boolean NOT NULL,
    "Deleted" boolean NOT NULL,
    "CompanyId" uuid,
    "StoreGroupId" uuid,
    "StoreId" uuid,
    "ScenarioId" uuid NOT NULL,
    "Version" integer NOT NULL,
    "IsActive" boolean NOT NULL,
    "RevenueGrowthPercent" numeric(9,4) NOT NULL,
    "SeasonalityFactorsCsv" character varying(500) NOT NULL,
    "NewStoreMonthlyRevenue" numeric(18,2) NOT NULL,
    "AverageBillValue" numeric(18,2) NOT NULL,
    "CustomerCountGrowthPercent" numeric(9,4) NOT NULL,
    "ReturnsDiscountPercent" numeric(9,4) NOT NULL,
    "GrossMarginPercent" numeric(9,4) NOT NULL,
    "PurchaseInflationPercent" numeric(9,4) NOT NULL,
    "InventoryDays" numeric(9,2) NOT NULL,
    "DebtorDays" numeric(9,2) NOT NULL,
    "CreditorDays" numeric(9,2) NOT NULL,
    "EmployeeCostMonthly" numeric(18,2) NOT NULL,
    "SalaryGrowthPercent" numeric(9,4) NOT NULL,
    "RentExpenseMonthly" numeric(18,2) NOT NULL,
    "ExpenseEscalationPercent" numeric(9,4) NOT NULL,
    "CapexMonthly" numeric(18,2) NOT NULL,
    "DepreciationRatePercent" numeric(9,4) NOT NULL,
    "DebtOpening" numeric(18,2) NOT NULL,
    "InterestRatePercent" numeric(9,4) NOT NULL,
    "DebtRepaymentMonthly" numeric(18,2) NOT NULL,
    "CapitalInjectionMonthly" numeric(18,2) NOT NULL,
    "DrawingsMonthly" numeric(18,2) NOT NULL,
    "TaxRatePercent" numeric(9,4) NOT NULL,
    "MinimumCash" numeric(18,2) NOT NULL,
    "Notes" character varying(1000),
    "CreatedBy" character varying(120),
    "Revision" integer NOT NULL,
    CONSTRAINT "PK_fa_projection_assumption_versions" PRIMARY KEY ("Id")
);

CREATE TABLE IF NOT EXISTS final_accounts.fa_projection_months (
    "Id" uuid NOT NULL,
    "CreatedAt" timestamp without time zone NOT NULL,
    "UpdatedAt" timestamp without time zone,
    "Synced" boolean NOT NULL,
    "Deleted" boolean NOT NULL,
    "ScenarioId" uuid NOT NULL,
    "AssumptionVersion" integer NOT NULL,
    "MonthNumber" integer NOT NULL,
    "MonthStart" timestamp without time zone NOT NULL,
    "MonthEnd" timestamp without time zone NOT NULL,
    "Revenue" numeric(18,2) NOT NULL,
    "ReturnsAndDiscounts" numeric(18,2) NOT NULL,
    "NetRevenue" numeric(18,2) NOT NULL,
    "CostOfGoodsSold" numeric(18,2) NOT NULL,
    "GrossProfit" numeric(18,2) NOT NULL,
    "PayrollExpense" numeric(18,2) NOT NULL,
    "RentExpense" numeric(18,2) NOT NULL,
    "OtherExpense" numeric(18,2) NOT NULL,
    "Depreciation" numeric(18,2) NOT NULL,
    "Interest" numeric(18,2) NOT NULL,
    "Tax" numeric(18,2) NOT NULL,
    "ProfitAfterTax" numeric(18,2) NOT NULL,
    "InventoryBalance" numeric(18,2) NOT NULL,
    "DebtorBalance" numeric(18,2) NOT NULL,
    "CreditorBalance" numeric(18,2) NOT NULL,
    "FixedAssets" numeric(18,2) NOT NULL,
    "DebtBalance" numeric(18,2) NOT NULL,
    "CapitalBalance" numeric(18,2) NOT NULL,
    "CashBalance" numeric(18,2) NOT NULL,
    "TotalAssets" numeric(18,2) NOT NULL,
    "TotalLiabilitiesEquity" numeric(18,2) NOT NULL,
    "BalanceDifference" numeric(18,2) NOT NULL,
    "OperatingCashFlow" numeric(18,2) NOT NULL,
    "InvestingCashFlow" numeric(18,2) NOT NULL,
    "FinancingCashFlow" numeric(18,2) NOT NULL,
    "ClosingCashFlow" numeric(18,2) NOT NULL,
    "WorkingCapitalRequirement" numeric(18,2) NOT NULL,
    "BreakEvenRevenue" numeric(18,2) NOT NULL,
    "CurrentRatio" numeric(18,4) NOT NULL,
    "DebtEquityRatio" numeric(18,4) NOT NULL,
    "Revision" integer NOT NULL,
    CONSTRAINT "PK_fa_projection_months" PRIMARY KEY ("Id")
);

CREATE UNIQUE INDEX IF NOT EXISTS "IX_fa_projection_scenarios_scope_number" ON final_accounts.fa_projection_scenarios ("CompanyId", "StoreGroupId", "StoreId", "ScenarioNumber");

CREATE INDEX IF NOT EXISTS "IX_fa_projection_scenarios_scope_status_type" ON final_accounts.fa_projection_scenarios ("CompanyId", "StoreGroupId", "StoreId", "Status", "ScenarioType");

CREATE UNIQUE INDEX IF NOT EXISTS "IX_fa_projection_assumptions_scenario_version" ON final_accounts.fa_projection_assumption_versions ("ScenarioId", "Version");

CREATE INDEX IF NOT EXISTS "IX_fa_projection_assumptions_scope_active" ON final_accounts.fa_projection_assumption_versions ("CompanyId", "StoreGroupId", "StoreId", "IsActive");

CREATE UNIQUE INDEX IF NOT EXISTS "IX_fa_projection_months_scenario_month" ON final_accounts.fa_projection_months ("ScenarioId", "MonthNumber");

CREATE INDEX IF NOT EXISTS "IX_fa_projection_months_scenario_start" ON final_accounts.fa_projection_months ("ScenarioId", "MonthStart");

CREATE TABLE IF NOT EXISTS final_accounts.fa_tally_profiles (
    "Id" uuid NOT NULL,
    "CreatedAt" timestamp without time zone NOT NULL,
    "UpdatedAt" timestamp without time zone,
    "Synced" boolean NOT NULL,
    "Deleted" boolean NOT NULL,
    "CompanyId" uuid,
    "StoreGroupId" uuid,
    "StoreId" uuid,
    "ProfileCode" character varying(64) NOT NULL,
    "Name" character varying(160) NOT NULL,
    "TallyRelease" character varying(80) NOT NULL,
    "TestCompanyName" character varying(160) NOT NULL,
    "BaseCurrency" character varying(16) NOT NULL,
    "Country" character varying(80) NOT NULL,
    "GstRegistrationType" character varying(80),
    "DuplicatePolicy" integer NOT NULL,
    "DirectPostingAllowed" boolean NOT NULL,
    "GroupMappingJson" text NOT NULL,
    "LedgerMappingJson" text NOT NULL,
    "VoucherTypeMappingJson" text NOT NULL,
    "TaxMappingJson" text NOT NULL,
    "StockCostCentreMappingJson" text NOT NULL,
    "IsActive" boolean NOT NULL,
    "Notes" character varying(1000),
    "Revision" integer NOT NULL,
    "CreatedBy" character varying(120),
    "UpdatedBy" character varying(120),
    CONSTRAINT "PK_fa_tally_profiles" PRIMARY KEY ("Id")
);

CREATE TABLE IF NOT EXISTS final_accounts.fa_exchange_runs (
    "Id" uuid NOT NULL,
    "CreatedAt" timestamp without time zone NOT NULL,
    "UpdatedAt" timestamp without time zone,
    "Synced" boolean NOT NULL,
    "Deleted" boolean NOT NULL,
    "CompanyId" uuid,
    "StoreGroupId" uuid,
    "StoreId" uuid,
    "RunNumber" character varying(64) NOT NULL,
    "RunKind" integer NOT NULL,
    "Status" integer NOT NULL,
    "TallyProfileId" uuid,
    "PeriodFrom" timestamp without time zone,
    "PeriodTo" timestamp without time zone,
    "AsOf" timestamp without time zone,
    "Format" character varying(24) NOT NULL,
    "MasterCount" integer NOT NULL,
    "VoucherCount" integer NOT NULL,
    "ExceptionCount" integer NOT NULL,
    "ControlDebit" numeric(18,2) NOT NULL,
    "ControlCredit" numeric(18,2) NOT NULL,
    "PayloadHash" character varying(128) NOT NULL,
    "ZipChecksum" character varying(128),
    "FileName" character varying(260),
    "GeneratedAt" timestamp without time zone,
    "GeneratedBy" character varying(120),
    "Notes" character varying(1000),
    "Revision" integer NOT NULL,
    "CreatedBy" character varying(120),
    "UpdatedBy" character varying(120),
    CONSTRAINT "PK_fa_exchange_runs" PRIMARY KEY ("Id")
);

CREATE TABLE IF NOT EXISTS final_accounts.fa_exchange_exceptions (
    "Id" uuid NOT NULL,
    "CreatedAt" timestamp without time zone NOT NULL,
    "UpdatedAt" timestamp without time zone,
    "Synced" boolean NOT NULL,
    "Deleted" boolean NOT NULL,
    "ExchangeRunId" uuid NOT NULL,
    "Severity" character varying(24) NOT NULL,
    "Code" character varying(80) NOT NULL,
    "Message" character varying(1000) NOT NULL,
    "SourceType" character varying(80),
    "SourceId" uuid,
    "Resolved" boolean NOT NULL,
    "Revision" integer NOT NULL,
    CONSTRAINT "PK_fa_exchange_exceptions" PRIMARY KEY ("Id")
);

CREATE UNIQUE INDEX IF NOT EXISTS "IX_fa_tally_profiles_scope_code" ON final_accounts.fa_tally_profiles ("CompanyId", "StoreGroupId", "StoreId", "ProfileCode");

CREATE INDEX IF NOT EXISTS "IX_fa_tally_profiles_scope_active" ON final_accounts.fa_tally_profiles ("CompanyId", "StoreGroupId", "StoreId", "IsActive");

CREATE UNIQUE INDEX IF NOT EXISTS "IX_fa_exchange_runs_scope_number" ON final_accounts.fa_exchange_runs ("CompanyId", "StoreGroupId", "StoreId", "RunNumber");

CREATE INDEX IF NOT EXISTS "IX_fa_exchange_runs_scope_kind_created" ON final_accounts.fa_exchange_runs ("CompanyId", "StoreGroupId", "StoreId", "RunKind", "CreatedAt");

CREATE INDEX IF NOT EXISTS "IX_fa_exchange_runs_tally_profile" ON final_accounts.fa_exchange_runs ("TallyProfileId");

CREATE INDEX IF NOT EXISTS "IX_fa_exchange_exceptions_run_code" ON final_accounts.fa_exchange_exceptions ("ExchangeRunId", "Code");

CREATE INDEX IF NOT EXISTS "IX_fa_exchange_exceptions_source_resolved" ON final_accounts.fa_exchange_exceptions ("SourceType", "SourceId", "Resolved");
""", cancellationToken);

    logger.LogInformation("Final Accounts storage repair check completed.");
}

public static async Task RepairKnownSchemaDriftAsync(GarmetixDbContext db, ILogger logger, CancellationToken cancellationToken = default)
    {
        try
        {
            await RepairGstReturnStorageAsync(db, logger, cancellationToken);
            await RepairGstTaxStorageAsync(db, logger, cancellationToken);
            await RepairPosHeldBillStorageAsync(db, logger, cancellationToken);
            await RepairCashVoucherConversionStorageAsync(db, logger, cancellationToken);
            await RepairStoreDayStorageAsync(db, logger, cancellationToken);
            await RepairDotMatrixPrintStorageAsync(db, logger, cancellationToken);
            await RepairHrEmployeeMasterAndBenefitsAsync(db, logger, cancellationToken);
            await RepairAttendanceCoreStorageAsync(db, logger, cancellationToken);
            await RepairDigitalBillCrmStorageAsync(db, logger, cancellationToken);
            await RepairFinalAccountsStorageAsync(db, logger, cancellationToken);

            await db.Database.ExecuteSqlRawAsync("""
                CREATE TABLE IF NOT EXISTS "FinancialYearLocks" (
                    "Id" uuid NOT NULL,
                    "CreatedAt" timestamp without time zone NOT NULL DEFAULT now(),
                    "UpdatedAt" timestamp without time zone NULL,
                    "Synced" boolean NOT NULL DEFAULT false,
                    "Deleted" boolean NOT NULL DEFAULT false,
                    "CompanyId" uuid NOT NULL,
                    "CreatedBy" text NULL,
                    "FinancialYear" text NOT NULL DEFAULT '',
                    "PeriodStart" timestamp without time zone NOT NULL,
                    "PeriodEnd" timestamp without time zone NOT NULL,
                    "StoreGroupId" uuid NULL,
                    "StoreId" uuid NULL,
                    "LockAccounting" boolean NOT NULL DEFAULT true,
                    "LockSales" boolean NOT NULL DEFAULT true,
                    "LockPurchase" boolean NOT NULL DEFAULT true,
                    "LockInventory" boolean NOT NULL DEFAULT true,
                    "LockGst" boolean NOT NULL DEFAULT true,
                    "Active" boolean NOT NULL DEFAULT true,
                    "LockedAt" timestamp without time zone NULL,
                    "LockedBy" text NULL,
                    "LockReason" text NULL,
                    "UnlockedAt" timestamp without time zone NULL,
                    "UnlockedBy" text NULL,
                    "UnlockReason" text NULL,
                    CONSTRAINT "PK_FinancialYearLocks" PRIMARY KEY ("Id")
                );
                CREATE INDEX IF NOT EXISTS "IX_FinancialYearLocks_CompanyId_FinancialYear_PeriodStart_PeriodEnd"
                    ON "FinancialYearLocks" ("CompanyId", "FinancialYear", "PeriodStart", "PeriodEnd");
                CREATE INDEX IF NOT EXISTS "IX_FinancialYearLocks_CompanyId_StoreGroupId_StoreId_Active"
                    ON "FinancialYearLocks" ("CompanyId", "StoreGroupId", "StoreId", "Active");
                """, cancellationToken);

            await db.Database.ExecuteSqlRawAsync("""
                CREATE TABLE IF NOT EXISTS "AuditLogEntries" (
                    "Id" uuid NOT NULL,
                    "CreatedAt" timestamp without time zone NOT NULL DEFAULT now(),
                    "UpdatedAt" timestamp without time zone NULL,
                    "Synced" boolean NOT NULL DEFAULT false,
                    "Deleted" boolean NOT NULL DEFAULT false,
                    "OccurredAt" timestamp without time zone NOT NULL DEFAULT now(),
                    "Action" text NOT NULL DEFAULT '',
                    "Module" text NOT NULL DEFAULT '',
                    "EntityName" text NOT NULL DEFAULT '',
                    "EntityDisplayName" text NOT NULL DEFAULT '',
                    "EntityId" uuid NOT NULL,
                    "Reference" text NOT NULL DEFAULT '',
                    "CompanyId" uuid NULL,
                    "StoreGroupId" uuid NULL,
                    "StoreId" uuid NULL,
                    "UserId" uuid NULL,
                    "UserName" text NULL,
                    "Source" text NOT NULL DEFAULT 'SaveChanges',
                    "RequestMethod" text NULL,
                    "RequestPath" text NULL,
                    "IpAddress" text NULL,
                    "Reason" text NULL,
                    "BeforeJson" text NULL,
                    "AfterJson" text NULL,
                    "ChangesJson" text NULL,
                    "ChangedFieldCount" integer NOT NULL DEFAULT 0,
                    "TraceIdentifier" text NULL,
                    CONSTRAINT "PK_AuditLogEntries" PRIMARY KEY ("Id")
                );
                CREATE INDEX IF NOT EXISTS "IX_AuditLogEntries_OccurredAt" ON "AuditLogEntries" ("OccurredAt");
                CREATE INDEX IF NOT EXISTS "IX_AuditLogEntries_CompanyId_StoreId_OccurredAt" ON "AuditLogEntries" ("CompanyId", "StoreId", "OccurredAt");
                CREATE INDEX IF NOT EXISTS "IX_AuditLogEntries_EntityName_EntityId" ON "AuditLogEntries" ("EntityName", "EntityId");
                CREATE INDEX IF NOT EXISTS "IX_AuditLogEntries_Module_Action_OccurredAt" ON "AuditLogEntries" ("Module", "Action", "OccurredAt");
                """, cancellationToken);

            // Some development databases may already have the migration recorded in
            // __EFMigrationsHistory but can still be missing columns when older ZIPs were
            // tested in between. These statements are idempotent and only add missing columns.
            await db.Database.ExecuteSqlRawAsync("""
                ALTER TABLE "Users" ADD COLUMN IF NOT EXISTS "IsActive" boolean NOT NULL DEFAULT true;
                ALTER TABLE "Users" ADD COLUMN IF NOT EXISTS "IsSuperAdmin" boolean NOT NULL DEFAULT false;
                ALTER TABLE "Users" ADD COLUMN IF NOT EXISTS "PinHash" text NOT NULL DEFAULT '';
                ALTER TABLE "Users" ADD COLUMN IF NOT EXISTS "RemoteUserId" uuid NULL;
                ALTER TABLE "Users" ADD COLUMN IF NOT EXISTS "AppOperation" integer NOT NULL DEFAULT 0;
                UPDATE "Users" SET "Admin" = ("Role" = 0) WHERE "Admin" IS DISTINCT FROM ("Role" = 0);
                UPDATE "Users" SET "IsSuperAdmin" = true WHERE "UserName" = 'garmetix' OR "Admin" = true;

                ALTER TABLE "Customers" ADD COLUMN IF NOT EXISTS "GSTLegalName" text NULL;
                ALTER TABLE "Customers" ADD COLUMN IF NOT EXISTS "GSTTradeName" text NULL;
                ALTER TABLE "Customers" ADD COLUMN IF NOT EXISTS "GSTPrincipalAddress" text NULL;
                ALTER TABLE "Customers" ADD COLUMN IF NOT EXISTS "GSTStateCode" text NULL;
                ALTER TABLE "Customers" ADD COLUMN IF NOT EXISTS "GSTTaxpayerType" text NULL;
                ALTER TABLE "Customers" ADD COLUMN IF NOT EXISTS "GSTRegistrationStatus" text NULL;
                ALTER TABLE "Customers" ADD COLUMN IF NOT EXISTS "GSTVerified" boolean NOT NULL DEFAULT false;
                ALTER TABLE "Customers" ADD COLUMN IF NOT EXISTS "GSTVerifiedAt" timestamp without time zone NULL;
                ALTER TABLE "Customers" ADD COLUMN IF NOT EXISTS "GSTLookupSource" text NULL;
                ALTER TABLE "Customers" ADD COLUMN IF NOT EXISTS "GSTMismatchAlert" text NULL;
                ALTER TABLE "Customers" ADD COLUMN IF NOT EXISTS "CreditBalance" numeric(18,2) NOT NULL DEFAULT 0;
                ALTER TABLE "Customers" ADD COLUMN IF NOT EXISTS "LoyaltyPoints" numeric(18,2) NOT NULL DEFAULT 0;

                -- Stage 11D-94: v4.12.05 added SalesInvoices.Remarks for Vyapar import source metadata.
                -- Some deployed Docker volumes had AutoMigrate disabled or migration history drift, so
                -- sale list, recent sales, invoice replacement and Vyapar imported-list endpoints failed
                -- with PostgreSQL 42703 (column s.Remarks does not exist). Keep this repair idempotent.
                ALTER TABLE "SalesInvoices" ADD COLUMN IF NOT EXISTS "Remarks" text NULL;

                ALTER TABLE "Vendors" ADD COLUMN IF NOT EXISTS "GSTLegalName" text NULL;
                ALTER TABLE "Vendors" ADD COLUMN IF NOT EXISTS "GSTTradeName" text NULL;
                ALTER TABLE "Vendors" ADD COLUMN IF NOT EXISTS "GSTPrincipalAddress" text NULL;
                ALTER TABLE "Vendors" ADD COLUMN IF NOT EXISTS "GSTStateCode" text NULL;
                ALTER TABLE "Vendors" ADD COLUMN IF NOT EXISTS "GSTTaxpayerType" text NULL;
                ALTER TABLE "Vendors" ADD COLUMN IF NOT EXISTS "GSTRegistrationStatus" text NULL;
                ALTER TABLE "Vendors" ADD COLUMN IF NOT EXISTS "GSTVerified" boolean NOT NULL DEFAULT false;
                ALTER TABLE "Vendors" ADD COLUMN IF NOT EXISTS "GSTVerifiedAt" timestamp without time zone NULL;
                ALTER TABLE "Vendors" ADD COLUMN IF NOT EXISTS "GSTLookupSource" text NULL;
                ALTER TABLE "Vendors" ADD COLUMN IF NOT EXISTS "GSTMismatchAlert" text NULL;

                -- Stage 14Q.4: v6.2.2 added Vendor.VendorType (nullable enum, backed by an
                -- integer column) so tailoring/alteration vendors can be distinguished from
                -- purchase vendors. See migration 20260710120000_AddVendorType.cs.
                ALTER TABLE "Vendors" ADD COLUMN IF NOT EXISTS "VendorType" integer NULL;

                CREATE INDEX IF NOT EXISTS "IX_Customers_CompanyId_GSTIN" ON "Customers" ("CompanyId", "GSTIN");
                CREATE INDEX IF NOT EXISTS "IX_Vendors_CompanyId_GSTIN" ON "Vendors" ("CompanyId", "GSTIN");

                CREATE TABLE IF NOT EXISTS "Salesmen" (
                    "Id" uuid NOT NULL,
                    "Name" text NOT NULL DEFAULT 'Manager',
                    "EmployeeId" uuid NULL,
                    "Active" boolean NOT NULL DEFAULT true,
                    "CompanyId" uuid NOT NULL,
                    "CreatedBy" text NULL,
                    "StoreGroupId" uuid NOT NULL,
                    "StoreId" uuid NOT NULL,
                    "CreatedAt" timestamp without time zone NOT NULL DEFAULT now(),
                    "UpdatedAt" timestamp without time zone NULL,
                    "Synced" boolean NOT NULL DEFAULT false,
                    "Deleted" boolean NOT NULL DEFAULT false,
                    CONSTRAINT "PK_Salesmen" PRIMARY KEY ("Id")
                );
                CREATE INDEX IF NOT EXISTS "IX_Salesmen_CompanyId_StoreId_Name" ON "Salesmen" ("CompanyId", "StoreId", "Name");

                CREATE TABLE IF NOT EXISTS "GstReturnDrafts" (
                    "Id" uuid NOT NULL,
                    "CreatedAt" timestamp without time zone NOT NULL,
                    "UpdatedAt" timestamp without time zone NULL,
                    "Synced" boolean NOT NULL DEFAULT false,
                    "Deleted" boolean NOT NULL DEFAULT false,
                    "CompanyId" uuid NOT NULL,
                    "CreatedBy" text NULL,
                    "Form" text NOT NULL DEFAULT '',
                    "Gstin" text NOT NULL DEFAULT '',
                    "ReturnPeriod" text NOT NULL DEFAULT '',
                    "Title" text NOT NULL DEFAULT '',
                    "Status" text NOT NULL DEFAULT 'Draft',
                    "PayloadJson" text NOT NULL DEFAULT '{{}}',
                    "LastPreviewIssuesJson" text NOT NULL DEFAULT '[]',
                    "RowCount" integer NOT NULL DEFAULT 0,
                    "TaxableValue" numeric(18,2) NOT NULL DEFAULT 0,
                    "IntegratedTax" numeric(18,2) NOT NULL DEFAULT 0,
                    "CentralTax" numeric(18,2) NOT NULL DEFAULT 0,
                    "StateTax" numeric(18,2) NOT NULL DEFAULT 0,
                    "Cess" numeric(18,2) NOT NULL DEFAULT 0,
                    "CreatedByUserId" uuid NULL,
                    "CreatedByUserName" text NOT NULL DEFAULT '',
                    "UpdatedByUserId" uuid NULL,
                    "UpdatedByUserName" text NOT NULL DEFAULT '',
                    "FiledAt" timestamp without time zone NULL,
                    "LockedAt" timestamp without time zone NULL,
                    CONSTRAINT "PK_GstReturnDrafts" PRIMARY KEY ("Id")
                );

                CREATE TABLE IF NOT EXISTS "GstReturnAuditEntries" (
                    "Id" uuid NOT NULL,
                    "CreatedAt" timestamp without time zone NOT NULL,
                    "UpdatedAt" timestamp without time zone NULL,
                    "Synced" boolean NOT NULL DEFAULT false,
                    "Deleted" boolean NOT NULL DEFAULT false,
                    "CompanyId" uuid NOT NULL,
                    "CreatedBy" text NULL,
                    "DraftId" uuid NOT NULL,
                    "Form" text NOT NULL DEFAULT '',
                    "ReturnPeriod" text NOT NULL DEFAULT '',
                    "Gstin" text NOT NULL DEFAULT '',
                    "Action" text NOT NULL DEFAULT '',
                    "Summary" text NOT NULL DEFAULT '',
                    "ActorUserId" uuid NULL,
                    "ActorName" text NOT NULL DEFAULT '',
                    "DetailsJson" text NOT NULL DEFAULT '{{}}',
                    CONSTRAINT "PK_GstReturnAuditEntries" PRIMARY KEY ("Id")
                );

                CREATE TABLE IF NOT EXISTS "CommercialNotes" (
                    "Id" uuid NOT NULL,
                    "NoteNumber" text NOT NULL DEFAULT '',
                    "NoteType" integer NOT NULL DEFAULT 1,
                    "OnDate" timestamp without time zone NOT NULL,
                    "PartyType" integer NOT NULL DEFAULT 0,
                    "PartyId" uuid NULL,
                    "CustomerId" uuid NULL,
                    "VendorId" uuid NULL,
                    "PartyName" text NOT NULL DEFAULT '',
                    "PartyGstin" text NULL,
                    "SourceType" text NOT NULL DEFAULT 'Manual',
                    "SourceId" uuid NULL,
                    "SourceNumber" text NULL,
                    "Reason" text NOT NULL DEFAULT '',
                    "TaxableAmount" numeric(18,2) NOT NULL DEFAULT 0,
                    "TaxAmount" numeric(18,2) NOT NULL DEFAULT 0,
                    "Amount" numeric(18,2) NOT NULL DEFAULT 0,
                    "IsAdjusted" boolean NOT NULL DEFAULT false,
                    "AdjustedAmount" numeric(18,2) NOT NULL DEFAULT 0,
                    "Printed" boolean NOT NULL DEFAULT false,
                    "Remarks" text NULL,
                    "CompanyId" uuid NOT NULL,
                    "CreatedBy" text NULL,
                    "StoreGroupId" uuid NOT NULL,
                    "StoreId" uuid NOT NULL,
                    "CreatedAt" timestamp without time zone NOT NULL,
                    "UpdatedAt" timestamp without time zone NULL,
                    "Synced" boolean NOT NULL DEFAULT false,
                    "Deleted" boolean NOT NULL DEFAULT false,
                    CONSTRAINT "PK_CommercialNotes" PRIMARY KEY ("Id")
                );

                CREATE TABLE IF NOT EXISTS "CustomerAdvanceReceipts" (
                    "Id" uuid NOT NULL,
                    "ReceiptNumber" text NOT NULL DEFAULT '',
                    "OnDate" timestamp without time zone NOT NULL,
                    "CustomerId" uuid NOT NULL,
                    "CustomerName" text NOT NULL DEFAULT '',
                    "CustomerMobileNumber" text NULL,
                    "Amount" numeric(18,2) NOT NULL DEFAULT 0,
                    "AdjustedAmount" numeric(18,2) NOT NULL DEFAULT 0,
                    "AvailableAmount" numeric(18,2) NOT NULL DEFAULT 0,
                    "PaymentMode" integer NOT NULL DEFAULT 0,
                    "BankAccountId" uuid NULL,
                    "ReferenceNumber" text NULL,
                    "Remarks" text NULL,
                    "CompanyId" uuid NOT NULL,
                    "CreatedBy" text NULL,
                    "StoreGroupId" uuid NOT NULL,
                    "StoreId" uuid NOT NULL,
                    "CreatedAt" timestamp without time zone NOT NULL,
                    "UpdatedAt" timestamp without time zone NULL,
                    "Synced" boolean NOT NULL DEFAULT false,
                    "Deleted" boolean NOT NULL DEFAULT false,
                    CONSTRAINT "PK_CustomerAdvanceReceipts" PRIMARY KEY ("Id")
                );

                CREATE TABLE IF NOT EXISTS "LoyaltyPrograms" (
                    "Id" uuid NOT NULL,
                    "Enabled" boolean NOT NULL DEFAULT true,
                    "Name" text NOT NULL DEFAULT 'Garmetix Loyalty',
                    "EarnPointsPerRupee" numeric(18,2) NOT NULL DEFAULT 0,
                    "RedeemValuePerPoint" numeric(18,2) NOT NULL DEFAULT 0,
                    "MinimumBillAmount" numeric(18,2) NOT NULL DEFAULT 0,
                    "ExpiryDays" integer NULL,
                    "CompanyId" uuid NOT NULL,
                    "CreatedBy" text NULL,
                    "StoreGroupId" uuid NOT NULL,
                    "StoreId" uuid NOT NULL,
                    "CreatedAt" timestamp without time zone NOT NULL,
                    "UpdatedAt" timestamp without time zone NULL,
                    "Synced" boolean NOT NULL DEFAULT false,
                    "Deleted" boolean NOT NULL DEFAULT false,
                    CONSTRAINT "PK_LoyaltyPrograms" PRIMARY KEY ("Id")
                );

                CREATE TABLE IF NOT EXISTS "LoyaltyPointLedgers" (
                    "Id" uuid NOT NULL,
                    "CustomerId" uuid NOT NULL,
                    "CustomerName" text NOT NULL DEFAULT '',
                    "OnDate" timestamp without time zone NOT NULL,
                    "SourceType" text NOT NULL DEFAULT '',
                    "SourceId" uuid NULL,
                    "SourceNumber" text NULL,
                    "PointsIn" numeric(18,2) NOT NULL DEFAULT 0,
                    "PointsOut" numeric(18,2) NOT NULL DEFAULT 0,
                    "BalanceAfter" numeric(18,2) NOT NULL DEFAULT 0,
                    "Remarks" text NULL,
                    "CompanyId" uuid NOT NULL,
                    "CreatedBy" text NULL,
                    "StoreGroupId" uuid NOT NULL,
                    "StoreId" uuid NOT NULL,
                    "CreatedAt" timestamp without time zone NOT NULL,
                    "UpdatedAt" timestamp without time zone NULL,
                    "Synced" boolean NOT NULL DEFAULT false,
                    "Deleted" boolean NOT NULL DEFAULT false,
                    CONSTRAINT "PK_LoyaltyPointLedgers" PRIMARY KEY ("Id")
                );

                ALTER TABLE IF EXISTS "GstReturnDrafts" ADD COLUMN IF NOT EXISTS "Title" text NOT NULL DEFAULT '';
                ALTER TABLE IF EXISTS "GstReturnDrafts" ADD COLUMN IF NOT EXISTS "LastPreviewIssuesJson" text NOT NULL DEFAULT '[]';
                ALTER TABLE IF EXISTS "GstReturnDrafts" ADD COLUMN IF NOT EXISTS "CreatedByUserName" text NOT NULL DEFAULT '';
                ALTER TABLE IF EXISTS "GstReturnDrafts" ADD COLUMN IF NOT EXISTS "UpdatedByUserName" text NOT NULL DEFAULT '';
                ALTER TABLE IF EXISTS "GstReturnDrafts" ADD COLUMN IF NOT EXISTS "FiledAt" timestamp without time zone NULL;
                ALTER TABLE IF EXISTS "GstReturnDrafts" ADD COLUMN IF NOT EXISTS "LockedAt" timestamp without time zone NULL;

                ALTER TABLE IF EXISTS "CommercialNotes" ADD COLUMN IF NOT EXISTS "Printed" boolean NOT NULL DEFAULT false;
                ALTER TABLE IF EXISTS "CommercialNotes" ADD COLUMN IF NOT EXISTS "AdjustedAmount" numeric(18,2) NOT NULL DEFAULT 0;
                ALTER TABLE IF EXISTS "CommercialNotes" ADD COLUMN IF NOT EXISTS "IsAdjusted" boolean NOT NULL DEFAULT false;

                ALTER TABLE IF EXISTS "CustomerAdvanceReceipts" ADD COLUMN IF NOT EXISTS "AvailableAmount" numeric(18,2) NOT NULL DEFAULT 0;
                ALTER TABLE IF EXISTS "CustomerAdvanceReceipts" ADD COLUMN IF NOT EXISTS "AdjustedAmount" numeric(18,2) NOT NULL DEFAULT 0;

                CREATE INDEX IF NOT EXISTS "IX_GstReturnDrafts_CompanyId_Form_ReturnPeriod_Gstin" ON "GstReturnDrafts" ("CompanyId", "Form", "ReturnPeriod", "Gstin");
                CREATE INDEX IF NOT EXISTS "IX_GstReturnDrafts_CompanyId_Status_UpdatedAt" ON "GstReturnDrafts" ("CompanyId", "Status", "UpdatedAt");
                CREATE INDEX IF NOT EXISTS "IX_GstReturnAuditEntries_CompanyId_DraftId_CreatedAt" ON "GstReturnAuditEntries" ("CompanyId", "DraftId", "CreatedAt");
                CREATE INDEX IF NOT EXISTS "IX_GstReturnAuditEntries_CompanyId_Form_ReturnPeriod" ON "GstReturnAuditEntries" ("CompanyId", "Form", "ReturnPeriod");
                CREATE INDEX IF NOT EXISTS "IX_CommercialNotes_CompanyId_StoreId_NoteNumber" ON "CommercialNotes" ("CompanyId", "StoreId", "NoteNumber");
                CREATE INDEX IF NOT EXISTS "IX_CommercialNotes_CompanyId_PartyType_PartyName" ON "CommercialNotes" ("CompanyId", "PartyType", "PartyName");
                CREATE INDEX IF NOT EXISTS "IX_CustomerAdvanceReceipts_CompanyId_StoreId_ReceiptNumber" ON "CustomerAdvanceReceipts" ("CompanyId", "StoreId", "ReceiptNumber");
                CREATE INDEX IF NOT EXISTS "IX_CustomerAdvanceReceipts_CompanyId_CustomerId_OnDate" ON "CustomerAdvanceReceipts" ("CompanyId", "CustomerId", "OnDate");
                CREATE INDEX IF NOT EXISTS "IX_LoyaltyPrograms_CompanyId_StoreId" ON "LoyaltyPrograms" ("CompanyId", "StoreId");
                CREATE INDEX IF NOT EXISTS "IX_LoyaltyPointLedgers_CompanyId_CustomerId_OnDate" ON "LoyaltyPointLedgers" ("CompanyId", "CustomerId", "OnDate");

                ALTER TABLE IF EXISTS "Products" ADD COLUMN IF NOT EXISTS "HSNCode" text NULL;
                ALTER TABLE IF EXISTS "Products" ADD COLUMN IF NOT EXISTS "ProductGroup" integer NOT NULL DEFAULT 0;
                ALTER TABLE IF EXISTS "Stocks" ADD COLUMN IF NOT EXISTS "StockType" integer NOT NULL DEFAULT 0;
                ALTER TABLE IF EXISTS "Stocks" ADD COLUMN IF NOT EXISTS "IsOFB" boolean NOT NULL DEFAULT false;
                ALTER TABLE IF EXISTS "ProductCategories" ADD COLUMN IF NOT EXISTS "ProductGroup" integer NULL DEFAULT 0;
                ALTER TABLE IF EXISTS "ProductCategories" ADD COLUMN IF NOT EXISTS "IsActive" boolean NOT NULL DEFAULT true;
                ALTER TABLE IF EXISTS "ProductSubCategories" ADD COLUMN IF NOT EXISTS "CategoryId" uuid NULL;

                CREATE TABLE IF NOT EXISTS "ProductDetails" (
                    "Id" uuid NOT NULL,
                    "ProductId" uuid NOT NULL,
                    "Barcode" text NOT NULL DEFAULT '',
                    "StyleCode" text NULL,
                    "BaseColor" text NULL,
                    "Brand" text NULL,
                    "VendorId" uuid NULL,
                    "CompanyId" uuid NOT NULL,
                    "CreatedBy" text NULL,
                    "CreatedAt" timestamp without time zone NOT NULL,
                    "UpdatedAt" timestamp without time zone NULL,
                    "Synced" boolean NOT NULL DEFAULT false,
                    "Deleted" boolean NOT NULL DEFAULT false,
                    CONSTRAINT "PK_ProductDetails" PRIMARY KEY ("Id")
                );

                CREATE TABLE IF NOT EXISTS "ProductAttributes" (
                    "Id" uuid NOT NULL,
                    "Name" text NOT NULL DEFAULT '',
                    CONSTRAINT "PK_ProductAttributes" PRIMARY KEY ("Id")
                );

                CREATE TABLE IF NOT EXISTS "ProductAttributeValues" (
                    "ProductId" uuid NOT NULL,
                    "AttributeId" uuid NOT NULL,
                    "Value" text NOT NULL DEFAULT '',
                    CONSTRAINT "PK_ProductAttributeValues" PRIMARY KEY ("ProductId", "AttributeId")
                );

                CREATE TABLE IF NOT EXISTS "ProductTags" (
                    "Id" uuid NOT NULL,
                    "Name" text NOT NULL DEFAULT '',
                    CONSTRAINT "PK_ProductTags" PRIMARY KEY ("Id")
                );

                CREATE TABLE IF NOT EXISTS "ProductTagMappings" (
                    "ProductId" uuid NOT NULL,
                    "TagId" uuid NOT NULL,
                    CONSTRAINT "PK_ProductTagMappings" PRIMARY KEY ("ProductId", "TagId")
                );

                CREATE INDEX IF NOT EXISTS "IX_Products_CompanyId_ProductGroup_ProductType" ON "Products" ("CompanyId", "ProductGroup", "ProductType");
                CREATE INDEX IF NOT EXISTS "IX_ProductCategories_CompanyId_ProductGroup_Name" ON "ProductCategories" ("CompanyId", "ProductGroup", "Name");
                CREATE INDEX IF NOT EXISTS "IX_ProductSubCategories_CompanyId_CategoryId_Name" ON "ProductSubCategories" ("CompanyId", "CategoryId", "Name");
                CREATE INDEX IF NOT EXISTS "IX_ProductDetails_CompanyId_ProductId_Barcode" ON "ProductDetails" ("CompanyId", "ProductId", "Barcode");

                ALTER TABLE IF EXISTS "PurchaseInvoices" ADD COLUMN IF NOT EXISTS "StoreGroupId" uuid NULL;
                ALTER TABLE IF EXISTS "PurchaseInvoices" ADD COLUMN IF NOT EXISTS "StoreId" uuid NULL;
                ALTER TABLE IF EXISTS "PurchaseInvoices" ADD COLUMN IF NOT EXISTS "SupplierInvoiceDate" timestamp without time zone NULL;

                ALTER TABLE IF EXISTS "InvoiceItems" ADD COLUMN IF NOT EXISTS "ProductName" text NULL;
                ALTER TABLE IF EXISTS "InvoiceItems" ADD COLUMN IF NOT EXISTS "HSNCode" text NULL;
                ALTER TABLE IF EXISTS "InvoiceItems" ADD COLUMN IF NOT EXISTS "Unit" integer NULL;
                ALTER TABLE IF EXISTS "InvoiceItems" ADD COLUMN IF NOT EXISTS "ProductCategoryId" uuid NULL;
                ALTER TABLE IF EXISTS "InvoiceItems" ADD COLUMN IF NOT EXISTS "ProductSubCategoryId" uuid NULL;
                ALTER TABLE IF EXISTS "InvoiceItems" ADD COLUMN IF NOT EXISTS "CGSTAmount" numeric(18,2) NULL;
                ALTER TABLE IF EXISTS "InvoiceItems" ADD COLUMN IF NOT EXISTS "SGSTAmount" numeric(18,2) NULL;
                ALTER TABLE IF EXISTS "InvoiceItems" ADD COLUMN IF NOT EXISTS "IGSTAmount" numeric(18,2) NULL;

                -- PurchaseInvoiceItem is mapped into InvoiceItems using a discriminator in the current model.
                -- Some imported databases also have a compatibility VIEW named PurchaseInvoiceItems.
                -- Do not ALTER that view here; add/repair required columns on InvoiceItems only.

                ALTER TABLE IF EXISTS "InvoicePayments" ADD COLUMN IF NOT EXISTS "BankAccountId" uuid NULL;
                ALTER TABLE IF EXISTS "InvoicePayments" ADD COLUMN IF NOT EXISTS "AdjustmentSourceType" text NULL;
                ALTER TABLE IF EXISTS "InvoicePayments" ADD COLUMN IF NOT EXISTS "AdjustmentSourceId" uuid NULL;
                ALTER TABLE IF EXISTS "InvoicePayments" ADD COLUMN IF NOT EXISTS "GatewayReference" text NULL;
                ALTER TABLE IF EXISTS "InvoicePayments" ADD COLUMN IF NOT EXISTS "SettlementStatus" text NULL;
                ALTER TABLE IF EXISTS "InvoicePayments" ADD COLUMN IF NOT EXISTS "PaymentDetailsJson" text NULL;

                ALTER TABLE IF EXISTS "CardPayments" ADD COLUMN IF NOT EXISTS "BankAccountId" uuid NULL;
                ALTER TABLE IF EXISTS "CardPayments" ADD COLUMN IF NOT EXISTS "MaskedCardNumber" text NULL;
                ALTER TABLE IF EXISTS "CardPayments" ADD COLUMN IF NOT EXISTS "ApprovalCode" text NULL;
                ALTER TABLE IF EXISTS "CardPayments" ADD COLUMN IF NOT EXISTS "GatewayReference" text NULL;
                ALTER TABLE IF EXISTS "CardPayments" ADD COLUMN IF NOT EXISTS "SettlementReference" text NULL;

                ALTER TABLE IF EXISTS "VendorPayments" ADD COLUMN IF NOT EXISTS "PurchaseInvoiceId" uuid NULL;
                ALTER TABLE IF EXISTS "VendorPayments" ADD COLUMN IF NOT EXISTS "PaymentMode" integer NOT NULL DEFAULT 0;
                ALTER TABLE IF EXISTS "VendorPayments" ADD COLUMN IF NOT EXISTS "BankAccountId" uuid NULL;
                ALTER TABLE IF EXISTS "VendorPayments" ADD COLUMN IF NOT EXISTS "ReferenceNumber" text NULL;

                CREATE TABLE IF NOT EXISTS "PurchasePayments" (
                    "Id" uuid NOT NULL,
                    "PurchaseInvoiceId" uuid NOT NULL,
                    "VendorId" uuid NOT NULL,
                    "OnDate" timestamp without time zone NOT NULL,
                    "Amount" numeric(18,2) NOT NULL DEFAULT 0,
                    "PaymentMode" integer NOT NULL DEFAULT 0,
                    "BankAccountId" uuid NULL,
                    "ReferenceNumber" text NULL,
                    "VoucherId" uuid NULL,
                    "AdjustmentSourceType" text NULL,
                    "AdjustmentSourceId" uuid NULL,
                    "Remarks" text NULL,
                    "CompanyId" uuid NOT NULL,
                    "CreatedBy" text NULL,
                    "StoreGroupId" uuid NOT NULL,
                    "StoreId" uuid NOT NULL,
                    "CreatedAt" timestamp without time zone NOT NULL,
                    "UpdatedAt" timestamp without time zone NULL,
                    "Synced" boolean NOT NULL DEFAULT false,
                    "Deleted" boolean NOT NULL DEFAULT false,
                    CONSTRAINT "PK_PurchasePayments" PRIMARY KEY ("Id")
                );

                CREATE TABLE IF NOT EXISTS "PurchaseReturns" (
                    "Id" uuid NOT NULL,
                    "ReturnNumber" text NOT NULL DEFAULT '',
                    "OnDate" timestamp without time zone NOT NULL,
                    "PurchaseInvoiceId" uuid NOT NULL,
                    "OriginalInvoiceNumber" text NOT NULL DEFAULT '',
                    "OriginalInvoiceDate" timestamp without time zone NOT NULL,
                    "SupplierInvoiceDate" timestamp without time zone NULL,
                    "VendorId" uuid NOT NULL,
                    "VendorName" text NOT NULL DEFAULT '',
                    "VendorGstin" text NULL,
                    "ReturnKind" text NOT NULL DEFAULT 'Partial',
                    "Status" text NOT NULL DEFAULT 'Posted',
                    "Reason" text NOT NULL DEFAULT '',
                    "Quantity" numeric(18,2) NOT NULL DEFAULT 0,
                    "TaxableAmount" numeric(18,2) NOT NULL DEFAULT 0,
                    "TaxAmount" numeric(18,2) NOT NULL DEFAULT 0,
                    "CGSTAmount" numeric(18,2) NOT NULL DEFAULT 0,
                    "SGSTAmount" numeric(18,2) NOT NULL DEFAULT 0,
                    "IGSTAmount" numeric(18,2) NOT NULL DEFAULT 0,
                    "ReturnAmount" numeric(18,2) NOT NULL DEFAULT 0,
                      "DebitNoteId" uuid NULL,
                      "DebitNoteNumber" text NULL,
                      "ItemCount" integer NOT NULL DEFAULT 0,
                      "Printed" boolean NOT NULL DEFAULT false,
                      "PrintCount" integer NOT NULL DEFAULT 0,
                      "LastPrintedAt" timestamp without time zone NULL,
                      "SettledAmount" numeric(18,2) NOT NULL DEFAULT 0,
                      "SettlementStatus" text NOT NULL DEFAULT 'Open',
                    "CompanyId" uuid NOT NULL,
                    "CreatedBy" text NULL,
                    "StoreGroupId" uuid NOT NULL,
                    "StoreId" uuid NOT NULL,
                    "CreatedAt" timestamp without time zone NOT NULL DEFAULT now(),
                    "UpdatedAt" timestamp without time zone NULL,
                    "Synced" boolean NOT NULL DEFAULT false,
                    "Deleted" boolean NOT NULL DEFAULT false,
                      CONSTRAINT "PK_PurchaseReturns" PRIMARY KEY ("Id")
                  );

                  ALTER TABLE "PurchaseReturns" ADD COLUMN IF NOT EXISTS "Printed" boolean NOT NULL DEFAULT false;
                  ALTER TABLE "PurchaseReturns" ADD COLUMN IF NOT EXISTS "PrintCount" integer NOT NULL DEFAULT 0;
                  ALTER TABLE "PurchaseReturns" ADD COLUMN IF NOT EXISTS "LastPrintedAt" timestamp without time zone NULL;
                  ALTER TABLE "PurchaseReturns" ADD COLUMN IF NOT EXISTS "SettledAmount" numeric(18,2) NOT NULL DEFAULT 0;
                  ALTER TABLE "PurchaseReturns" ADD COLUMN IF NOT EXISTS "SettlementStatus" text NOT NULL DEFAULT 'Open';
                  ALTER TABLE "PurchaseReturns" ADD COLUMN IF NOT EXISTS "ItcReversalAmount" numeric(18,2) NOT NULL DEFAULT 0;
                  ALTER TABLE "PurchaseReturns" ADD COLUMN IF NOT EXISTS "ItcReversalStatus" text NOT NULL DEFAULT 'Pending';
                  ALTER TABLE "PurchaseReturns" ADD COLUMN IF NOT EXISTS "JournalEntryId" uuid NULL;

                  -- Stage 14T: purchase return transport/freight fields (billed to vendor via the
                  -- debit note vs booked as an in-house Expense voucher). See migration
                  -- 20260711090000_AddPurchaseReturnFreightFields.cs.
                  ALTER TABLE "PurchaseReturns" ADD COLUMN IF NOT EXISTS "TransportDetails" text NULL;
                  ALTER TABLE "PurchaseReturns" ADD COLUMN IF NOT EXISTS "FreightAmount" numeric(18,2) NOT NULL DEFAULT 0;
                  ALTER TABLE "PurchaseReturns" ADD COLUMN IF NOT EXISTS "FreightBearer" text NULL;
                  ALTER TABLE "PurchaseReturns" ADD COLUMN IF NOT EXISTS "FreightExpenseVoucherId" uuid NULL;
                  ALTER TABLE "PurchaseReturns" ADD COLUMN IF NOT EXISTS "FreightExpenseVoucherNumber" text NULL;

                  -- Vendor-first Goods Return page: GST on freight when the vendor bears the
                  -- cost (freight amount + 5% GST added to the debit note). See migration
                  -- 20260711140000_AddPurchaseReturnFreightTaxAmount.cs.
                  ALTER TABLE "PurchaseReturns" ADD COLUMN IF NOT EXISTS "FreightTaxAmount" numeric(18,2) NOT NULL DEFAULT 0;

                  ALTER TABLE "PurchasePayments" ADD COLUMN IF NOT EXISTS "AdjustmentSourceType" text NULL;
                  ALTER TABLE "PurchasePayments" ADD COLUMN IF NOT EXISTS "AdjustmentSourceId" uuid NULL;

                CREATE TABLE IF NOT EXISTS "PurchaseReturnItems" (
                    "Id" uuid NOT NULL,
                    "PurchaseReturnId" uuid NOT NULL,
                    "PurchaseInvoiceId" uuid NOT NULL,
                    "PurchaseInvoiceItemId" uuid NOT NULL,
                    "ProductId" uuid NOT NULL,
                    "ProductName" text NOT NULL DEFAULT '',
                    "Barcode" text NOT NULL DEFAULT '',
                    "HSNCode" text NULL,
                    "Unit" integer NULL,
                    "ProductCategoryId" uuid NULL,
                    "ProductSubCategoryId" uuid NULL,
                    "PurchasedQuantity" numeric(18,2) NOT NULL DEFAULT 0,
                    "PreviouslyReturnedQuantity" numeric(18,2) NOT NULL DEFAULT 0,
                    "ReturnedQuantity" numeric(18,2) NOT NULL DEFAULT 0,
                    "MRP" numeric(18,2) NOT NULL DEFAULT 0,
                    "UnitRate" numeric(18,2) NOT NULL DEFAULT 0,
                    "DiscountAmount" numeric(18,2) NOT NULL DEFAULT 0,
                    "TaxableAmount" numeric(18,2) NOT NULL DEFAULT 0,
                    "TaxRate" numeric(18,2) NOT NULL DEFAULT 0,
                    "TaxAmount" numeric(18,2) NOT NULL DEFAULT 0,
                    "CGSTAmount" numeric(18,2) NOT NULL DEFAULT 0,
                    "SGSTAmount" numeric(18,2) NOT NULL DEFAULT 0,
                    "IGSTAmount" numeric(18,2) NOT NULL DEFAULT 0,
                    "ReturnAmount" numeric(18,2) NOT NULL DEFAULT 0,
                    "Reason" text NULL,
                    "CompanyId" uuid NOT NULL,
                    "CreatedBy" text NULL,
                    "CreatedAt" timestamp without time zone NOT NULL DEFAULT now(),
                    "UpdatedAt" timestamp without time zone NULL,
                    "Synced" boolean NOT NULL DEFAULT false,
                    "Deleted" boolean NOT NULL DEFAULT false,
                    CONSTRAINT "PK_PurchaseReturnItems" PRIMARY KEY ("Id")
                );

                CREATE TABLE IF NOT EXISTS "PurchaseReturnItcReversals" (
                    "Id" uuid NOT NULL,
                    "PurchaseReturnId" uuid NOT NULL,
                    "PurchaseReturnItemId" uuid NOT NULL,
                    "PurchaseInvoiceId" uuid NOT NULL,
                    "PurchaseInvoiceItemId" uuid NOT NULL,
                    "ReturnNumber" text NOT NULL DEFAULT '',
                    "OriginalInvoiceNumber" text NOT NULL DEFAULT '',
                    "OnDate" timestamp without time zone NOT NULL,
                    "ProductId" uuid NOT NULL,
                    "ProductName" text NOT NULL DEFAULT '',
                    "HSNCode" text NULL,
                    "TaxRate" numeric(18,2) NOT NULL DEFAULT 0,
                    "ReturnedQuantity" numeric(18,2) NOT NULL DEFAULT 0,
                    "TaxableAmount" numeric(18,2) NOT NULL DEFAULT 0,
                    "CGSTAmount" numeric(18,2) NOT NULL DEFAULT 0,
                    "SGSTAmount" numeric(18,2) NOT NULL DEFAULT 0,
                    "IGSTAmount" numeric(18,2) NOT NULL DEFAULT 0,
                    "TaxAmount" numeric(18,2) NOT NULL DEFAULT 0,
                    "JournalEntryId" uuid NULL,
                    "Status" text NOT NULL DEFAULT 'Posted',
                    "CompanyId" uuid NOT NULL,
                    "CreatedBy" text NULL,
                    "StoreGroupId" uuid NOT NULL,
                    "StoreId" uuid NOT NULL,
                    "CreatedAt" timestamp without time zone NOT NULL DEFAULT now(),
                    "UpdatedAt" timestamp without time zone NULL,
                    "Synced" boolean NOT NULL DEFAULT false,
                    "Deleted" boolean NOT NULL DEFAULT false,
                    CONSTRAINT "PK_PurchaseReturnItcReversals" PRIMARY KEY ("Id")
                );

                CREATE TABLE IF NOT EXISTS "VendorSettlements" (
                    "Id" uuid NOT NULL,
                    "SettlementNumber" text NOT NULL DEFAULT '',
                    "OnDate" timestamp without time zone NOT NULL,
                    "VendorId" uuid NOT NULL,
                    "VendorName" text NOT NULL DEFAULT '',
                    "PurchaseReturnId" uuid NOT NULL,
                    "ReturnNumber" text NOT NULL DEFAULT '',
                    "DebitNoteId" uuid NOT NULL,
                    "DebitNoteNumber" text NOT NULL DEFAULT '',
                    "SettlementType" text NOT NULL DEFAULT 'Adjustment',
                    "AdjustedAmount" numeric(18,2) NOT NULL DEFAULT 0,
                    "RefundAmount" numeric(18,2) NOT NULL DEFAULT 0,
                    "TotalAmount" numeric(18,2) NOT NULL DEFAULT 0,
                    "PaymentMode" integer NULL,
                    "BankAccountId" uuid NULL,
                    "ReferenceNumber" text NULL,
                    "VoucherId" uuid NULL,
                    "JournalEntryId" uuid NULL,
                    "BankTransactionId" uuid NULL,
                    "Status" text NOT NULL DEFAULT 'Posted',
                    "Remarks" text NULL,
                    "CompanyId" uuid NOT NULL,
                    "CreatedBy" text NULL,
                    "StoreGroupId" uuid NOT NULL,
                    "StoreId" uuid NOT NULL,
                    "CreatedAt" timestamp without time zone NOT NULL DEFAULT now(),
                    "UpdatedAt" timestamp without time zone NULL,
                    "Synced" boolean NOT NULL DEFAULT false,
                    "Deleted" boolean NOT NULL DEFAULT false,
                    CONSTRAINT "PK_VendorSettlements" PRIMARY KEY ("Id")
                );

                CREATE TABLE IF NOT EXISTS "VendorSettlementAllocations" (
                    "Id" uuid NOT NULL,
                    "VendorSettlementId" uuid NOT NULL,
                    "PurchaseInvoiceId" uuid NOT NULL,
                    "PurchaseInvoiceNumber" text NOT NULL DEFAULT '',
                    "Amount" numeric(18,2) NOT NULL DEFAULT 0,
                    "CompanyId" uuid NOT NULL,
                    "CreatedBy" text NULL,
                    "CreatedAt" timestamp without time zone NOT NULL DEFAULT now(),
                    "UpdatedAt" timestamp without time zone NULL,
                    "Synced" boolean NOT NULL DEFAULT false,
                    "Deleted" boolean NOT NULL DEFAULT false,
                    CONSTRAINT "PK_VendorSettlementAllocations" PRIMARY KEY ("Id")
                );

                CREATE INDEX IF NOT EXISTS "IX_VendorSettlements_CompanyId_StoreId_SettlementNumber" ON "VendorSettlements" ("CompanyId", "StoreId", "SettlementNumber");
                CREATE INDEX IF NOT EXISTS "IX_VendorSettlements_CompanyId_VendorId_OnDate" ON "VendorSettlements" ("CompanyId", "VendorId", "OnDate");
                CREATE INDEX IF NOT EXISTS "IX_VendorSettlements_CompanyId_PurchaseReturnId" ON "VendorSettlements" ("CompanyId", "PurchaseReturnId");
                CREATE INDEX IF NOT EXISTS "IX_VendorSettlementAllocations_CompanyId_VendorSettlementId" ON "VendorSettlementAllocations" ("CompanyId", "VendorSettlementId");
                CREATE INDEX IF NOT EXISTS "IX_VendorSettlementAllocations_CompanyId_PurchaseInvoiceId" ON "VendorSettlementAllocations" ("CompanyId", "PurchaseInvoiceId");

                UPDATE "PurchaseReturns" AS purchase_return
                SET "SettledAmount" = LEAST(note."AdjustedAmount", purchase_return."ReturnAmount"),
                    "SettlementStatus" = CASE
                        WHEN note."AdjustedAmount" <= 0 THEN 'Open'
                        WHEN note."AdjustedAmount" >= purchase_return."ReturnAmount" THEN 'Settled'
                        ELSE 'Partially Settled'
                    END
                FROM "CommercialNotes" AS note
                WHERE purchase_return."DebitNoteId" = note."Id";

                CREATE TABLE IF NOT EXISTS "NonGstGoodsDocuments" (
                    "Id" uuid NOT NULL,
                    "DocumentNumber" text NOT NULL DEFAULT '',
                    "OnDate" timestamp without time zone NOT NULL,
                    "DocumentType" integer NOT NULL DEFAULT 1,
                    "PartyName" text NOT NULL DEFAULT '',
                    "VendorId" uuid NULL,
                    "CustomerId" uuid NULL,
                    "PaymentMode" integer NOT NULL DEFAULT 0,
                    "ReferenceNumber" text NULL,
                    "GrossAmount" numeric(18,2) NOT NULL DEFAULT 0,
                    "DiscountAmount" numeric(18,2) NOT NULL DEFAULT 0,
                    "NetAmount" numeric(18,2) NOT NULL DEFAULT 0,
                    "LedgerId" uuid NULL,
                    "Remarks" text NULL,
                    "CompanyId" uuid NOT NULL,
                    "CreatedBy" text NULL,
                    "StoreGroupId" uuid NOT NULL,
                    "StoreId" uuid NOT NULL,
                    "CreatedAt" timestamp without time zone NOT NULL,
                    "UpdatedAt" timestamp without time zone NULL,
                    "Synced" boolean NOT NULL DEFAULT false,
                    "Deleted" boolean NOT NULL DEFAULT false,
                    CONSTRAINT "PK_NonGstGoodsDocuments" PRIMARY KEY ("Id")
                );

                CREATE TABLE IF NOT EXISTS "NonGstGoodsItems" (
                    "Id" uuid NOT NULL,
                    "DocumentId" uuid NOT NULL,
                    "ProductId" uuid NOT NULL,
                    "StockId" uuid NULL,
                    "Barcode" text NOT NULL DEFAULT '',
                    "ProductName" text NOT NULL DEFAULT '',
                    "Quantity" numeric(18,2) NOT NULL DEFAULT 0,
                    "Rate" numeric(18,2) NOT NULL DEFAULT 0,
                    "GrossAmount" numeric(18,2) NOT NULL DEFAULT 0,
                    "DiscountAmount" numeric(18,2) NOT NULL DEFAULT 0,
                    "TaxableAmount" numeric(18,2) NOT NULL DEFAULT 0,
                    "TaxRate" numeric(18,2) NOT NULL DEFAULT 0,
                    "TaxAmount" numeric(18,2) NOT NULL DEFAULT 0,
                    "Amount" numeric(18,2) NOT NULL DEFAULT 0,
                    "CostRate" numeric(18,2) NOT NULL DEFAULT 0,
                    "CostAmount" numeric(18,2) NOT NULL DEFAULT 0,
                    "CompanyId" uuid NOT NULL,
                    "CreatedBy" text NULL,
                    "CreatedAt" timestamp without time zone NOT NULL,
                    "UpdatedAt" timestamp without time zone NULL,
                    "Synced" boolean NOT NULL DEFAULT false,
                    "Deleted" boolean NOT NULL DEFAULT false,
                    CONSTRAINT "PK_NonGstGoodsItems" PRIMARY KEY ("Id")
                );

                CREATE INDEX IF NOT EXISTS "IX_Stocks_CompanyId_StoreId_IsOFB" ON "Stocks" ("CompanyId", "StoreId", "IsOFB");
                CREATE INDEX IF NOT EXISTS "IX_NonGstGoodsDocuments_CompanyId_StoreId_DocumentType_OnDate" ON "NonGstGoodsDocuments" ("CompanyId", "StoreId", "DocumentType", "OnDate");
                CREATE INDEX IF NOT EXISTS "IX_NonGstGoodsDocuments_CompanyId_DocumentNumber" ON "NonGstGoodsDocuments" ("CompanyId", "DocumentNumber");
                CREATE INDEX IF NOT EXISTS "IX_NonGstGoodsItems_CompanyId_DocumentId" ON "NonGstGoodsItems" ("CompanyId", "DocumentId");

                ALTER TABLE IF EXISTS "NonGstGoodsItems" ADD COLUMN IF NOT EXISTS "GrossAmount" numeric(18,2) NOT NULL DEFAULT 0;
                ALTER TABLE IF EXISTS "NonGstGoodsItems" ADD COLUMN IF NOT EXISTS "TaxableAmount" numeric(18,2) NOT NULL DEFAULT 0;
                ALTER TABLE IF EXISTS "NonGstGoodsItems" ADD COLUMN IF NOT EXISTS "TaxRate" numeric(18,2) NOT NULL DEFAULT 0;
                ALTER TABLE IF EXISTS "NonGstGoodsItems" ADD COLUMN IF NOT EXISTS "TaxAmount" numeric(18,2) NOT NULL DEFAULT 0;
                ALTER TABLE IF EXISTS "NonGstGoodsItems" ADD COLUMN IF NOT EXISTS "CostRate" numeric(18,2) NOT NULL DEFAULT 0;
                ALTER TABLE IF EXISTS "NonGstGoodsItems" ADD COLUMN IF NOT EXISTS "CostAmount" numeric(18,2) NOT NULL DEFAULT 0;
                ALTER TABLE IF EXISTS "NonGstGoodsDocuments" ADD COLUMN IF NOT EXISTS "PaidAmount" numeric(18,2) NOT NULL DEFAULT 0;
                ALTER TABLE IF EXISTS "NonGstGoodsDocuments" ADD COLUMN IF NOT EXISTS "BalanceAmount" numeric(18,2) NOT NULL DEFAULT 0;

                UPDATE "NonGstGoodsDocuments"
                SET "PaidAmount" = "NetAmount",
                    "BalanceAmount" = 0
                WHERE "LedgerId" IS NOT NULL;

                UPDATE "NonGstGoodsDocuments"
                SET "LedgerId" = NULL;

                DELETE FROM "JournalLines"
                WHERE "JournalEntryId" IN (
                    SELECT "Id"
                    FROM "JournalEntries"
                    WHERE "SourceType" IN ('NonGstPurchase', 'NonGstSale')
                );

                DELETE FROM "JournalEntries"
                WHERE "SourceType" IN ('NonGstPurchase', 'NonGstSale');

                CREATE TABLE IF NOT EXISTS "StockMovements" (
                    "Id" uuid NOT NULL,
                    "StockId" uuid NULL,
                    "ProductId" uuid NOT NULL,
                    "Barcode" text NOT NULL DEFAULT '',
                    "MovementType" text NOT NULL DEFAULT '',
                    "QuantityIn" numeric(18,2) NOT NULL DEFAULT 0,
                    "QuantityOut" numeric(18,2) NOT NULL DEFAULT 0,
                    "CostPrice" numeric(18,2) NOT NULL DEFAULT 0,
                    "MRP" numeric(18,2) NOT NULL DEFAULT 0,
                    "TaxRate" numeric(18,2) NOT NULL DEFAULT 0,
                    "HSNCode" text NULL,
                    "SourceType" text NULL,
                    "SourceId" uuid NULL,
                    "SourceNumber" text NULL,
                    "Remarks" text NULL,
                    "OnDate" timestamp without time zone NOT NULL,
                    "CompanyId" uuid NOT NULL,
                    "CreatedBy" text NULL,
                    "StoreGroupId" uuid NOT NULL,
                    "StoreId" uuid NOT NULL,
                    "CreatedAt" timestamp without time zone NOT NULL,
                    "UpdatedAt" timestamp without time zone NULL,
                    "Synced" boolean NOT NULL DEFAULT false,
                    "Deleted" boolean NOT NULL DEFAULT false,
                    CONSTRAINT "PK_StockMovements" PRIMARY KEY ("Id")
                );

                ALTER TABLE IF EXISTS "Stocks" ALTER COLUMN "CostPrice" TYPE numeric(18,4);
                ALTER TABLE IF EXISTS "StockMovements" ALTER COLUMN "CostPrice" TYPE numeric(18,4);
                ALTER TABLE IF EXISTS "StockMovements" ADD COLUMN IF NOT EXISTS "QuantityBefore" numeric(18,2) NOT NULL DEFAULT 0;
                ALTER TABLE IF EXISTS "StockMovements" ADD COLUMN IF NOT EXISTS "QuantityAfter" numeric(18,2) NOT NULL DEFAULT 0;
                ALTER TABLE IF EXISTS "StockMovements" ADD COLUMN IF NOT EXISTS "AverageCostBefore" numeric(18,4) NOT NULL DEFAULT 0;
                ALTER TABLE IF EXISTS "StockMovements" ADD COLUMN IF NOT EXISTS "AverageCostAfter" numeric(18,4) NOT NULL DEFAULT 0;
                ALTER TABLE IF EXISTS "StockMovements" ADD COLUMN IF NOT EXISTS "InventoryValueBefore" numeric(18,2) NOT NULL DEFAULT 0;
                ALTER TABLE IF EXISTS "StockMovements" ADD COLUMN IF NOT EXISTS "InventoryValueAfter" numeric(18,2) NOT NULL DEFAULT 0;
                ALTER TABLE IF EXISTS "StockMovements" ADD COLUMN IF NOT EXISTS "CostImpact" numeric(18,2) NOT NULL DEFAULT 0;
                ALTER TABLE IF EXISTS "StockMovements" ADD COLUMN IF NOT EXISTS "ValuationMethod" text NOT NULL DEFAULT 'WeightedAverage';

                DROP TABLE IF EXISTS "_LegacyStockProjectionBackfill";
                CREATE TEMP TABLE "_LegacyStockProjectionBackfill" ON COMMIT DROP AS
                SELECT stock.*
                FROM "Stocks" AS stock
                WHERE stock."IsOFB" = false
                  AND (stock."PurchaseQty" <> 0 OR stock."SoldQty" <> 0)
                  AND NOT EXISTS (
                      SELECT 1
                      FROM "StockMovements" AS movement
                      WHERE movement."StockId" = stock."Id");

                INSERT INTO "StockMovements" (
                    "Id", "StockId", "ProductId", "Barcode", "MovementType",
                    "QuantityIn", "QuantityOut", "CostPrice",
                    "QuantityBefore", "QuantityAfter", "AverageCostBefore", "AverageCostAfter",
                    "InventoryValueBefore", "InventoryValueAfter", "CostImpact", "ValuationMethod",
                    "MRP", "TaxRate", "HSNCode", "SourceType", "SourceId", "SourceNumber", "Remarks",
                    "OnDate", "CompanyId", "CreatedBy", "StoreGroupId", "StoreId",
                    "CreatedAt", "UpdatedAt", "Synced", "Deleted")
                SELECT
                    md5(stock."Id"::text || ':legacy-opening-in')::uuid,
                    stock."Id", stock."ProductId", stock."Barcode", 'LegacyOpeningIn',
                    stock."PurchaseQty", 0, stock."CostPrice",
                    0, stock."PurchaseQty", 0,
                    CASE WHEN stock."PurchaseQty" > 0 THEN stock."CostPrice" ELSE 0 END,
                    0, ROUND(stock."PurchaseQty" * stock."CostPrice", 2),
                    ROUND(stock."PurchaseQty" * stock."CostPrice", 2), 'WeightedAverage',
                    stock."MRP", stock."TaxRate", stock."HSNCode",
                    'LegacyStockProjection', stock."Id", stock."Barcode",
                    'Backfilled from legacy Stock purchase quantity',
                    stock."CreatedAt", stock."CompanyId", 'Migration',
                    stock."StoreGroupId", stock."StoreId", stock."CreatedAt",
                    NULL, false, false
                FROM "_LegacyStockProjectionBackfill" AS stock
                WHERE stock."PurchaseQty" > 0;

                INSERT INTO "StockMovements" (
                    "Id", "StockId", "ProductId", "Barcode", "MovementType",
                    "QuantityIn", "QuantityOut", "CostPrice",
                    "QuantityBefore", "QuantityAfter", "AverageCostBefore", "AverageCostAfter",
                    "InventoryValueBefore", "InventoryValueAfter", "CostImpact", "ValuationMethod",
                    "MRP", "TaxRate", "HSNCode", "SourceType", "SourceId", "SourceNumber", "Remarks",
                    "OnDate", "CompanyId", "CreatedBy", "StoreGroupId", "StoreId",
                    "CreatedAt", "UpdatedAt", "Synced", "Deleted")
                SELECT
                    md5(stock."Id"::text || ':legacy-opening-out')::uuid,
                    stock."Id", stock."ProductId", stock."Barcode", 'LegacyOpeningOut',
                    0, stock."SoldQty",
                    CASE WHEN stock."PurchaseQty" > 0 THEN stock."CostPrice" ELSE 0 END,
                    stock."PurchaseQty", ROUND(stock."PurchaseQty" - stock."SoldQty", 2),
                    CASE WHEN stock."PurchaseQty" > 0 THEN stock."CostPrice" ELSE 0 END,
                    CASE WHEN stock."PurchaseQty" - stock."SoldQty" > 0 THEN stock."CostPrice" ELSE 0 END,
                    ROUND(stock."PurchaseQty" * stock."CostPrice", 2),
                    ROUND((stock."PurchaseQty" - stock."SoldQty") * stock."CostPrice", 2),
                    ROUND(-stock."SoldQty" * stock."CostPrice", 2), 'WeightedAverage',
                    stock."MRP", stock."TaxRate", stock."HSNCode",
                    'LegacyStockProjection', stock."Id", stock."Barcode",
                    'Backfilled from legacy Stock sold quantity',
                    stock."CreatedAt" + interval '1 second', stock."CompanyId", 'Migration',
                    stock."StoreGroupId", stock."StoreId", stock."CreatedAt" + interval '1 second',
                    NULL, false, false
                FROM "_LegacyStockProjectionBackfill" AS stock
                WHERE stock."SoldQty" > 0;

                CREATE TABLE IF NOT EXISTS "StockOperationDocuments" (
                    "Id" uuid NOT NULL,
                    "DocumentNumber" text NOT NULL DEFAULT '',
                    "OnDate" timestamp without time zone NOT NULL,
                    "OperationType" text NOT NULL DEFAULT '',
                    "Status" text NOT NULL DEFAULT 'Posted',
                    "FromStoreId" uuid NULL,
                    "FromStoreName" text NULL,
                    "ToStoreId" uuid NULL,
                    "ToStoreName" text NULL,
                    "Reason" text NOT NULL DEFAULT '',
                    "TotalQuantity" numeric(18,2) NOT NULL DEFAULT 0,
                    "TotalCostValue" numeric(18,2) NOT NULL DEFAULT 0,
                    "TotalMrpValue" numeric(18,2) NOT NULL DEFAULT 0,
                    "ItemCount" integer NOT NULL DEFAULT 0,
                    "PostedAt" timestamp without time zone NOT NULL,
                    "AccountingStatus" text NOT NULL DEFAULT 'Pending',
                    "JournalEntryId" uuid NULL,
                    "CompanyId" uuid NOT NULL,
                    "CreatedBy" text NULL,
                    "StoreGroupId" uuid NOT NULL,
                    "StoreId" uuid NOT NULL,
                    "CreatedAt" timestamp without time zone NOT NULL DEFAULT now(),
                    "UpdatedAt" timestamp without time zone NULL,
                    "Synced" boolean NOT NULL DEFAULT false,
                    "Deleted" boolean NOT NULL DEFAULT false,
                    CONSTRAINT "PK_StockOperationDocuments" PRIMARY KEY ("Id")
                );

                CREATE TABLE IF NOT EXISTS "StockOperationItems" (
                    "Id" uuid NOT NULL,
                    "StockOperationDocumentId" uuid NOT NULL,
                    "ProductId" uuid NOT NULL,
                    "StockId" uuid NULL,
                    "DestinationStockId" uuid NULL,
                    "ProductName" text NOT NULL DEFAULT '',
                    "Barcode" text NOT NULL DEFAULT '',
                    "HSNCode" text NULL,
                    "Unit" integer NOT NULL DEFAULT 0,
                    "FromStoreId" uuid NULL,
                    "ToStoreId" uuid NULL,
                    "SystemQuantity" numeric(18,2) NOT NULL DEFAULT 0,
                    "CountedQuantity" numeric(18,2) NULL,
                    "QuantityIn" numeric(18,2) NOT NULL DEFAULT 0,
                    "QuantityOut" numeric(18,2) NOT NULL DEFAULT 0,
                    "QuantityDifference" numeric(18,2) NOT NULL DEFAULT 0,
                    "FromQuantityBefore" numeric(18,2) NOT NULL DEFAULT 0,
                    "FromQuantityAfter" numeric(18,2) NOT NULL DEFAULT 0,
                    "ToQuantityBefore" numeric(18,2) NULL,
                    "ToQuantityAfter" numeric(18,2) NULL,
                    "CostPrice" numeric(18,2) NOT NULL DEFAULT 0,
                    "MRP" numeric(18,2) NOT NULL DEFAULT 0,
                    "CostValue" numeric(18,2) NOT NULL DEFAULT 0,
                    "MrpValue" numeric(18,2) NOT NULL DEFAULT 0,
                    "OutMovementId" uuid NULL,
                    "InMovementId" uuid NULL,
                    "Reason" text NULL,
                    "CompanyId" uuid NOT NULL,
                    "CreatedBy" text NULL,
                    "CreatedAt" timestamp without time zone NOT NULL DEFAULT now(),
                    "UpdatedAt" timestamp without time zone NULL,
                    "Synced" boolean NOT NULL DEFAULT false,
                    "Deleted" boolean NOT NULL DEFAULT false,
                    CONSTRAINT "PK_StockOperationItems" PRIMARY KEY ("Id")
                );

                CREATE TABLE IF NOT EXISTS "DocumentSequences" (
                    "Id" uuid NOT NULL,
                    "DocumentType" text NOT NULL DEFAULT '',
                    "Prefix" text NOT NULL DEFAULT '',
                    "SequenceDate" timestamp without time zone NOT NULL,
                    "LastNumber" integer NOT NULL DEFAULT 0,
                    "StoreGroupId" uuid NULL,
                    "StoreId" uuid NULL,
                    "CompanyId" uuid NOT NULL,
                    "CreatedBy" text NULL,
                    "CreatedAt" timestamp without time zone NOT NULL DEFAULT now(),
                    "UpdatedAt" timestamp without time zone NULL,
                    "Synced" boolean NOT NULL DEFAULT false,
                    "Deleted" boolean NOT NULL DEFAULT false,
                    CONSTRAINT "PK_DocumentSequences" PRIMARY KEY ("Id")
                );

                DO $sequence_index$
                BEGIN
                    IF NOT EXISTS (
                        SELECT 1
                        FROM pg_class index_class
                        JOIN pg_index index_info ON index_info.indexrelid = index_class.oid
                        WHERE index_class.relname = 'IX_DocumentSequences_Company_Store_Type_Date'
                          AND index_info.indisunique
                          AND index_info.indnullsnotdistinct
                          AND index_info.indpred IS NOT NULL
                    ) THEN
                        UPDATE "DocumentSequences"
                        SET "DocumentType" = btrim("DocumentType"),
                            "SequenceDate" = date_trunc('day', "SequenceDate")
                        WHERE NOT "Deleted";

                        WITH ranked AS (
                            SELECT
                                "Id",
                                max("LastNumber") OVER (
                                    PARTITION BY "CompanyId", "StoreGroupId", "StoreId", "DocumentType", "SequenceDate"
                                ) AS max_number,
                                row_number() OVER (
                                    PARTITION BY "CompanyId", "StoreGroupId", "StoreId", "DocumentType", "SequenceDate"
                                    ORDER BY "CreatedAt", "Id"
                                ) AS row_number
                            FROM "DocumentSequences"
                            WHERE NOT "Deleted"
                        )
                        UPDATE "DocumentSequences" target
                        SET "LastNumber" = ranked.max_number,
                            "Deleted" = ranked.row_number > 1,
                            "UpdatedAt" = now()
                        FROM ranked
                        WHERE target."Id" = ranked."Id";

                        DROP INDEX IF EXISTS "IX_DocumentSequences_Company_Store_Type_Date";
                        CREATE UNIQUE INDEX "IX_DocumentSequences_Company_Store_Type_Date"
                            ON "DocumentSequences" ("CompanyId", "StoreGroupId", "StoreId", "DocumentType", "SequenceDate")
                            NULLS NOT DISTINCT
                            WHERE "Deleted" = false;
                    END IF;
                END
                $sequence_index$;

                CREATE INDEX IF NOT EXISTS "IX_PurchaseInvoices_CompanyId_StoreId_InwardNumber" ON "PurchaseInvoices" ("CompanyId", "StoreId", "InwardNumber");
                CREATE INDEX IF NOT EXISTS "IX_PurchasePayments_CompanyId_StoreId_PurchaseInvoiceId_OnDate" ON "PurchasePayments" ("CompanyId", "StoreId", "PurchaseInvoiceId", "OnDate");
                CREATE INDEX IF NOT EXISTS "IX_PurchasePayments_CompanyId_VendorId_OnDate" ON "PurchasePayments" ("CompanyId", "VendorId", "OnDate");
                CREATE INDEX IF NOT EXISTS "IX_PurchaseReturns_CompanyId_StoreId_ReturnNumber" ON "PurchaseReturns" ("CompanyId", "StoreId", "ReturnNumber");
                CREATE INDEX IF NOT EXISTS "IX_PurchaseReturns_CompanyId_PurchaseInvoiceId_OnDate" ON "PurchaseReturns" ("CompanyId", "PurchaseInvoiceId", "OnDate");
                CREATE INDEX IF NOT EXISTS "IX_PurchaseReturnItems_CompanyId_PurchaseReturnId" ON "PurchaseReturnItems" ("CompanyId", "PurchaseReturnId");
                CREATE INDEX IF NOT EXISTS "IX_PurchaseReturnItems_CompanyId_PurchaseInvoiceId_PurchaseInvoiceItemId" ON "PurchaseReturnItems" ("CompanyId", "PurchaseInvoiceId", "PurchaseInvoiceItemId");
                CREATE INDEX IF NOT EXISTS "IX_PurchaseReturnItcReversals_CompanyId_PurchaseReturnId" ON "PurchaseReturnItcReversals" ("CompanyId", "PurchaseReturnId");
                CREATE INDEX IF NOT EXISTS "IX_PurchaseReturnItcReversals_CompanyId_PurchaseInvoiceId_PurchaseInvoiceItemId" ON "PurchaseReturnItcReversals" ("CompanyId", "PurchaseInvoiceId", "PurchaseInvoiceItemId");
                CREATE INDEX IF NOT EXISTS "IX_PurchaseReturnItcReversals_CompanyId_JournalEntryId" ON "PurchaseReturnItcReversals" ("CompanyId", "JournalEntryId");
                CREATE INDEX IF NOT EXISTS "IX_StockMovements_CompanyId_StoreId_ProductId_OnDate" ON "StockMovements" ("CompanyId", "StoreId", "ProductId", "OnDate");
                CREATE INDEX IF NOT EXISTS "IX_StockMovements_CompanyId_SourceType_SourceId" ON "StockMovements" ("CompanyId", "SourceType", "SourceId");
                CREATE INDEX IF NOT EXISTS "IX_StockOperationDocuments_CompanyId_StoreId_DocumentNumber" ON "StockOperationDocuments" ("CompanyId", "StoreId", "DocumentNumber");
                CREATE INDEX IF NOT EXISTS "IX_StockOperationDocuments_CompanyId_OperationType_OnDate" ON "StockOperationDocuments" ("CompanyId", "OperationType", "OnDate");
                ALTER TABLE "BankTransactions" ADD COLUMN IF NOT EXISTS "Reconciled" boolean NOT NULL DEFAULT false;
                ALTER TABLE "BankTransactions" ADD COLUMN IF NOT EXISTS "ReconciledAt" timestamp without time zone NULL;
                ALTER TABLE "BankTransactions" ADD COLUMN IF NOT EXISTS "ReconciledBy" text NULL;
                ALTER TABLE "BankTransactions" ADD COLUMN IF NOT EXISTS "ReconciliationReference" text NULL;
                ALTER TABLE "BankTransactions" ADD COLUMN IF NOT EXISTS "ReconciliationRemarks" text NULL;
                ALTER TABLE "BankStatementLines" ADD COLUMN IF NOT EXISTS "ReconciledAt" timestamp without time zone NULL;
                ALTER TABLE "BankStatementLines" ADD COLUMN IF NOT EXISTS "ReconciledBy" text NULL;
                ALTER TABLE "BankStatementLines" ADD COLUMN IF NOT EXISTS "ReconciliationReference" text NULL;
                ALTER TABLE "BankStatementLines" ADD COLUMN IF NOT EXISTS "ReconciliationRemarks" text NULL;
                ALTER TABLE "ChequeLogs" ADD COLUMN IF NOT EXISTS "BankTransactionId" uuid NULL;
                ALTER TABLE "ChequeLogs" ADD COLUMN IF NOT EXISTS "DepositedAt" timestamp without time zone NULL;
                ALTER TABLE "ChequeLogs" ADD COLUMN IF NOT EXISTS "ClearedAt" timestamp without time zone NULL;
                ALTER TABLE "ChequeLogs" ADD COLUMN IF NOT EXISTS "BouncedAt" timestamp without time zone NULL;
                ALTER TABLE "ChequeLogs" ADD COLUMN IF NOT EXISTS "CancelledAt" timestamp without time zone NULL;
                ALTER TABLE "ChequeLogs" ADD COLUMN IF NOT EXISTS "LifecycleRemarks" text NULL;
                CREATE INDEX IF NOT EXISTS "IX_BankTransactions_CompanyId_BankAccountId_Reconciled" ON "BankTransactions" ("CompanyId", "BankAccountId", "Reconciled");
                CREATE INDEX IF NOT EXISTS "IX_BankStatementLines_CompanyId_BankAccountId_Reconciled" ON "BankStatementLines" ("CompanyId", "BankAccountId", "Reconciled");
                CREATE INDEX IF NOT EXISTS "IX_ChequeLogs_CompanyId_Status_OnDate" ON "ChequeLogs" ("CompanyId", "Status", "OnDate");



                CREATE TABLE IF NOT EXISTS "TailoringServiceItems" (
                    "Id" uuid NOT NULL,
                    "ServiceCode" text NOT NULL DEFAULT '',
                    "Name" text NOT NULL DEFAULT '',
                    "Category" integer NOT NULL DEFAULT 0,
                    "DefaultCustomerRate" numeric(18,2) NOT NULL DEFAULT 0,
                    "DefaultVendorRate" numeric(18,2) NOT NULL DEFAULT 0,
                    "TaxRate" numeric(18,2) NOT NULL DEFAULT 0,
                    "HSNCode" text NULL,
                    "ProductId" uuid NULL,
                    "Active" boolean NOT NULL DEFAULT true,
                    "Remarks" text NULL,
                    "CompanyId" uuid NOT NULL,
                    "CreatedBy" text NULL,
                    "StoreGroupId" uuid NOT NULL,
                    "StoreId" uuid NOT NULL,
                    "CreatedAt" timestamp without time zone NOT NULL DEFAULT now(),
                    "UpdatedAt" timestamp without time zone NULL,
                    "Synced" boolean NOT NULL DEFAULT false,
                    "Deleted" boolean NOT NULL DEFAULT false,
                    CONSTRAINT "PK_TailoringServiceItems" PRIMARY KEY ("Id")
                );


                CREATE TABLE IF NOT EXISTS "TailoringVendorServiceRates" (
                    "Id" uuid NOT NULL,
                    "VendorId" uuid NOT NULL,
                    "ServiceItemId" uuid NOT NULL,
                    "CustomerRate" numeric(18,2) NOT NULL DEFAULT 0,
                    "VendorRate" numeric(18,2) NOT NULL DEFAULT 0,
                    "EffectiveFrom" timestamp without time zone NULL,
                    "Active" boolean NOT NULL DEFAULT true,
                    "Remarks" text NULL,
                    "CompanyId" uuid NOT NULL,
                    "CreatedBy" text NULL,
                    "StoreGroupId" uuid NOT NULL,
                    "StoreId" uuid NOT NULL,
                    "CreatedAt" timestamp without time zone NOT NULL DEFAULT now(),
                    "UpdatedAt" timestamp without time zone NULL,
                    "Synced" boolean NOT NULL DEFAULT false,
                    "Deleted" boolean NOT NULL DEFAULT false,
                    CONSTRAINT "PK_TailoringVendorServiceRates" PRIMARY KEY ("Id")
                );

                CREATE TABLE IF NOT EXISTS "TailoringOrders" (
                    "Id" uuid NOT NULL,
                    "OrderNumber" text NOT NULL DEFAULT '',
                    "OnDate" timestamp without time zone NOT NULL DEFAULT now(),
                    "OrderType" integer NOT NULL DEFAULT 0,
                    "Status" integer NOT NULL DEFAULT 1,
                    "CustomerId" uuid NOT NULL,
                    "CustomerName" text NOT NULL DEFAULT '',
                    "CustomerMobileNumber" text NULL,
                    "VendorId" uuid NULL,
                    "VendorName" text NULL,
                    "SourceInvoiceId" uuid NULL,
                    "SourceInvoiceNumber" text NULL,
                    "SourceInvoiceItemId" uuid NULL,
                    "SourceProductId" uuid NULL,
                    "SourceProductName" text NULL,
                    "SourceBarcode" text NULL,
                    "ExpectedDeliveryDate" timestamp without time zone NULL,
                    "DeliveredAt" timestamp without time zone NULL,
                    "MeasurementsJson" text NULL,
                    "CustomerInstructions" text NULL,
                    "InternalRemarks" text NULL,
                    "CustomerChargeAmount" numeric(18,2) NOT NULL DEFAULT 0,
                    "VendorCostAmount" numeric(18,2) NOT NULL DEFAULT 0,
                    "InHouseExpenseAmount" numeric(18,2) NOT NULL DEFAULT 0,
                    "CustomerReceivedAmount" numeric(18,2) NOT NULL DEFAULT 0,
                    "VendorPaidAmount" numeric(18,2) NOT NULL DEFAULT 0,
                    "CustomerBalanceAmount" numeric(18,2) NOT NULL DEFAULT 0,
                    "VendorBalanceAmount" numeric(18,2) NOT NULL DEFAULT 0,
                    "ProfitImpactAmount" numeric(18,2) NOT NULL DEFAULT 0,
                    "ServiceInvoiceId" uuid NULL,
                    "ServiceInvoiceNumber" text NULL,
                    "ClosedAt" timestamp without time zone NULL,
                    "CompanyId" uuid NOT NULL,
                    "CreatedBy" text NULL,
                    "StoreGroupId" uuid NOT NULL,
                    "StoreId" uuid NOT NULL,
                    "CreatedAt" timestamp without time zone NOT NULL DEFAULT now(),
                    "UpdatedAt" timestamp without time zone NULL,
                    "Synced" boolean NOT NULL DEFAULT false,
                    "Deleted" boolean NOT NULL DEFAULT false,
                    CONSTRAINT "PK_TailoringOrders" PRIMARY KEY ("Id")
                );

                CREATE TABLE IF NOT EXISTS "TailoringOrderLines" (
                    "Id" uuid NOT NULL,
                    "TailoringOrderId" uuid NOT NULL,
                    "ServiceItemId" uuid NULL,
                    "ServiceName" text NOT NULL DEFAULT '',
                    "Category" integer NOT NULL DEFAULT 0,
                    "GarmentName" text NULL,
                    "Barcode" text NULL,
                    "Quantity" numeric(18,2) NOT NULL DEFAULT 1,
                    "CustomerRate" numeric(18,2) NOT NULL DEFAULT 0,
                    "VendorRate" numeric(18,2) NOT NULL DEFAULT 0,
                    "DiscountAmount" numeric(18,2) NOT NULL DEFAULT 0,
                    "CustomerChargeAmount" numeric(18,2) NOT NULL DEFAULT 0,
                    "VendorCostAmount" numeric(18,2) NOT NULL DEFAULT 0,
                    "CostResponsibility" integer NOT NULL DEFAULT 0,
                    "ExpectedDeliveryDate" timestamp without time zone NULL,
                    "DeliveredAt" timestamp without time zone NULL,
                    "Status" integer NOT NULL DEFAULT 1,
                    "MeasurementsJson" text NULL,
                    "Instructions" text NULL,
                    "VendorRemarks" text NULL,
                    "CompanyId" uuid NOT NULL,
                    "CreatedBy" text NULL,
                    "CreatedAt" timestamp without time zone NOT NULL DEFAULT now(),
                    "UpdatedAt" timestamp without time zone NULL,
                    "Synced" boolean NOT NULL DEFAULT false,
                    "Deleted" boolean NOT NULL DEFAULT false,
                    CONSTRAINT "PK_TailoringOrderLines" PRIMARY KEY ("Id")
                );

                CREATE TABLE IF NOT EXISTS "TailoringCustomerReceipts" (
                    "Id" uuid NOT NULL,
                    "TailoringOrderId" uuid NOT NULL,
                    "OnDate" timestamp without time zone NOT NULL DEFAULT now(),
                    "Amount" numeric(18,2) NOT NULL DEFAULT 0,
                    "PaymentMode" integer NOT NULL DEFAULT 0,
                    "BankAccountId" uuid NULL,
                    "ReferenceNumber" text NULL,
                    "Remarks" text NULL,
                    "InvoicePaymentId" uuid NULL,
                    "CompanyId" uuid NOT NULL,
                    "CreatedBy" text NULL,
                    "StoreGroupId" uuid NOT NULL,
                    "StoreId" uuid NOT NULL,
                    "CreatedAt" timestamp without time zone NOT NULL DEFAULT now(),
                    "UpdatedAt" timestamp without time zone NULL,
                    "Synced" boolean NOT NULL DEFAULT false,
                    "Deleted" boolean NOT NULL DEFAULT false,
                    CONSTRAINT "PK_TailoringCustomerReceipts" PRIMARY KEY ("Id")
                );

                CREATE TABLE IF NOT EXISTS "TailoringVendorPayments" (
                    "Id" uuid NOT NULL,
                    "TailoringOrderId" uuid NOT NULL,
                    "VendorId" uuid NOT NULL,
                    "OnDate" timestamp without time zone NOT NULL DEFAULT now(),
                    "Amount" numeric(18,2) NOT NULL DEFAULT 0,
                    "PaymentMode" integer NOT NULL DEFAULT 0,
                    "BankAccountId" uuid NULL,
                    "ReferenceNumber" text NULL,
                    "VoucherId" uuid NULL,
                    "Remarks" text NULL,
                    "CompanyId" uuid NOT NULL,
                    "CreatedBy" text NULL,
                    "StoreGroupId" uuid NOT NULL,
                    "StoreId" uuid NOT NULL,
                    "CreatedAt" timestamp without time zone NOT NULL DEFAULT now(),
                    "UpdatedAt" timestamp without time zone NULL,
                    "Synced" boolean NOT NULL DEFAULT false,
                    "Deleted" boolean NOT NULL DEFAULT false,
                    CONSTRAINT "PK_TailoringVendorPayments" PRIMARY KEY ("Id")
                );

                CREATE TABLE IF NOT EXISTS "TailoringOrderHistories" (
                    "Id" uuid NOT NULL,
                    "TailoringOrderId" uuid NOT NULL,
                    "EventDate" timestamp without time zone NOT NULL DEFAULT now(),
                    "Action" text NOT NULL DEFAULT '',
                    "FromStatus" integer NULL,
                    "ToStatus" integer NULL,
                    "Actor" text NULL,
                    "Remarks" text NULL,
                    "DetailsJson" text NULL,
                    "CompanyId" uuid NOT NULL,
                    "CreatedBy" text NULL,
                    "CreatedAt" timestamp without time zone NOT NULL DEFAULT now(),
                    "UpdatedAt" timestamp without time zone NULL,
                    "Synced" boolean NOT NULL DEFAULT false,
                    "Deleted" boolean NOT NULL DEFAULT false,
                    CONSTRAINT "PK_TailoringOrderHistories" PRIMARY KEY ("Id")
                );

                CREATE INDEX IF NOT EXISTS "IX_TailoringServiceItems_CompanyId_StoreId_ServiceCode" ON "TailoringServiceItems" ("CompanyId", "StoreId", "ServiceCode");
                CREATE INDEX IF NOT EXISTS "IX_TailoringServiceItems_CompanyId_StoreId_Category_Active" ON "TailoringServiceItems" ("CompanyId", "StoreId", "Category", "Active");
                CREATE INDEX IF NOT EXISTS "IX_TailoringVendorServiceRates_CompanyId_StoreId_VendorId_ServiceItemId_Active" ON "TailoringVendorServiceRates" ("CompanyId", "StoreId", "VendorId", "ServiceItemId", "Active");
                CREATE INDEX IF NOT EXISTS "IX_TailoringVendorServiceRates_CompanyId_VendorId_ServiceItemId" ON "TailoringVendorServiceRates" ("CompanyId", "VendorId", "ServiceItemId");

                CREATE INDEX IF NOT EXISTS "IX_TailoringOrders_CompanyId_StoreId_OrderNumber" ON "TailoringOrders" ("CompanyId", "StoreId", "OrderNumber");
                CREATE INDEX IF NOT EXISTS "IX_TailoringOrders_CompanyId_StoreId_Status_ExpectedDeliveryDate" ON "TailoringOrders" ("CompanyId", "StoreId", "Status", "ExpectedDeliveryDate");
                CREATE INDEX IF NOT EXISTS "IX_TailoringOrders_CompanyId_CustomerId_OnDate" ON "TailoringOrders" ("CompanyId", "CustomerId", "OnDate");
                CREATE INDEX IF NOT EXISTS "IX_TailoringOrders_CompanyId_VendorId_Status" ON "TailoringOrders" ("CompanyId", "VendorId", "Status");
                CREATE INDEX IF NOT EXISTS "IX_TailoringOrderLines_CompanyId_TailoringOrderId" ON "TailoringOrderLines" ("CompanyId", "TailoringOrderId");
                CREATE INDEX IF NOT EXISTS "IX_TailoringCustomerReceipts_CompanyId_TailoringOrderId_OnDate" ON "TailoringCustomerReceipts" ("CompanyId", "TailoringOrderId", "OnDate");
                CREATE INDEX IF NOT EXISTS "IX_TailoringVendorPayments_CompanyId_TailoringOrderId_OnDate" ON "TailoringVendorPayments" ("CompanyId", "TailoringOrderId", "OnDate");
                CREATE INDEX IF NOT EXISTS "IX_TailoringOrderHistories_CompanyId_TailoringOrderId_EventDate" ON "TailoringOrderHistories" ("CompanyId", "TailoringOrderId", "EventDate");

                ALTER TABLE "StockOperationDocuments" ADD COLUMN IF NOT EXISTS "AccountingStatus" text NOT NULL DEFAULT 'Pending';
                ALTER TABLE "StockOperationDocuments" ADD COLUMN IF NOT EXISTS "JournalEntryId" uuid NULL;
                CREATE INDEX IF NOT EXISTS "IX_StockOperationDocuments_CompanyId_JournalEntryId" ON "StockOperationDocuments" ("CompanyId", "JournalEntryId");
                CREATE INDEX IF NOT EXISTS "IX_StockOperationItems_CompanyId_StockOperationDocumentId" ON "StockOperationItems" ("CompanyId", "StockOperationDocumentId");
                CREATE INDEX IF NOT EXISTS "IX_StockOperationItems_CompanyId_ProductId_StockId" ON "StockOperationItems" ("CompanyId", "ProductId", "StockId");

                CREATE INDEX IF NOT EXISTS "IX_Attendance_CompanyId_StoreId_OnDate" ON "Attendance" ("CompanyId", "StoreId", "OnDate");
                CREATE INDEX IF NOT EXISTS "IX_Attendance_CompanyId_StoreId_EmployeeId_OnDate" ON "Attendance" ("CompanyId", "StoreId", "EmployeeId", "OnDate");
                CREATE INDEX IF NOT EXISTS "IX_PurchaseInvoices_CompanyId_StoreId_OnDate" ON "PurchaseInvoices" ("CompanyId", "StoreId", "OnDate");
                CREATE INDEX IF NOT EXISTS "IX_PurchaseInvoices_CompanyId_StoreId_InwardDate" ON "PurchaseInvoices" ("CompanyId", "StoreId", "InwardDate");
                CREATE INDEX IF NOT EXISTS "IX_PurchaseInvoices_CompanyId_StoreId_VendorId_OnDate" ON "PurchaseInvoices" ("CompanyId", "StoreId", "VendorId", "OnDate");
                CREATE INDEX IF NOT EXISTS "IX_InvoiceItems_CompanyId_InvoiceId" ON "InvoiceItems" ("CompanyId", "InvoiceId");
                CREATE INDEX IF NOT EXISTS "IX_Stocks_CompanyId_StoreId_ProductId_IsOFB" ON "Stocks" ("CompanyId", "StoreId", "ProductId", "IsOFB");
                CREATE INDEX IF NOT EXISTS "IX_Stocks_CompanyId_StoreId_Barcode" ON "Stocks" ("CompanyId", "StoreId", "Barcode");
                CREATE INDEX IF NOT EXISTS "IX_ProductDetails_CompanyId_ProductId_Brand" ON "ProductDetails" ("CompanyId", "ProductId", "Brand");
                CREATE INDEX IF NOT EXISTS "IX_StockMovements_CompanyId_StoreId_ProductId_OnDate" ON "StockMovements" ("CompanyId", "StoreId", "ProductId", "OnDate");
                """, cancellationToken);

            await db.Database.ExecuteSqlRawAsync("""
                CREATE TABLE IF NOT EXISTS "PurchaseInvoiceImportBatches" (
                    "Id" uuid NOT NULL,
                    "CreatedAt" timestamp without time zone NOT NULL DEFAULT now(),
                    "UpdatedAt" timestamp without time zone NULL,
                    "Synced" boolean NOT NULL DEFAULT false,
                    "Deleted" boolean NOT NULL DEFAULT false,
                    "CompanyId" uuid NOT NULL,
                    "CreatedBy" text NULL,
                    "StoreGroupId" uuid NOT NULL,
                    "StoreId" uuid NOT NULL,
                    "Status" text NOT NULL DEFAULT 'Uploaded',
                    "SourceFileName" text NOT NULL DEFAULT '',
                    "StoredFilePath" text NOT NULL DEFAULT '',
                    "ContentType" text NOT NULL DEFAULT '',
                    "FileSizeBytes" bigint NOT NULL DEFAULT 0,
                    "Sha256Hash" text NOT NULL DEFAULT '',
                    "OcrProvider" text NOT NULL DEFAULT '',
                    "OcrStatus" text NOT NULL DEFAULT 'Pending',
                    "RawTextPath" text NULL,
                    "RawJsonPath" text NULL,
                    "ConfidenceScore" numeric(18,2) NOT NULL DEFAULT 0,
                    "ParserTemplate" text NULL,
                    "ParserTemplateReason" text NULL,
                    "ImportQaNotes" text NULL,
                    "AcceptanceStatus" text NULL,
                    "AcceptanceNotes" text NULL,
                    "AcceptanceTestedAt" timestamp without time zone NULL,
                    "AcceptanceTestedBy" text NULL,
                    "CorrectionStatus" text NULL,
                    "CorrectionNotes" text NULL,
                    "CorrectionRequestedAt" timestamp without time zone NULL,
                    "CorrectionRequestedBy" text NULL,
                    "VendorId" uuid NULL,
                    "VendorNameRaw" text NULL,
                    "VendorNameFinal" text NULL,
                    "VendorGstinRaw" text NULL,
                    "VendorGstinFinal" text NULL,
                    "VendorMobileNumber" text NULL,
                    "VendorAddress" text NULL,
                    "SupplierInvoiceNumber" text NULL,
                    "SupplierInvoiceDate" timestamp without time zone NULL,
                    "DueDate" timestamp without time zone NULL,
                    "TaxableAmount" numeric(18,2) NOT NULL DEFAULT 0,
                    "CgstAmount" numeric(18,2) NOT NULL DEFAULT 0,
                    "SgstAmount" numeric(18,2) NOT NULL DEFAULT 0,
                    "IgstAmount" numeric(18,2) NOT NULL DEFAULT 0,
                    "FreightAmount" numeric(18,2) NOT NULL DEFAULT 0,
                    "DiscountAmount" numeric(18,2) NOT NULL DEFAULT 0,
                    "RoundOff" numeric(18,2) NOT NULL DEFAULT 0,
                    "BillAmount" numeric(18,2) NOT NULL DEFAULT 0,
                    "PaidAmount" numeric(18,2) NOT NULL DEFAULT 0,
                    "PaymentMode" integer NOT NULL DEFAULT 0,
                    "BankAccountId" uuid NULL,
                    "DuplicatePurchaseInvoiceId" uuid NULL,
                    "PostedPurchaseInvoiceId" uuid NULL,
                    "ErrorMessage" text NULL,
                    "PostedAt" timestamp without time zone NULL,
                    "RejectedAt" timestamp without time zone NULL,
                    "VerifiedBy" text NULL,
                    "DuplicateOverrideReason" text NULL,
                    "DuplicateOverrideBy" text NULL,
                    "DuplicateOverrideAt" timestamp without time zone NULL,
                    CONSTRAINT "PK_PurchaseInvoiceImportBatches" PRIMARY KEY ("Id")
                );

                CREATE TABLE IF NOT EXISTS "PurchaseInvoiceImportLines" (
                    "Id" uuid NOT NULL,
                    "CreatedAt" timestamp without time zone NOT NULL DEFAULT now(),
                    "UpdatedAt" timestamp without time zone NULL,
                    "Synced" boolean NOT NULL DEFAULT false,
                    "Deleted" boolean NOT NULL DEFAULT false,
                    "CompanyId" uuid NOT NULL,
                    "CreatedBy" text NULL,
                    "StoreGroupId" uuid NOT NULL,
                    "StoreId" uuid NOT NULL,
                    "BatchId" uuid NOT NULL,
                    "LineNumber" integer NOT NULL DEFAULT 0,
                    "ProductId" uuid NULL,
                    "ProductNameRaw" text NULL,
                    "ProductNameFinal" text NULL,
                    "BarcodeRaw" text NULL,
                    "BarcodeFinal" text NULL,
                    "HsnCode" text NULL,
                    "Unit" integer NOT NULL DEFAULT 2,
                    "Quantity" numeric(18,2) NOT NULL DEFAULT 0,
                    "Mrp" numeric(18,2) NOT NULL DEFAULT 0,
                    "CostPrice" numeric(18,2) NOT NULL DEFAULT 0,
                    "UnitDiscount" numeric(18,2) NOT NULL DEFAULT 0,
                    "LineDiscount" numeric(18,2) NOT NULL DEFAULT 0,
                    "TaxRate" numeric(18,2) NOT NULL DEFAULT 0,
                    "GstPriceMode" text NOT NULL DEFAULT 'Inclusive',
                    "TaxId" uuid NULL,
                    "TaxableAmount" numeric(18,2) NOT NULL DEFAULT 0,
                    "TaxAmount" numeric(18,2) NOT NULL DEFAULT 0,
                    "CgstAmount" numeric(18,2) NOT NULL DEFAULT 0,
                    "SgstAmount" numeric(18,2) NOT NULL DEFAULT 0,
                    "IgstAmount" numeric(18,2) NOT NULL DEFAULT 0,
                    "LineTotal" numeric(18,2) NOT NULL DEFAULT 0,
                    "ConfidenceScore" numeric(18,2) NOT NULL DEFAULT 0,
                    "MatchStatus" text NOT NULL DEFAULT 'NeedsManualReview',
                    "ReviewRequired" boolean NOT NULL DEFAULT true,
                    "ReviewMessage" text NULL,
                    "ProductCategoryId" uuid NULL,
                    "ProductSubCategoryId" uuid NULL,
                    "ProductType" integer NOT NULL DEFAULT 0,
                    "ProductGroup" integer NOT NULL DEFAULT 0,
                    "Ignored" boolean NOT NULL DEFAULT false,
                    CONSTRAINT "PK_PurchaseInvoiceImportLines" PRIMARY KEY ("Id")
                );

                CREATE TABLE IF NOT EXISTS "PurchaseInvoiceImportFiles" (
                    "Id" uuid NOT NULL,
                    "CreatedAt" timestamp without time zone NOT NULL DEFAULT now(),
                    "UpdatedAt" timestamp without time zone NULL,
                    "Synced" boolean NOT NULL DEFAULT false,
                    "Deleted" boolean NOT NULL DEFAULT false,
                    "CompanyId" uuid NOT NULL,
                    "CreatedBy" text NULL,
                    "StoreGroupId" uuid NOT NULL,
                    "StoreId" uuid NOT NULL,
                    "BatchId" uuid NOT NULL,
                    "FileKind" text NOT NULL DEFAULT 'OriginalUpload',
                    "OriginalFileName" text NOT NULL DEFAULT '',
                    "StoredFilePath" text NOT NULL DEFAULT '',
                    "ContentType" text NOT NULL DEFAULT '',
                    "FileSizeBytes" bigint NOT NULL DEFAULT 0,
                    "Sha256Hash" text NOT NULL DEFAULT '',
                    CONSTRAINT "PK_PurchaseInvoiceImportFiles" PRIMARY KEY ("Id")
                );

                CREATE TABLE IF NOT EXISTS "PurchaseInvoiceImportVendorProfiles" (
                    "Id" uuid NOT NULL,
                    "CreatedAt" timestamp without time zone NOT NULL DEFAULT now(),
                    "UpdatedAt" timestamp without time zone NULL,
                    "Synced" boolean NOT NULL DEFAULT false,
                    "Deleted" boolean NOT NULL DEFAULT false,
                    "CompanyId" uuid NOT NULL,
                    "CreatedBy" text NULL,
                    "StoreGroupId" uuid NOT NULL,
                    "StoreId" uuid NOT NULL,
                    "VendorId" uuid NULL,
                    "VendorGstin" text NULL,
                    "VendorName" text NULL,
                    "IgnoredLinePatternsJson" text NULL,
                    "ProductAliasesJson" text NULL,
                    "LastLearnedAt" timestamp without time zone NULL,
                    "SuccessfulDraftCount" integer NOT NULL DEFAULT 0,
                    "LearningNotes" text NULL,
                    "PreferredParserTemplate" text NULL,
                    CONSTRAINT "PK_PurchaseInvoiceImportVendorProfiles" PRIMARY KEY ("Id")
                );

                ALTER TABLE IF EXISTS "PurchaseInvoiceImportBatches" ADD COLUMN IF NOT EXISTS "DuplicateOverrideReason" text NULL;
                ALTER TABLE IF EXISTS "PurchaseInvoiceImportBatches" ADD COLUMN IF NOT EXISTS "DuplicateOverrideBy" text NULL;
                ALTER TABLE IF EXISTS "PurchaseInvoiceImportBatches" ADD COLUMN IF NOT EXISTS "DuplicateOverrideAt" timestamp without time zone NULL;
                ALTER TABLE IF EXISTS "PurchaseInvoiceImportLines" ADD COLUMN IF NOT EXISTS "GstPriceMode" text NOT NULL DEFAULT 'Inclusive';
                ALTER TABLE IF EXISTS "PurchaseInvoiceImportBatches" ADD COLUMN IF NOT EXISTS "ParserTemplate" text NULL;
                ALTER TABLE IF EXISTS "PurchaseInvoiceImportBatches" ADD COLUMN IF NOT EXISTS "ParserTemplateReason" text NULL;
                ALTER TABLE IF EXISTS "PurchaseInvoiceImportBatches" ADD COLUMN IF NOT EXISTS "ImportQaNotes" text NULL;
                ALTER TABLE IF EXISTS "PurchaseInvoiceImportBatches" ADD COLUMN IF NOT EXISTS "AcceptanceStatus" text NULL;
                ALTER TABLE IF EXISTS "PurchaseInvoiceImportBatches" ADD COLUMN IF NOT EXISTS "AcceptanceNotes" text NULL;
                ALTER TABLE IF EXISTS "PurchaseInvoiceImportBatches" ADD COLUMN IF NOT EXISTS "AcceptanceTestedAt" timestamp without time zone NULL;
                ALTER TABLE IF EXISTS "PurchaseInvoiceImportBatches" ADD COLUMN IF NOT EXISTS "AcceptanceTestedBy" text NULL;
                ALTER TABLE IF EXISTS "PurchaseInvoiceImportBatches" ADD COLUMN IF NOT EXISTS "CorrectionStatus" text NULL;
                ALTER TABLE IF EXISTS "PurchaseInvoiceImportBatches" ADD COLUMN IF NOT EXISTS "CorrectionNotes" text NULL;
                ALTER TABLE IF EXISTS "PurchaseInvoiceImportBatches" ADD COLUMN IF NOT EXISTS "CorrectionRequestedAt" timestamp without time zone NULL;
                ALTER TABLE IF EXISTS "PurchaseInvoiceImportBatches" ADD COLUMN IF NOT EXISTS "CorrectionRequestedBy" text NULL;
                ALTER TABLE IF EXISTS "PurchaseInvoiceImportVendorProfiles" ADD COLUMN IF NOT EXISTS "PreferredParserTemplate" text NULL;

                CREATE INDEX IF NOT EXISTS "IX_PurchaseInvoiceImportBatches_CompanyId_StoreId_Status_CreatedAt" ON "PurchaseInvoiceImportBatches" ("CompanyId", "StoreId", "Status", "CreatedAt");
                CREATE INDEX IF NOT EXISTS "IX_PurchaseInvoiceImportBatches_CompanyId_VendorGstinFinal_SupplierInvoiceNumber" ON "PurchaseInvoiceImportBatches" ("CompanyId", "VendorGstinFinal", "SupplierInvoiceNumber");
                CREATE INDEX IF NOT EXISTS "IX_PurchaseInvoiceImportBatches_Sha256Hash" ON "PurchaseInvoiceImportBatches" ("Sha256Hash");
                CREATE INDEX IF NOT EXISTS "IX_PurchaseInvoiceImportBatches_PostedPurchaseInvoiceId" ON "PurchaseInvoiceImportBatches" ("PostedPurchaseInvoiceId");
                CREATE INDEX IF NOT EXISTS "IX_PurchaseInvoiceImportBatches_CompanyId_AcceptanceStatus" ON "PurchaseInvoiceImportBatches" ("CompanyId", "AcceptanceStatus");
                CREATE INDEX IF NOT EXISTS "IX_PurchaseInvoiceImportBatches_CompanyId_CorrectionStatus" ON "PurchaseInvoiceImportBatches" ("CompanyId", "CorrectionStatus");
                CREATE INDEX IF NOT EXISTS "IX_PurchaseInvoiceImportLines_CompanyId_BatchId_LineNumber" ON "PurchaseInvoiceImportLines" ("CompanyId", "BatchId", "LineNumber");
                CREATE INDEX IF NOT EXISTS "IX_PurchaseInvoiceImportLines_CompanyId_ProductId" ON "PurchaseInvoiceImportLines" ("CompanyId", "ProductId");
                CREATE INDEX IF NOT EXISTS "IX_PurchaseInvoiceImportFiles_CompanyId_BatchId_FileKind" ON "PurchaseInvoiceImportFiles" ("CompanyId", "BatchId", "FileKind");
                CREATE INDEX IF NOT EXISTS "IX_PurchaseInvoiceImportFiles_Sha256Hash" ON "PurchaseInvoiceImportFiles" ("Sha256Hash");
                CREATE INDEX IF NOT EXISTS "IX_PurchaseInvoiceImportVendorProfiles_CompanyId_VendorGstin" ON "PurchaseInvoiceImportVendorProfiles" ("CompanyId", "VendorGstin");
                CREATE INDEX IF NOT EXISTS "IX_PurchaseInvoiceImportVendorProfiles_CompanyId_VendorId" ON "PurchaseInvoiceImportVendorProfiles" ("CompanyId", "VendorId");
                """, cancellationToken);

            logger.LogInformation("Known database schema drift repair check completed.");
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Known database schema drift repair check failed. Startup will continue; affected endpoints may fail until migrations are applied manually.");
        }
    }
}
