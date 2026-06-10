using Garmetix.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Garmetix.Api.Database;

public static class DatabaseSchemaRepairService
{
    
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

public static async Task RepairKnownSchemaDriftAsync(GarmetixDbContext db, ILogger logger, CancellationToken cancellationToken = default)
    {
        try
        {
            await RepairGstReturnStorageAsync(db, logger, cancellationToken);

            // Some development databases may already have the migration recorded in
            // __EFMigrationsHistory but can still be missing columns when older ZIPs were
            // tested in between. These statements are idempotent and only add missing columns.
            await db.Database.ExecuteSqlRawAsync("""
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

                ALTER TABLE IF EXISTS "PurchaseInvoiceItems" ADD COLUMN IF NOT EXISTS "ProductName" text NULL;
                ALTER TABLE IF EXISTS "PurchaseInvoiceItems" ADD COLUMN IF NOT EXISTS "HSNCode" text NULL;
                ALTER TABLE IF EXISTS "PurchaseInvoiceItems" ADD COLUMN IF NOT EXISTS "Unit" integer NULL;
                ALTER TABLE IF EXISTS "PurchaseInvoiceItems" ADD COLUMN IF NOT EXISTS "ProductCategoryId" uuid NULL;
                ALTER TABLE IF EXISTS "PurchaseInvoiceItems" ADD COLUMN IF NOT EXISTS "ProductSubCategoryId" uuid NULL;
                ALTER TABLE IF EXISTS "PurchaseInvoiceItems" ADD COLUMN IF NOT EXISTS "CGSTAmount" numeric(18,2) NULL;
                ALTER TABLE IF EXISTS "PurchaseInvoiceItems" ADD COLUMN IF NOT EXISTS "SGSTAmount" numeric(18,2) NULL;
                ALTER TABLE IF EXISTS "PurchaseInvoiceItems" ADD COLUMN IF NOT EXISTS "IGSTAmount" numeric(18,2) NULL;

                ALTER TABLE IF EXISTS "InvoicePayments" ADD COLUMN IF NOT EXISTS "BankAccountId" uuid NULL;
                ALTER TABLE IF EXISTS "InvoicePayments" ADD COLUMN IF NOT EXISTS "AdjustmentSourceType" text NULL;
                ALTER TABLE IF EXISTS "InvoicePayments" ADD COLUMN IF NOT EXISTS "AdjustmentSourceId" uuid NULL;
                ALTER TABLE IF EXISTS "InvoicePayments" ADD COLUMN IF NOT EXISTS "GatewayReference" text NULL;
                ALTER TABLE IF EXISTS "InvoicePayments" ADD COLUMN IF NOT EXISTS "SettlementStatus" text NULL;

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

                CREATE INDEX IF NOT EXISTS "IX_DocumentSequences_Company_Store_Type_Date" ON "DocumentSequences" ("CompanyId", "StoreGroupId", "StoreId", "DocumentType", "SequenceDate");

                CREATE INDEX IF NOT EXISTS "IX_PurchaseInvoices_CompanyId_StoreId_InwardNumber" ON "PurchaseInvoices" ("CompanyId", "StoreId", "InwardNumber");
                CREATE INDEX IF NOT EXISTS "IX_PurchasePayments_CompanyId_StoreId_PurchaseInvoiceId_OnDate" ON "PurchasePayments" ("CompanyId", "StoreId", "PurchaseInvoiceId", "OnDate");
                CREATE INDEX IF NOT EXISTS "IX_PurchasePayments_CompanyId_VendorId_OnDate" ON "PurchasePayments" ("CompanyId", "VendorId", "OnDate");
                CREATE INDEX IF NOT EXISTS "IX_StockMovements_CompanyId_StoreId_ProductId_OnDate" ON "StockMovements" ("CompanyId", "StoreId", "ProductId", "OnDate");
                CREATE INDEX IF NOT EXISTS "IX_StockMovements_CompanyId_SourceType_SourceId" ON "StockMovements" ("CompanyId", "SourceType", "SourceId");
                """, cancellationToken);

            logger.LogInformation("Known database schema drift repair check completed.");
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Known database schema drift repair check failed. Startup will continue; affected endpoints may fail until migrations are applied manually.");
        }
    }
}
