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

public static async Task RepairKnownSchemaDriftAsync(GarmetixDbContext db, ILogger logger, CancellationToken cancellationToken = default)
    {
        try
        {
            await RepairGstReturnStorageAsync(db, logger, cancellationToken);
            await RepairCashVoucherConversionStorageAsync(db, logger, cancellationToken);
            await RepairStoreDayStorageAsync(db, logger, cancellationToken);

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
                UPDATE "Users" SET "Admin" = ("Role" = 0);

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
                """, cancellationToken);

            logger.LogInformation("Known database schema drift repair check completed.");
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Known database schema drift repair check failed. Startup will continue; affected endpoints may fail until migrations are applied manually.");
        }
    }
}
