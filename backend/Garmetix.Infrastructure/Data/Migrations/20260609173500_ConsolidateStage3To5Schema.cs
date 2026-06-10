using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Garmetix.Infrastructure.Data.Migrations
{
    /// <summary>
    /// Consolidates schema changes that were previously protected by runtime schema repair
    /// during the Stage 3A through Stage 5D implementation cycle.
    ///
    /// This migration is intentionally idempotent because many development/production
    /// databases may already have these tables/columns from older runtime repair checks.
    /// It gives EF migrations a formal record of the current production schema without
    /// forcing destructive changes on existing Docker volumes.
    /// </summary>
    public partial class ConsolidateStage3To5Schema : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                ALTER TABLE IF EXISTS "Customers" ADD COLUMN IF NOT EXISTS "GSTLegalName" text NULL;
                ALTER TABLE IF EXISTS "Customers" ADD COLUMN IF NOT EXISTS "GSTTradeName" text NULL;
                ALTER TABLE IF EXISTS "Customers" ADD COLUMN IF NOT EXISTS "GSTPrincipalAddress" text NULL;
                ALTER TABLE IF EXISTS "Customers" ADD COLUMN IF NOT EXISTS "GSTStateCode" text NULL;
                ALTER TABLE IF EXISTS "Customers" ADD COLUMN IF NOT EXISTS "GSTTaxpayerType" text NULL;
                ALTER TABLE IF EXISTS "Customers" ADD COLUMN IF NOT EXISTS "GSTRegistrationStatus" text NULL;
                ALTER TABLE IF EXISTS "Customers" ADD COLUMN IF NOT EXISTS "GSTVerified" boolean NOT NULL DEFAULT false;
                ALTER TABLE IF EXISTS "Customers" ADD COLUMN IF NOT EXISTS "GSTVerifiedAt" timestamp without time zone NULL;
                ALTER TABLE IF EXISTS "Customers" ADD COLUMN IF NOT EXISTS "GSTLookupSource" text NULL;
                ALTER TABLE IF EXISTS "Customers" ADD COLUMN IF NOT EXISTS "GSTMismatchAlert" text NULL;
                ALTER TABLE IF EXISTS "Customers" ADD COLUMN IF NOT EXISTS "CreditBalance" numeric(18,2) NOT NULL DEFAULT 0;
                ALTER TABLE IF EXISTS "Customers" ADD COLUMN IF NOT EXISTS "LoyaltyPoints" numeric(18,2) NOT NULL DEFAULT 0;

                ALTER TABLE IF EXISTS "Vendors" ADD COLUMN IF NOT EXISTS "GSTLegalName" text NULL;
                ALTER TABLE IF EXISTS "Vendors" ADD COLUMN IF NOT EXISTS "GSTTradeName" text NULL;
                ALTER TABLE IF EXISTS "Vendors" ADD COLUMN IF NOT EXISTS "GSTPrincipalAddress" text NULL;
                ALTER TABLE IF EXISTS "Vendors" ADD COLUMN IF NOT EXISTS "GSTStateCode" text NULL;
                ALTER TABLE IF EXISTS "Vendors" ADD COLUMN IF NOT EXISTS "GSTTaxpayerType" text NULL;
                ALTER TABLE IF EXISTS "Vendors" ADD COLUMN IF NOT EXISTS "GSTRegistrationStatus" text NULL;
                ALTER TABLE IF EXISTS "Vendors" ADD COLUMN IF NOT EXISTS "GSTVerified" boolean NOT NULL DEFAULT false;
                ALTER TABLE IF EXISTS "Vendors" ADD COLUMN IF NOT EXISTS "GSTVerifiedAt" timestamp without time zone NULL;
                ALTER TABLE IF EXISTS "Vendors" ADD COLUMN IF NOT EXISTS "GSTLookupSource" text NULL;
                ALTER TABLE IF EXISTS "Vendors" ADD COLUMN IF NOT EXISTS "GSTMismatchAlert" text NULL;

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

                ALTER TABLE IF EXISTS "PurchasePayments" ADD COLUMN IF NOT EXISTS "PurchaseInvoiceId" uuid NOT NULL DEFAULT '00000000-0000-0000-0000-000000000000';
                ALTER TABLE IF EXISTS "PurchasePayments" ADD COLUMN IF NOT EXISTS "VendorId" uuid NOT NULL DEFAULT '00000000-0000-0000-0000-000000000000';
                ALTER TABLE IF EXISTS "PurchasePayments" ADD COLUMN IF NOT EXISTS "OnDate" timestamp without time zone NOT NULL DEFAULT now();
                ALTER TABLE IF EXISTS "PurchasePayments" ADD COLUMN IF NOT EXISTS "Amount" numeric(18,2) NOT NULL DEFAULT 0;
                ALTER TABLE IF EXISTS "PurchasePayments" ADD COLUMN IF NOT EXISTS "PaymentMode" integer NOT NULL DEFAULT 0;
                ALTER TABLE IF EXISTS "PurchasePayments" ADD COLUMN IF NOT EXISTS "BankAccountId" uuid NULL;
                ALTER TABLE IF EXISTS "PurchasePayments" ADD COLUMN IF NOT EXISTS "ReferenceNumber" text NULL;
                ALTER TABLE IF EXISTS "PurchasePayments" ADD COLUMN IF NOT EXISTS "VoucherId" uuid NULL;
                ALTER TABLE IF EXISTS "PurchasePayments" ADD COLUMN IF NOT EXISTS "Remarks" text NULL;
                ALTER TABLE IF EXISTS "PurchasePayments" ADD COLUMN IF NOT EXISTS "CompanyId" uuid NOT NULL DEFAULT '00000000-0000-0000-0000-000000000000';
                ALTER TABLE IF EXISTS "PurchasePayments" ADD COLUMN IF NOT EXISTS "CreatedBy" text NULL;
                ALTER TABLE IF EXISTS "PurchasePayments" ADD COLUMN IF NOT EXISTS "StoreGroupId" uuid NOT NULL DEFAULT '00000000-0000-0000-0000-000000000000';
                ALTER TABLE IF EXISTS "PurchasePayments" ADD COLUMN IF NOT EXISTS "StoreId" uuid NOT NULL DEFAULT '00000000-0000-0000-0000-000000000000';
                ALTER TABLE IF EXISTS "PurchasePayments" ADD COLUMN IF NOT EXISTS "CreatedAt" timestamp without time zone NOT NULL DEFAULT now();
                ALTER TABLE IF EXISTS "PurchasePayments" ADD COLUMN IF NOT EXISTS "UpdatedAt" timestamp without time zone NULL;
                ALTER TABLE IF EXISTS "PurchasePayments" ADD COLUMN IF NOT EXISTS "Synced" boolean NOT NULL DEFAULT false;
                ALTER TABLE IF EXISTS "PurchasePayments" ADD COLUMN IF NOT EXISTS "Deleted" boolean NOT NULL DEFAULT false;

                ALTER TABLE IF EXISTS "StockMovements" ADD COLUMN IF NOT EXISTS "StockId" uuid NULL;
                ALTER TABLE IF EXISTS "StockMovements" ADD COLUMN IF NOT EXISTS "ProductId" uuid NOT NULL DEFAULT '00000000-0000-0000-0000-000000000000';
                ALTER TABLE IF EXISTS "StockMovements" ADD COLUMN IF NOT EXISTS "Barcode" text NOT NULL DEFAULT '';
                ALTER TABLE IF EXISTS "StockMovements" ADD COLUMN IF NOT EXISTS "MovementType" text NOT NULL DEFAULT '';
                ALTER TABLE IF EXISTS "StockMovements" ADD COLUMN IF NOT EXISTS "QuantityIn" numeric(18,2) NOT NULL DEFAULT 0;
                ALTER TABLE IF EXISTS "StockMovements" ADD COLUMN IF NOT EXISTS "QuantityOut" numeric(18,2) NOT NULL DEFAULT 0;
                ALTER TABLE IF EXISTS "StockMovements" ADD COLUMN IF NOT EXISTS "CostPrice" numeric(18,2) NOT NULL DEFAULT 0;
                ALTER TABLE IF EXISTS "StockMovements" ADD COLUMN IF NOT EXISTS "MRP" numeric(18,2) NOT NULL DEFAULT 0;
                ALTER TABLE IF EXISTS "StockMovements" ADD COLUMN IF NOT EXISTS "TaxRate" numeric(18,2) NOT NULL DEFAULT 0;
                ALTER TABLE IF EXISTS "StockMovements" ADD COLUMN IF NOT EXISTS "HSNCode" text NULL;
                ALTER TABLE IF EXISTS "StockMovements" ADD COLUMN IF NOT EXISTS "SourceType" text NULL;
                ALTER TABLE IF EXISTS "StockMovements" ADD COLUMN IF NOT EXISTS "SourceId" uuid NULL;
                ALTER TABLE IF EXISTS "StockMovements" ADD COLUMN IF NOT EXISTS "SourceNumber" text NULL;
                ALTER TABLE IF EXISTS "StockMovements" ADD COLUMN IF NOT EXISTS "Remarks" text NULL;
                ALTER TABLE IF EXISTS "StockMovements" ADD COLUMN IF NOT EXISTS "OnDate" timestamp without time zone NOT NULL DEFAULT now();
                ALTER TABLE IF EXISTS "StockMovements" ADD COLUMN IF NOT EXISTS "CompanyId" uuid NOT NULL DEFAULT '00000000-0000-0000-0000-000000000000';
                ALTER TABLE IF EXISTS "StockMovements" ADD COLUMN IF NOT EXISTS "CreatedBy" text NULL;
                ALTER TABLE IF EXISTS "StockMovements" ADD COLUMN IF NOT EXISTS "StoreGroupId" uuid NOT NULL DEFAULT '00000000-0000-0000-0000-000000000000';
                ALTER TABLE IF EXISTS "StockMovements" ADD COLUMN IF NOT EXISTS "StoreId" uuid NOT NULL DEFAULT '00000000-0000-0000-0000-000000000000';
                ALTER TABLE IF EXISTS "StockMovements" ADD COLUMN IF NOT EXISTS "CreatedAt" timestamp without time zone NOT NULL DEFAULT now();
                ALTER TABLE IF EXISTS "StockMovements" ADD COLUMN IF NOT EXISTS "UpdatedAt" timestamp without time zone NULL;
                ALTER TABLE IF EXISTS "StockMovements" ADD COLUMN IF NOT EXISTS "Synced" boolean NOT NULL DEFAULT false;
                ALTER TABLE IF EXISTS "StockMovements" ADD COLUMN IF NOT EXISTS "Deleted" boolean NOT NULL DEFAULT false;

                ALTER TABLE IF EXISTS "DocumentSequences" ADD COLUMN IF NOT EXISTS "DocumentType" text NOT NULL DEFAULT '';
                ALTER TABLE IF EXISTS "DocumentSequences" ADD COLUMN IF NOT EXISTS "Prefix" text NOT NULL DEFAULT '';
                ALTER TABLE IF EXISTS "DocumentSequences" ADD COLUMN IF NOT EXISTS "SequenceDate" timestamp without time zone NOT NULL DEFAULT now();
                ALTER TABLE IF EXISTS "DocumentSequences" ADD COLUMN IF NOT EXISTS "LastNumber" integer NOT NULL DEFAULT 0;
                ALTER TABLE IF EXISTS "DocumentSequences" ADD COLUMN IF NOT EXISTS "StoreGroupId" uuid NULL;
                ALTER TABLE IF EXISTS "DocumentSequences" ADD COLUMN IF NOT EXISTS "StoreId" uuid NULL;
                ALTER TABLE IF EXISTS "DocumentSequences" ADD COLUMN IF NOT EXISTS "CompanyId" uuid NOT NULL DEFAULT '00000000-0000-0000-0000-000000000000';
                ALTER TABLE IF EXISTS "DocumentSequences" ADD COLUMN IF NOT EXISTS "CreatedBy" text NULL;
                ALTER TABLE IF EXISTS "DocumentSequences" ADD COLUMN IF NOT EXISTS "CreatedAt" timestamp without time zone NOT NULL DEFAULT now();
                ALTER TABLE IF EXISTS "DocumentSequences" ADD COLUMN IF NOT EXISTS "UpdatedAt" timestamp without time zone NULL;
                ALTER TABLE IF EXISTS "DocumentSequences" ADD COLUMN IF NOT EXISTS "Synced" boolean NOT NULL DEFAULT false;
                ALTER TABLE IF EXISTS "DocumentSequences" ADD COLUMN IF NOT EXISTS "Deleted" boolean NOT NULL DEFAULT false;

                CREATE INDEX IF NOT EXISTS "IX_DocumentSequences_Company_Store_Type_Date" ON "DocumentSequences" ("CompanyId", "StoreGroupId", "StoreId", "DocumentType", "SequenceDate");
                CREATE INDEX IF NOT EXISTS "IX_PurchaseInvoices_CompanyId_StoreId_InwardNumber" ON "PurchaseInvoices" ("CompanyId", "StoreId", "InwardNumber");
                CREATE INDEX IF NOT EXISTS "IX_PurchasePayments_CompanyId_StoreId_PurchaseInvoiceId_OnDate" ON "PurchasePayments" ("CompanyId", "StoreId", "PurchaseInvoiceId", "OnDate");
                CREATE INDEX IF NOT EXISTS "IX_PurchasePayments_CompanyId_VendorId_OnDate" ON "PurchasePayments" ("CompanyId", "VendorId", "OnDate");
                CREATE INDEX IF NOT EXISTS "IX_StockMovements_CompanyId_StoreId_ProductId_OnDate" ON "StockMovements" ("CompanyId", "StoreId", "ProductId", "OnDate");
                CREATE INDEX IF NOT EXISTS "IX_StockMovements_CompanyId_SourceType_SourceId" ON "StockMovements" ("CompanyId", "SourceType", "SourceId");
                CREATE INDEX IF NOT EXISTS "IX_InvoicePayments_CompanyId_StoreId_InvoiceId_OnDate" ON "InvoicePayments" ("CompanyId", "StoreId", "InvoiceId", "OnDate");
                CREATE INDEX IF NOT EXISTS "IX_CardPayments_CompanyId_StoreId_InvoiceId_OnDate" ON "CardPayments" ("CompanyId", "StoreId", "InvoiceId", "OnDate");
                CREATE INDEX IF NOT EXISTS "IX_VendorPayments_CompanyId_VendorId_OnDate" ON "VendorPayments" ("CompanyId", "VendorId", "OnDate");
                """);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // This consolidation migration is deliberately non-destructive.
            // Older Docker volumes may have received these objects from runtime repair
            // instead of EF migration history, so dropping them in Down could remove
            // live production data unexpectedly.
            migrationBuilder.Sql("""
                -- No-op by design. Create a manual rollback script only after a verified backup.
                """);
        }
    }
}
