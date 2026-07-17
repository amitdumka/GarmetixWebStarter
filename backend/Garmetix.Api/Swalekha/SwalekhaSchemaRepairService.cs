using Garmetix.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Garmetix.Api.Swalekha;

/// <summary>
/// Idempotent CREATE TABLE IF NOT EXISTS / ADD COLUMN IF NOT EXISTS repair for swalekha_db,
/// mirroring Garmetix.Api.Database.DatabaseSchemaRepairService's established pattern for the
/// main database. Needed because SwalekhaDbContext's startup EnsureCreatedAsync only creates
/// tables the first time the database has none - it will not add new tables/columns to an
/// already-existing schema on later deploys, exactly the gap this closes.
/// </summary>
public static class SwalekhaSchemaRepairService
{
    public static async Task RepairSwalekhaStorageAsync(SwalekhaDbContext db, ILogger logger, CancellationToken cancellationToken = default)
    {
        await db.Database.ExecuteSqlRawAsync("""
            CREATE TABLE IF NOT EXISTS "SwalekhaAccounts" (
                "Id" uuid NOT NULL,
                "CreatedAt" timestamp without time zone NOT NULL DEFAULT now(),
                "UpdatedAt" timestamp without time zone NULL,
                "Synced" boolean NOT NULL DEFAULT false,
                "Deleted" boolean NOT NULL DEFAULT false,
                "Name" text NOT NULL DEFAULT '',
                "AccountType" integer NOT NULL DEFAULT 0,
                "BankName" text NULL,
                "AccountNumberMasked" text NULL,
                "Ifsc" text NULL,
                "CreditLimit" numeric(18,2) NULL,
                "StatementDayOfMonth" integer NULL,
                "DueDayOfMonth" integer NULL,
                "OpeningBalance" numeric(18,2) NOT NULL DEFAULT 0,
                "CurrentBalance" numeric(18,2) NOT NULL DEFAULT 0,
                "Currency" text NOT NULL DEFAULT 'INR',
                "IsActive" boolean NOT NULL DEFAULT true,
                "Notes" text NULL,
                CONSTRAINT "PK_SwalekhaAccounts" PRIMARY KEY ("Id")
            );

            ALTER TABLE "SwalekhaAccounts" ADD COLUMN IF NOT EXISTS "Name" text NOT NULL DEFAULT '';
            ALTER TABLE "SwalekhaAccounts" ADD COLUMN IF NOT EXISTS "AccountType" integer NOT NULL DEFAULT 0;
            ALTER TABLE "SwalekhaAccounts" ADD COLUMN IF NOT EXISTS "BankName" text NULL;
            ALTER TABLE "SwalekhaAccounts" ADD COLUMN IF NOT EXISTS "AccountNumberMasked" text NULL;
            ALTER TABLE "SwalekhaAccounts" ADD COLUMN IF NOT EXISTS "Ifsc" text NULL;
            ALTER TABLE "SwalekhaAccounts" ADD COLUMN IF NOT EXISTS "CreditLimit" numeric(18,2) NULL;
            ALTER TABLE "SwalekhaAccounts" ADD COLUMN IF NOT EXISTS "StatementDayOfMonth" integer NULL;
            ALTER TABLE "SwalekhaAccounts" ADD COLUMN IF NOT EXISTS "DueDayOfMonth" integer NULL;
            ALTER TABLE "SwalekhaAccounts" ADD COLUMN IF NOT EXISTS "OpeningBalance" numeric(18,2) NOT NULL DEFAULT 0;
            ALTER TABLE "SwalekhaAccounts" ADD COLUMN IF NOT EXISTS "CurrentBalance" numeric(18,2) NOT NULL DEFAULT 0;
            ALTER TABLE "SwalekhaAccounts" ADD COLUMN IF NOT EXISTS "Currency" text NOT NULL DEFAULT 'INR';
            ALTER TABLE "SwalekhaAccounts" ADD COLUMN IF NOT EXISTS "IsActive" boolean NOT NULL DEFAULT true;
            ALTER TABLE "SwalekhaAccounts" ADD COLUMN IF NOT EXISTS "Notes" text NULL;

            CREATE TABLE IF NOT EXISTS "SwalekhaAccountTransactions" (
                "Id" uuid NOT NULL,
                "CreatedAt" timestamp without time zone NOT NULL DEFAULT now(),
                "UpdatedAt" timestamp without time zone NULL,
                "Synced" boolean NOT NULL DEFAULT false,
                "Deleted" boolean NOT NULL DEFAULT false,
                "AccountId" uuid NOT NULL,
                "TransactionType" integer NOT NULL DEFAULT 0,
                "Amount" numeric(18,2) NOT NULL DEFAULT 0,
                "TransactionDate" timestamp without time zone NOT NULL DEFAULT now(),
                "Narration" text NOT NULL DEFAULT '',
                "CounterAccountId" uuid NULL,
                "RunningBalance" numeric(18,2) NOT NULL DEFAULT 0,
                "TransferGroupId" uuid NULL,
                CONSTRAINT "PK_SwalekhaAccountTransactions" PRIMARY KEY ("Id")
            );

            ALTER TABLE "SwalekhaAccountTransactions" ADD COLUMN IF NOT EXISTS "AccountId" uuid NOT NULL DEFAULT '00000000-0000-0000-0000-000000000000';
            ALTER TABLE "SwalekhaAccountTransactions" ADD COLUMN IF NOT EXISTS "TransactionType" integer NOT NULL DEFAULT 0;
            ALTER TABLE "SwalekhaAccountTransactions" ADD COLUMN IF NOT EXISTS "Amount" numeric(18,2) NOT NULL DEFAULT 0;
            ALTER TABLE "SwalekhaAccountTransactions" ADD COLUMN IF NOT EXISTS "TransactionDate" timestamp without time zone NOT NULL DEFAULT now();
            ALTER TABLE "SwalekhaAccountTransactions" ADD COLUMN IF NOT EXISTS "Narration" text NOT NULL DEFAULT '';
            ALTER TABLE "SwalekhaAccountTransactions" ADD COLUMN IF NOT EXISTS "CounterAccountId" uuid NULL;
            ALTER TABLE "SwalekhaAccountTransactions" ADD COLUMN IF NOT EXISTS "RunningBalance" numeric(18,2) NOT NULL DEFAULT 0;
            ALTER TABLE "SwalekhaAccountTransactions" ADD COLUMN IF NOT EXISTS "TransferGroupId" uuid NULL;

            CREATE INDEX IF NOT EXISTS "IX_SwalekhaAccountTransactions_AccountId_TransactionDate"
                ON "SwalekhaAccountTransactions" ("AccountId", "TransactionDate");

            CREATE TABLE IF NOT EXISTS "SwalekhaContacts" (
                "Id" uuid NOT NULL,
                "CreatedAt" timestamp without time zone NOT NULL DEFAULT now(),
                "UpdatedAt" timestamp without time zone NULL,
                "Synced" boolean NOT NULL DEFAULT false,
                "Deleted" boolean NOT NULL DEFAULT false,
                "Name" text NOT NULL DEFAULT '',
                "Phone" text NULL,
                "Email" text NULL,
                "Relationship" text NULL,
                "Balance" numeric(18,2) NOT NULL DEFAULT 0,
                "IsActive" boolean NOT NULL DEFAULT true,
                "Notes" text NULL,
                CONSTRAINT "PK_SwalekhaContacts" PRIMARY KEY ("Id")
            );

            ALTER TABLE "SwalekhaContacts" ADD COLUMN IF NOT EXISTS "Name" text NOT NULL DEFAULT '';
            ALTER TABLE "SwalekhaContacts" ADD COLUMN IF NOT EXISTS "Phone" text NULL;
            ALTER TABLE "SwalekhaContacts" ADD COLUMN IF NOT EXISTS "Email" text NULL;
            ALTER TABLE "SwalekhaContacts" ADD COLUMN IF NOT EXISTS "Relationship" text NULL;
            ALTER TABLE "SwalekhaContacts" ADD COLUMN IF NOT EXISTS "Balance" numeric(18,2) NOT NULL DEFAULT 0;
            ALTER TABLE "SwalekhaContacts" ADD COLUMN IF NOT EXISTS "IsActive" boolean NOT NULL DEFAULT true;
            ALTER TABLE "SwalekhaContacts" ADD COLUMN IF NOT EXISTS "Notes" text NULL;

            CREATE TABLE IF NOT EXISTS "SwalekhaPersonLedgerEntries" (
                "Id" uuid NOT NULL,
                "CreatedAt" timestamp without time zone NOT NULL DEFAULT now(),
                "UpdatedAt" timestamp without time zone NULL,
                "Synced" boolean NOT NULL DEFAULT false,
                "Deleted" boolean NOT NULL DEFAULT false,
                "ContactId" uuid NOT NULL,
                "EntryType" integer NOT NULL DEFAULT 0,
                "Amount" numeric(18,2) NOT NULL DEFAULT 0,
                "EntryDate" timestamp without time zone NOT NULL DEFAULT now(),
                "Narration" text NOT NULL DEFAULT '',
                "RunningBalance" numeric(18,2) NOT NULL DEFAULT 0,
                CONSTRAINT "PK_SwalekhaPersonLedgerEntries" PRIMARY KEY ("Id")
            );

            ALTER TABLE "SwalekhaPersonLedgerEntries" ADD COLUMN IF NOT EXISTS "ContactId" uuid NOT NULL DEFAULT '00000000-0000-0000-0000-000000000000';
            ALTER TABLE "SwalekhaPersonLedgerEntries" ADD COLUMN IF NOT EXISTS "EntryType" integer NOT NULL DEFAULT 0;
            ALTER TABLE "SwalekhaPersonLedgerEntries" ADD COLUMN IF NOT EXISTS "Amount" numeric(18,2) NOT NULL DEFAULT 0;
            ALTER TABLE "SwalekhaPersonLedgerEntries" ADD COLUMN IF NOT EXISTS "EntryDate" timestamp without time zone NOT NULL DEFAULT now();
            ALTER TABLE "SwalekhaPersonLedgerEntries" ADD COLUMN IF NOT EXISTS "Narration" text NOT NULL DEFAULT '';
            ALTER TABLE "SwalekhaPersonLedgerEntries" ADD COLUMN IF NOT EXISTS "RunningBalance" numeric(18,2) NOT NULL DEFAULT 0;

            CREATE INDEX IF NOT EXISTS "IX_SwalekhaPersonLedgerEntries_ContactId_EntryDate"
                ON "SwalekhaPersonLedgerEntries" ("ContactId", "EntryDate");

            CREATE TABLE IF NOT EXISTS "SwalekhaExpenseSheets" (
                "Id" uuid NOT NULL,
                "CreatedAt" timestamp without time zone NOT NULL DEFAULT now(),
                "UpdatedAt" timestamp without time zone NULL,
                "Synced" boolean NOT NULL DEFAULT false,
                "Deleted" boolean NOT NULL DEFAULT false,
                "Name" text NOT NULL DEFAULT '',
                "SheetType" text NOT NULL DEFAULT '',
                "Budget" numeric(18,2) NULL,
                "IsActive" boolean NOT NULL DEFAULT true,
                "Notes" text NULL,
                CONSTRAINT "PK_SwalekhaExpenseSheets" PRIMARY KEY ("Id")
            );

            ALTER TABLE "SwalekhaExpenseSheets" ADD COLUMN IF NOT EXISTS "Name" text NOT NULL DEFAULT '';
            ALTER TABLE "SwalekhaExpenseSheets" ADD COLUMN IF NOT EXISTS "SheetType" text NOT NULL DEFAULT '';
            ALTER TABLE "SwalekhaExpenseSheets" ADD COLUMN IF NOT EXISTS "Budget" numeric(18,2) NULL;
            ALTER TABLE "SwalekhaExpenseSheets" ADD COLUMN IF NOT EXISTS "IsActive" boolean NOT NULL DEFAULT true;
            ALTER TABLE "SwalekhaExpenseSheets" ADD COLUMN IF NOT EXISTS "Notes" text NULL;

            CREATE TABLE IF NOT EXISTS "SwalekhaExpenseEntries" (
                "Id" uuid NOT NULL,
                "CreatedAt" timestamp without time zone NOT NULL DEFAULT now(),
                "UpdatedAt" timestamp without time zone NULL,
                "Synced" boolean NOT NULL DEFAULT false,
                "Deleted" boolean NOT NULL DEFAULT false,
                "SheetId" uuid NOT NULL,
                "Category" text NOT NULL DEFAULT '',
                "Amount" numeric(18,2) NOT NULL DEFAULT 0,
                "EntryDate" timestamp without time zone NOT NULL DEFAULT now(),
                "Narration" text NOT NULL DEFAULT '',
                "IsHidden" boolean NOT NULL DEFAULT false,
                CONSTRAINT "PK_SwalekhaExpenseEntries" PRIMARY KEY ("Id")
            );

            ALTER TABLE "SwalekhaExpenseEntries" ADD COLUMN IF NOT EXISTS "SheetId" uuid NOT NULL DEFAULT '00000000-0000-0000-0000-000000000000';
            ALTER TABLE "SwalekhaExpenseEntries" ADD COLUMN IF NOT EXISTS "Category" text NOT NULL DEFAULT '';
            ALTER TABLE "SwalekhaExpenseEntries" ADD COLUMN IF NOT EXISTS "Amount" numeric(18,2) NOT NULL DEFAULT 0;
            ALTER TABLE "SwalekhaExpenseEntries" ADD COLUMN IF NOT EXISTS "EntryDate" timestamp without time zone NOT NULL DEFAULT now();
            ALTER TABLE "SwalekhaExpenseEntries" ADD COLUMN IF NOT EXISTS "Narration" text NOT NULL DEFAULT '';
            ALTER TABLE "SwalekhaExpenseEntries" ADD COLUMN IF NOT EXISTS "IsHidden" boolean NOT NULL DEFAULT false;

            CREATE INDEX IF NOT EXISTS "IX_SwalekhaExpenseEntries_SheetId_EntryDate"
                ON "SwalekhaExpenseEntries" ("SheetId", "EntryDate");

            CREATE TABLE IF NOT EXISTS "SwalekhaIncomeEntries" (
                "Id" uuid NOT NULL,
                "CreatedAt" timestamp without time zone NOT NULL DEFAULT now(),
                "UpdatedAt" timestamp without time zone NULL,
                "Synced" boolean NOT NULL DEFAULT false,
                "Deleted" boolean NOT NULL DEFAULT false,
                "Source" text NOT NULL DEFAULT '',
                "Amount" numeric(18,2) NOT NULL DEFAULT 0,
                "EntryDate" timestamp without time zone NOT NULL DEFAULT now(),
                "Narration" text NOT NULL DEFAULT '',
                CONSTRAINT "PK_SwalekhaIncomeEntries" PRIMARY KEY ("Id")
            );

            ALTER TABLE "SwalekhaIncomeEntries" ADD COLUMN IF NOT EXISTS "Source" text NOT NULL DEFAULT '';
            ALTER TABLE "SwalekhaIncomeEntries" ADD COLUMN IF NOT EXISTS "Amount" numeric(18,2) NOT NULL DEFAULT 0;
            ALTER TABLE "SwalekhaIncomeEntries" ADD COLUMN IF NOT EXISTS "EntryDate" timestamp without time zone NOT NULL DEFAULT now();
            ALTER TABLE "SwalekhaIncomeEntries" ADD COLUMN IF NOT EXISTS "Narration" text NOT NULL DEFAULT '';

            CREATE INDEX IF NOT EXISTS "IX_SwalekhaIncomeEntries_EntryDate"
                ON "SwalekhaIncomeEntries" ("EntryDate");

            CREATE TABLE IF NOT EXISTS "SwalekhaRecurringBills" (
                "Id" uuid NOT NULL,
                "CreatedAt" timestamp without time zone NOT NULL DEFAULT now(),
                "UpdatedAt" timestamp without time zone NULL,
                "Synced" boolean NOT NULL DEFAULT false,
                "Deleted" boolean NOT NULL DEFAULT false,
                "Name" text NOT NULL DEFAULT '',
                "Amount" numeric(18,2) NOT NULL DEFAULT 0,
                "DueDayOfMonth" integer NOT NULL DEFAULT 1,
                "Category" text NULL,
                "IsActive" boolean NOT NULL DEFAULT true,
                "Notes" text NULL,
                "LastPaidDate" timestamp without time zone NULL,
                CONSTRAINT "PK_SwalekhaRecurringBills" PRIMARY KEY ("Id")
            );

            ALTER TABLE "SwalekhaRecurringBills" ADD COLUMN IF NOT EXISTS "Name" text NOT NULL DEFAULT '';
            ALTER TABLE "SwalekhaRecurringBills" ADD COLUMN IF NOT EXISTS "Amount" numeric(18,2) NOT NULL DEFAULT 0;
            ALTER TABLE "SwalekhaRecurringBills" ADD COLUMN IF NOT EXISTS "DueDayOfMonth" integer NOT NULL DEFAULT 1;
            ALTER TABLE "SwalekhaRecurringBills" ADD COLUMN IF NOT EXISTS "Category" text NULL;
            ALTER TABLE "SwalekhaRecurringBills" ADD COLUMN IF NOT EXISTS "IsActive" boolean NOT NULL DEFAULT true;
            ALTER TABLE "SwalekhaRecurringBills" ADD COLUMN IF NOT EXISTS "Notes" text NULL;
            ALTER TABLE "SwalekhaRecurringBills" ADD COLUMN IF NOT EXISTS "LastPaidDate" timestamp without time zone NULL;

            CREATE TABLE IF NOT EXISTS "SwalekhaTrips" (
                "Id" uuid NOT NULL,
                "CreatedAt" timestamp without time zone NOT NULL DEFAULT now(),
                "UpdatedAt" timestamp without time zone NULL,
                "Synced" boolean NOT NULL DEFAULT false,
                "Deleted" boolean NOT NULL DEFAULT false,
                "SheetId" uuid NOT NULL,
                "Name" text NOT NULL DEFAULT '',
                "Destination" text NULL,
                "StartDate" timestamp without time zone NULL,
                "EndDate" timestamp without time zone NULL,
                "IsClosed" boolean NOT NULL DEFAULT false,
                "ClosedAt" timestamp without time zone NULL,
                "Notes" text NULL,
                CONSTRAINT "PK_SwalekhaTrips" PRIMARY KEY ("Id")
            );

            ALTER TABLE "SwalekhaTrips" ADD COLUMN IF NOT EXISTS "SheetId" uuid NOT NULL DEFAULT '00000000-0000-0000-0000-000000000000';
            ALTER TABLE "SwalekhaTrips" ADD COLUMN IF NOT EXISTS "Name" text NOT NULL DEFAULT '';
            ALTER TABLE "SwalekhaTrips" ADD COLUMN IF NOT EXISTS "Destination" text NULL;
            ALTER TABLE "SwalekhaTrips" ADD COLUMN IF NOT EXISTS "StartDate" timestamp without time zone NULL;
            ALTER TABLE "SwalekhaTrips" ADD COLUMN IF NOT EXISTS "EndDate" timestamp without time zone NULL;
            ALTER TABLE "SwalekhaTrips" ADD COLUMN IF NOT EXISTS "IsClosed" boolean NOT NULL DEFAULT false;
            ALTER TABLE "SwalekhaTrips" ADD COLUMN IF NOT EXISTS "ClosedAt" timestamp without time zone NULL;
            ALTER TABLE "SwalekhaTrips" ADD COLUMN IF NOT EXISTS "Notes" text NULL;
            """, cancellationToken);

        // PersonalFin_06 - OwnerId retrofit across every existing Swalekha table (multi-owner
        // data isolation). A single trailing block rather than interleaving into each table
        // above, since ALTER TABLE ADD COLUMN IF NOT EXISTS is order-independent - it applies
        // cleanly whether the table was just freshly created above (no OwnerId column yet) or
        // already existed from a database created before PersonalFin_06.
        await db.Database.ExecuteSqlRawAsync("""
            ALTER TABLE "SwalekhaAccounts" ADD COLUMN IF NOT EXISTS "OwnerId" uuid NOT NULL DEFAULT '00000000-0000-0000-0000-000000000000';
            ALTER TABLE "SwalekhaAccountTransactions" ADD COLUMN IF NOT EXISTS "OwnerId" uuid NOT NULL DEFAULT '00000000-0000-0000-0000-000000000000';
            ALTER TABLE "SwalekhaContacts" ADD COLUMN IF NOT EXISTS "OwnerId" uuid NOT NULL DEFAULT '00000000-0000-0000-0000-000000000000';
            ALTER TABLE "SwalekhaPersonLedgerEntries" ADD COLUMN IF NOT EXISTS "OwnerId" uuid NOT NULL DEFAULT '00000000-0000-0000-0000-000000000000';
            ALTER TABLE "SwalekhaExpenseSheets" ADD COLUMN IF NOT EXISTS "OwnerId" uuid NOT NULL DEFAULT '00000000-0000-0000-0000-000000000000';
            ALTER TABLE "SwalekhaExpenseEntries" ADD COLUMN IF NOT EXISTS "OwnerId" uuid NOT NULL DEFAULT '00000000-0000-0000-0000-000000000000';
            ALTER TABLE "SwalekhaIncomeEntries" ADD COLUMN IF NOT EXISTS "OwnerId" uuid NOT NULL DEFAULT '00000000-0000-0000-0000-000000000000';
            ALTER TABLE "SwalekhaRecurringBills" ADD COLUMN IF NOT EXISTS "OwnerId" uuid NOT NULL DEFAULT '00000000-0000-0000-0000-000000000000';
            ALTER TABLE "SwalekhaTrips" ADD COLUMN IF NOT EXISTS "OwnerId" uuid NOT NULL DEFAULT '00000000-0000-0000-0000-000000000000';

            CREATE INDEX IF NOT EXISTS "IX_SwalekhaAccounts_OwnerId" ON "SwalekhaAccounts" ("OwnerId");
            CREATE INDEX IF NOT EXISTS "IX_SwalekhaAccountTransactions_OwnerId" ON "SwalekhaAccountTransactions" ("OwnerId");
            CREATE INDEX IF NOT EXISTS "IX_SwalekhaContacts_OwnerId" ON "SwalekhaContacts" ("OwnerId");
            CREATE INDEX IF NOT EXISTS "IX_SwalekhaPersonLedgerEntries_OwnerId" ON "SwalekhaPersonLedgerEntries" ("OwnerId");
            CREATE INDEX IF NOT EXISTS "IX_SwalekhaExpenseSheets_OwnerId" ON "SwalekhaExpenseSheets" ("OwnerId");
            CREATE INDEX IF NOT EXISTS "IX_SwalekhaExpenseEntries_OwnerId" ON "SwalekhaExpenseEntries" ("OwnerId");
            CREATE INDEX IF NOT EXISTS "IX_SwalekhaIncomeEntries_OwnerId" ON "SwalekhaIncomeEntries" ("OwnerId");
            CREATE INDEX IF NOT EXISTS "IX_SwalekhaRecurringBills_OwnerId" ON "SwalekhaRecurringBills" ("OwnerId");
            CREATE INDEX IF NOT EXISTS "IX_SwalekhaTrips_OwnerId" ON "SwalekhaTrips" ("OwnerId");
            """, cancellationToken);

        // PersonalFin_07 - Owner Profile + Family Connections. Two new tables, OwnerId included
        // from day one (unlike the PersonalFin_06 retrofit above, these never existed without it).
        await db.Database.ExecuteSqlRawAsync("""
            CREATE TABLE IF NOT EXISTS "SwalekhaOwnerProfiles" (
                "Id" uuid NOT NULL,
                "CreatedAt" timestamp without time zone NOT NULL DEFAULT now(),
                "UpdatedAt" timestamp without time zone NULL,
                "Synced" boolean NOT NULL DEFAULT false,
                "Deleted" boolean NOT NULL DEFAULT false,
                "OwnerId" uuid NOT NULL DEFAULT '00000000-0000-0000-0000-000000000000',
                "FullName" text NULL,
                "Pan" text NULL,
                "Aadhar" text NULL,
                "PassportNo" text NULL,
                "Mobile" text NULL,
                "Email" text NULL,
                "AddressLine" text NULL,
                "City" text NULL,
                "State" text NULL,
                "Country" text NULL,
                "ZipCode" text NULL,
                "SpouseName" text NULL,
                "SpouseContact" text NULL,
                "LinkedAccountId" uuid NULL,
                "SourceEmployeeId" uuid NULL,
                "IsAutoProvisioned" boolean NOT NULL DEFAULT false,
                "Notes" text NULL,
                CONSTRAINT "PK_SwalekhaOwnerProfiles" PRIMARY KEY ("Id")
            );

            ALTER TABLE "SwalekhaOwnerProfiles" ADD COLUMN IF NOT EXISTS "OwnerId" uuid NOT NULL DEFAULT '00000000-0000-0000-0000-000000000000';
            ALTER TABLE "SwalekhaOwnerProfiles" ADD COLUMN IF NOT EXISTS "FullName" text NULL;
            ALTER TABLE "SwalekhaOwnerProfiles" ADD COLUMN IF NOT EXISTS "Pan" text NULL;
            ALTER TABLE "SwalekhaOwnerProfiles" ADD COLUMN IF NOT EXISTS "Aadhar" text NULL;
            ALTER TABLE "SwalekhaOwnerProfiles" ADD COLUMN IF NOT EXISTS "PassportNo" text NULL;
            ALTER TABLE "SwalekhaOwnerProfiles" ADD COLUMN IF NOT EXISTS "Mobile" text NULL;
            ALTER TABLE "SwalekhaOwnerProfiles" ADD COLUMN IF NOT EXISTS "Email" text NULL;
            ALTER TABLE "SwalekhaOwnerProfiles" ADD COLUMN IF NOT EXISTS "AddressLine" text NULL;
            ALTER TABLE "SwalekhaOwnerProfiles" ADD COLUMN IF NOT EXISTS "City" text NULL;
            ALTER TABLE "SwalekhaOwnerProfiles" ADD COLUMN IF NOT EXISTS "State" text NULL;
            ALTER TABLE "SwalekhaOwnerProfiles" ADD COLUMN IF NOT EXISTS "Country" text NULL;
            ALTER TABLE "SwalekhaOwnerProfiles" ADD COLUMN IF NOT EXISTS "ZipCode" text NULL;
            ALTER TABLE "SwalekhaOwnerProfiles" ADD COLUMN IF NOT EXISTS "SpouseName" text NULL;
            ALTER TABLE "SwalekhaOwnerProfiles" ADD COLUMN IF NOT EXISTS "SpouseContact" text NULL;
            ALTER TABLE "SwalekhaOwnerProfiles" ADD COLUMN IF NOT EXISTS "LinkedAccountId" uuid NULL;
            ALTER TABLE "SwalekhaOwnerProfiles" ADD COLUMN IF NOT EXISTS "SourceEmployeeId" uuid NULL;
            ALTER TABLE "SwalekhaOwnerProfiles" ADD COLUMN IF NOT EXISTS "IsAutoProvisioned" boolean NOT NULL DEFAULT false;
            ALTER TABLE "SwalekhaOwnerProfiles" ADD COLUMN IF NOT EXISTS "Notes" text NULL;

            CREATE INDEX IF NOT EXISTS "IX_SwalekhaOwnerProfiles_OwnerId" ON "SwalekhaOwnerProfiles" ("OwnerId");

            CREATE TABLE IF NOT EXISTS "SwalekhaFamilyMembers" (
                "Id" uuid NOT NULL,
                "CreatedAt" timestamp without time zone NOT NULL DEFAULT now(),
                "UpdatedAt" timestamp without time zone NULL,
                "Synced" boolean NOT NULL DEFAULT false,
                "Deleted" boolean NOT NULL DEFAULT false,
                "OwnerId" uuid NOT NULL DEFAULT '00000000-0000-0000-0000-000000000000',
                "Name" text NOT NULL DEFAULT '',
                "Relationship" text NULL,
                "Mobile" text NULL,
                "Email" text NULL,
                "DateOfBirth" timestamp without time zone NULL,
                "LinkedOwnerId" uuid NULL,
                "IsActive" boolean NOT NULL DEFAULT true,
                "Notes" text NULL,
                CONSTRAINT "PK_SwalekhaFamilyMembers" PRIMARY KEY ("Id")
            );

            ALTER TABLE "SwalekhaFamilyMembers" ADD COLUMN IF NOT EXISTS "OwnerId" uuid NOT NULL DEFAULT '00000000-0000-0000-0000-000000000000';
            ALTER TABLE "SwalekhaFamilyMembers" ADD COLUMN IF NOT EXISTS "Name" text NOT NULL DEFAULT '';
            ALTER TABLE "SwalekhaFamilyMembers" ADD COLUMN IF NOT EXISTS "Relationship" text NULL;
            ALTER TABLE "SwalekhaFamilyMembers" ADD COLUMN IF NOT EXISTS "Mobile" text NULL;
            ALTER TABLE "SwalekhaFamilyMembers" ADD COLUMN IF NOT EXISTS "Email" text NULL;
            ALTER TABLE "SwalekhaFamilyMembers" ADD COLUMN IF NOT EXISTS "DateOfBirth" timestamp without time zone NULL;
            ALTER TABLE "SwalekhaFamilyMembers" ADD COLUMN IF NOT EXISTS "LinkedOwnerId" uuid NULL;
            ALTER TABLE "SwalekhaFamilyMembers" ADD COLUMN IF NOT EXISTS "IsActive" boolean NOT NULL DEFAULT true;
            ALTER TABLE "SwalekhaFamilyMembers" ADD COLUMN IF NOT EXISTS "Notes" text NULL;

            CREATE INDEX IF NOT EXISTS "IX_SwalekhaFamilyMembers_OwnerId" ON "SwalekhaFamilyMembers" ("OwnerId");
            CREATE INDEX IF NOT EXISTS "IX_SwalekhaFamilyMembers_LinkedOwnerId" ON "SwalekhaFamilyMembers" ("LinkedOwnerId");
            """, cancellationToken);

        // PersonalFin_08 - Investments I (Fixed Deposits + Recurring Deposits).
        await db.Database.ExecuteSqlRawAsync("""
            CREATE TABLE IF NOT EXISTS "SwalekhaFixedDeposits" (
                "Id" uuid NOT NULL,
                "CreatedAt" timestamp without time zone NOT NULL DEFAULT now(),
                "UpdatedAt" timestamp without time zone NULL,
                "Synced" boolean NOT NULL DEFAULT false,
                "Deleted" boolean NOT NULL DEFAULT false,
                "OwnerId" uuid NOT NULL DEFAULT '00000000-0000-0000-0000-000000000000',
                "BankName" text NOT NULL DEFAULT '',
                "FdNumber" text NULL,
                "AccountId" uuid NULL,
                "PrincipalAmount" numeric(18,2) NOT NULL DEFAULT 0,
                "InterestRatePercent" numeric(18,2) NOT NULL DEFAULT 0,
                "TenureMonths" integer NOT NULL DEFAULT 0,
                "StartDate" timestamp without time zone NOT NULL DEFAULT now(),
                "MaturityDate" timestamp without time zone NOT NULL DEFAULT now(),
                "MaturityAmount" numeric(18,2) NULL,
                "AutoRenew" boolean NOT NULL DEFAULT false,
                "TdsDeducted" numeric(18,2) NULL,
                "IsClosed" boolean NOT NULL DEFAULT false,
                "ClosedAt" timestamp without time zone NULL,
                "Notes" text NULL,
                CONSTRAINT "PK_SwalekhaFixedDeposits" PRIMARY KEY ("Id")
            );

            ALTER TABLE "SwalekhaFixedDeposits" ADD COLUMN IF NOT EXISTS "OwnerId" uuid NOT NULL DEFAULT '00000000-0000-0000-0000-000000000000';
            ALTER TABLE "SwalekhaFixedDeposits" ADD COLUMN IF NOT EXISTS "BankName" text NOT NULL DEFAULT '';
            ALTER TABLE "SwalekhaFixedDeposits" ADD COLUMN IF NOT EXISTS "FdNumber" text NULL;
            ALTER TABLE "SwalekhaFixedDeposits" ADD COLUMN IF NOT EXISTS "AccountId" uuid NULL;
            ALTER TABLE "SwalekhaFixedDeposits" ADD COLUMN IF NOT EXISTS "PrincipalAmount" numeric(18,2) NOT NULL DEFAULT 0;
            ALTER TABLE "SwalekhaFixedDeposits" ADD COLUMN IF NOT EXISTS "InterestRatePercent" numeric(18,2) NOT NULL DEFAULT 0;
            ALTER TABLE "SwalekhaFixedDeposits" ADD COLUMN IF NOT EXISTS "TenureMonths" integer NOT NULL DEFAULT 0;
            ALTER TABLE "SwalekhaFixedDeposits" ADD COLUMN IF NOT EXISTS "StartDate" timestamp without time zone NOT NULL DEFAULT now();
            ALTER TABLE "SwalekhaFixedDeposits" ADD COLUMN IF NOT EXISTS "MaturityDate" timestamp without time zone NOT NULL DEFAULT now();
            ALTER TABLE "SwalekhaFixedDeposits" ADD COLUMN IF NOT EXISTS "MaturityAmount" numeric(18,2) NULL;
            ALTER TABLE "SwalekhaFixedDeposits" ADD COLUMN IF NOT EXISTS "AutoRenew" boolean NOT NULL DEFAULT false;
            ALTER TABLE "SwalekhaFixedDeposits" ADD COLUMN IF NOT EXISTS "TdsDeducted" numeric(18,2) NULL;
            ALTER TABLE "SwalekhaFixedDeposits" ADD COLUMN IF NOT EXISTS "IsClosed" boolean NOT NULL DEFAULT false;
            ALTER TABLE "SwalekhaFixedDeposits" ADD COLUMN IF NOT EXISTS "ClosedAt" timestamp without time zone NULL;
            ALTER TABLE "SwalekhaFixedDeposits" ADD COLUMN IF NOT EXISTS "Notes" text NULL;

            CREATE INDEX IF NOT EXISTS "IX_SwalekhaFixedDeposits_OwnerId" ON "SwalekhaFixedDeposits" ("OwnerId");
            CREATE INDEX IF NOT EXISTS "IX_SwalekhaFixedDeposits_MaturityDate" ON "SwalekhaFixedDeposits" ("MaturityDate");

            CREATE TABLE IF NOT EXISTS "SwalekhaRecurringDeposits" (
                "Id" uuid NOT NULL,
                "CreatedAt" timestamp without time zone NOT NULL DEFAULT now(),
                "UpdatedAt" timestamp without time zone NULL,
                "Synced" boolean NOT NULL DEFAULT false,
                "Deleted" boolean NOT NULL DEFAULT false,
                "OwnerId" uuid NOT NULL DEFAULT '00000000-0000-0000-0000-000000000000',
                "BankName" text NOT NULL DEFAULT '',
                "RdNumber" text NULL,
                "AccountId" uuid NULL,
                "MonthlyInstallment" numeric(18,2) NOT NULL DEFAULT 0,
                "InterestRatePercent" numeric(18,2) NOT NULL DEFAULT 0,
                "TenureMonths" integer NOT NULL DEFAULT 0,
                "StartDate" timestamp without time zone NOT NULL DEFAULT now(),
                "MaturityDate" timestamp without time zone NOT NULL DEFAULT now(),
                "MaturityAmount" numeric(18,2) NULL,
                "InstallmentsPaid" integer NOT NULL DEFAULT 0,
                "IsClosed" boolean NOT NULL DEFAULT false,
                "ClosedAt" timestamp without time zone NULL,
                "Notes" text NULL,
                CONSTRAINT "PK_SwalekhaRecurringDeposits" PRIMARY KEY ("Id")
            );

            ALTER TABLE "SwalekhaRecurringDeposits" ADD COLUMN IF NOT EXISTS "OwnerId" uuid NOT NULL DEFAULT '00000000-0000-0000-0000-000000000000';
            ALTER TABLE "SwalekhaRecurringDeposits" ADD COLUMN IF NOT EXISTS "BankName" text NOT NULL DEFAULT '';
            ALTER TABLE "SwalekhaRecurringDeposits" ADD COLUMN IF NOT EXISTS "RdNumber" text NULL;
            ALTER TABLE "SwalekhaRecurringDeposits" ADD COLUMN IF NOT EXISTS "AccountId" uuid NULL;
            ALTER TABLE "SwalekhaRecurringDeposits" ADD COLUMN IF NOT EXISTS "MonthlyInstallment" numeric(18,2) NOT NULL DEFAULT 0;
            ALTER TABLE "SwalekhaRecurringDeposits" ADD COLUMN IF NOT EXISTS "InterestRatePercent" numeric(18,2) NOT NULL DEFAULT 0;
            ALTER TABLE "SwalekhaRecurringDeposits" ADD COLUMN IF NOT EXISTS "TenureMonths" integer NOT NULL DEFAULT 0;
            ALTER TABLE "SwalekhaRecurringDeposits" ADD COLUMN IF NOT EXISTS "StartDate" timestamp without time zone NOT NULL DEFAULT now();
            ALTER TABLE "SwalekhaRecurringDeposits" ADD COLUMN IF NOT EXISTS "MaturityDate" timestamp without time zone NOT NULL DEFAULT now();
            ALTER TABLE "SwalekhaRecurringDeposits" ADD COLUMN IF NOT EXISTS "MaturityAmount" numeric(18,2) NULL;
            ALTER TABLE "SwalekhaRecurringDeposits" ADD COLUMN IF NOT EXISTS "InstallmentsPaid" integer NOT NULL DEFAULT 0;
            ALTER TABLE "SwalekhaRecurringDeposits" ADD COLUMN IF NOT EXISTS "IsClosed" boolean NOT NULL DEFAULT false;
            ALTER TABLE "SwalekhaRecurringDeposits" ADD COLUMN IF NOT EXISTS "ClosedAt" timestamp without time zone NULL;
            ALTER TABLE "SwalekhaRecurringDeposits" ADD COLUMN IF NOT EXISTS "Notes" text NULL;

            CREATE INDEX IF NOT EXISTS "IX_SwalekhaRecurringDeposits_OwnerId" ON "SwalekhaRecurringDeposits" ("OwnerId");
            CREATE INDEX IF NOT EXISTS "IX_SwalekhaRecurringDeposits_MaturityDate" ON "SwalekhaRecurringDeposits" ("MaturityDate");
            """, cancellationToken);

        // PersonalFin_09 - Investments II (Mutual Funds + SIP tracker).
        await db.Database.ExecuteSqlRawAsync("""
            CREATE TABLE IF NOT EXISTS "SwalekhaMutualFunds" (
                "Id" uuid NOT NULL,
                "CreatedAt" timestamp without time zone NOT NULL DEFAULT now(),
                "UpdatedAt" timestamp without time zone NULL,
                "Synced" boolean NOT NULL DEFAULT false,
                "Deleted" boolean NOT NULL DEFAULT false,
                "OwnerId" uuid NOT NULL DEFAULT '00000000-0000-0000-0000-000000000000',
                "SchemeName" text NOT NULL DEFAULT '',
                "Amc" text NULL,
                "FolioNumber" text NULL,
                "AccountId" uuid NULL,
                "InvestmentMode" integer NOT NULL DEFAULT 0,
                "SipAmount" numeric(18,2) NULL,
                "SipDayOfMonth" integer NULL,
                "LastSipInstallmentDate" timestamp without time zone NULL,
                "CurrentNav" numeric(18,2) NULL,
                "CurrentNavUpdatedAt" timestamp without time zone NULL,
                "CurrentUnits" numeric(18,2) NOT NULL DEFAULT 0,
                "TotalInvested" numeric(18,2) NOT NULL DEFAULT 0,
                "IsActive" boolean NOT NULL DEFAULT true,
                "Notes" text NULL,
                CONSTRAINT "PK_SwalekhaMutualFunds" PRIMARY KEY ("Id")
            );

            ALTER TABLE "SwalekhaMutualFunds" ADD COLUMN IF NOT EXISTS "OwnerId" uuid NOT NULL DEFAULT '00000000-0000-0000-0000-000000000000';
            ALTER TABLE "SwalekhaMutualFunds" ADD COLUMN IF NOT EXISTS "SchemeName" text NOT NULL DEFAULT '';
            ALTER TABLE "SwalekhaMutualFunds" ADD COLUMN IF NOT EXISTS "Amc" text NULL;
            ALTER TABLE "SwalekhaMutualFunds" ADD COLUMN IF NOT EXISTS "FolioNumber" text NULL;
            ALTER TABLE "SwalekhaMutualFunds" ADD COLUMN IF NOT EXISTS "AccountId" uuid NULL;
            ALTER TABLE "SwalekhaMutualFunds" ADD COLUMN IF NOT EXISTS "InvestmentMode" integer NOT NULL DEFAULT 0;
            ALTER TABLE "SwalekhaMutualFunds" ADD COLUMN IF NOT EXISTS "SipAmount" numeric(18,2) NULL;
            ALTER TABLE "SwalekhaMutualFunds" ADD COLUMN IF NOT EXISTS "SipDayOfMonth" integer NULL;
            ALTER TABLE "SwalekhaMutualFunds" ADD COLUMN IF NOT EXISTS "LastSipInstallmentDate" timestamp without time zone NULL;
            ALTER TABLE "SwalekhaMutualFunds" ADD COLUMN IF NOT EXISTS "CurrentNav" numeric(18,2) NULL;
            ALTER TABLE "SwalekhaMutualFunds" ADD COLUMN IF NOT EXISTS "CurrentNavUpdatedAt" timestamp without time zone NULL;
            ALTER TABLE "SwalekhaMutualFunds" ADD COLUMN IF NOT EXISTS "CurrentUnits" numeric(18,2) NOT NULL DEFAULT 0;
            ALTER TABLE "SwalekhaMutualFunds" ADD COLUMN IF NOT EXISTS "TotalInvested" numeric(18,2) NOT NULL DEFAULT 0;
            ALTER TABLE "SwalekhaMutualFunds" ADD COLUMN IF NOT EXISTS "IsActive" boolean NOT NULL DEFAULT true;
            ALTER TABLE "SwalekhaMutualFunds" ADD COLUMN IF NOT EXISTS "Notes" text NULL;

            CREATE INDEX IF NOT EXISTS "IX_SwalekhaMutualFunds_OwnerId" ON "SwalekhaMutualFunds" ("OwnerId");

            CREATE TABLE IF NOT EXISTS "SwalekhaMutualFundTransactions" (
                "Id" uuid NOT NULL,
                "CreatedAt" timestamp without time zone NOT NULL DEFAULT now(),
                "UpdatedAt" timestamp without time zone NULL,
                "Synced" boolean NOT NULL DEFAULT false,
                "Deleted" boolean NOT NULL DEFAULT false,
                "OwnerId" uuid NOT NULL DEFAULT '00000000-0000-0000-0000-000000000000',
                "FundId" uuid NOT NULL,
                "TransactionType" integer NOT NULL DEFAULT 0,
                "TransactionDate" timestamp without time zone NOT NULL DEFAULT now(),
                "Units" numeric(18,2) NOT NULL DEFAULT 0,
                "NavAtTransaction" numeric(18,2) NOT NULL DEFAULT 0,
                "Amount" numeric(18,2) NOT NULL DEFAULT 0,
                "Narration" text NOT NULL DEFAULT '',
                "InvestedAmountRemoved" numeric(18,2) NULL,
                CONSTRAINT "PK_SwalekhaMutualFundTransactions" PRIMARY KEY ("Id")
            );

            ALTER TABLE "SwalekhaMutualFundTransactions" ADD COLUMN IF NOT EXISTS "OwnerId" uuid NOT NULL DEFAULT '00000000-0000-0000-0000-000000000000';
            ALTER TABLE "SwalekhaMutualFundTransactions" ADD COLUMN IF NOT EXISTS "FundId" uuid NOT NULL DEFAULT '00000000-0000-0000-0000-000000000000';
            ALTER TABLE "SwalekhaMutualFundTransactions" ADD COLUMN IF NOT EXISTS "TransactionType" integer NOT NULL DEFAULT 0;
            ALTER TABLE "SwalekhaMutualFundTransactions" ADD COLUMN IF NOT EXISTS "TransactionDate" timestamp without time zone NOT NULL DEFAULT now();
            ALTER TABLE "SwalekhaMutualFundTransactions" ADD COLUMN IF NOT EXISTS "Units" numeric(18,2) NOT NULL DEFAULT 0;
            ALTER TABLE "SwalekhaMutualFundTransactions" ADD COLUMN IF NOT EXISTS "NavAtTransaction" numeric(18,2) NOT NULL DEFAULT 0;
            ALTER TABLE "SwalekhaMutualFundTransactions" ADD COLUMN IF NOT EXISTS "Amount" numeric(18,2) NOT NULL DEFAULT 0;
            ALTER TABLE "SwalekhaMutualFundTransactions" ADD COLUMN IF NOT EXISTS "Narration" text NOT NULL DEFAULT '';
            ALTER TABLE "SwalekhaMutualFundTransactions" ADD COLUMN IF NOT EXISTS "InvestedAmountRemoved" numeric(18,2) NULL;

            CREATE INDEX IF NOT EXISTS "IX_SwalekhaMutualFundTransactions_OwnerId" ON "SwalekhaMutualFundTransactions" ("OwnerId");
            CREATE INDEX IF NOT EXISTS "IX_SwalekhaMutualFundTransactions_FundId_TransactionDate"
                ON "SwalekhaMutualFundTransactions" ("FundId", "TransactionDate");
            """, cancellationToken);

        // PersonalFin_10 - Investments III (Shares/Stocks + simple PPF/EPF/NPS/Gold snapshots).
        await db.Database.ExecuteSqlRawAsync("""
            CREATE TABLE IF NOT EXISTS "SwalekhaShareHoldings" (
                "Id" uuid NOT NULL,
                "CreatedAt" timestamp without time zone NOT NULL DEFAULT now(),
                "UpdatedAt" timestamp without time zone NULL,
                "Synced" boolean NOT NULL DEFAULT false,
                "Deleted" boolean NOT NULL DEFAULT false,
                "OwnerId" uuid NOT NULL DEFAULT '00000000-0000-0000-0000-000000000000',
                "Symbol" text NOT NULL DEFAULT '',
                "CompanyName" text NULL,
                "Exchange" text NULL,
                "DematAccount" text NULL,
                "Broker" text NULL,
                "AccountId" uuid NULL,
                "CurrentPrice" numeric(18,2) NULL,
                "CurrentPriceUpdatedAt" timestamp without time zone NULL,
                "CurrentQuantity" numeric(18,2) NOT NULL DEFAULT 0,
                "TotalInvested" numeric(18,2) NOT NULL DEFAULT 0,
                "RealizedPnL" numeric(18,2) NOT NULL DEFAULT 0,
                "IsActive" boolean NOT NULL DEFAULT true,
                "Notes" text NULL,
                CONSTRAINT "PK_SwalekhaShareHoldings" PRIMARY KEY ("Id")
            );

            ALTER TABLE "SwalekhaShareHoldings" ADD COLUMN IF NOT EXISTS "OwnerId" uuid NOT NULL DEFAULT '00000000-0000-0000-0000-000000000000';
            ALTER TABLE "SwalekhaShareHoldings" ADD COLUMN IF NOT EXISTS "Symbol" text NOT NULL DEFAULT '';
            ALTER TABLE "SwalekhaShareHoldings" ADD COLUMN IF NOT EXISTS "CompanyName" text NULL;
            ALTER TABLE "SwalekhaShareHoldings" ADD COLUMN IF NOT EXISTS "Exchange" text NULL;
            ALTER TABLE "SwalekhaShareHoldings" ADD COLUMN IF NOT EXISTS "DematAccount" text NULL;
            ALTER TABLE "SwalekhaShareHoldings" ADD COLUMN IF NOT EXISTS "Broker" text NULL;
            ALTER TABLE "SwalekhaShareHoldings" ADD COLUMN IF NOT EXISTS "AccountId" uuid NULL;
            ALTER TABLE "SwalekhaShareHoldings" ADD COLUMN IF NOT EXISTS "CurrentPrice" numeric(18,2) NULL;
            ALTER TABLE "SwalekhaShareHoldings" ADD COLUMN IF NOT EXISTS "CurrentPriceUpdatedAt" timestamp without time zone NULL;
            ALTER TABLE "SwalekhaShareHoldings" ADD COLUMN IF NOT EXISTS "CurrentQuantity" numeric(18,2) NOT NULL DEFAULT 0;
            ALTER TABLE "SwalekhaShareHoldings" ADD COLUMN IF NOT EXISTS "TotalInvested" numeric(18,2) NOT NULL DEFAULT 0;
            ALTER TABLE "SwalekhaShareHoldings" ADD COLUMN IF NOT EXISTS "RealizedPnL" numeric(18,2) NOT NULL DEFAULT 0;
            ALTER TABLE "SwalekhaShareHoldings" ADD COLUMN IF NOT EXISTS "IsActive" boolean NOT NULL DEFAULT true;
            ALTER TABLE "SwalekhaShareHoldings" ADD COLUMN IF NOT EXISTS "Notes" text NULL;

            CREATE INDEX IF NOT EXISTS "IX_SwalekhaShareHoldings_OwnerId" ON "SwalekhaShareHoldings" ("OwnerId");

            CREATE TABLE IF NOT EXISTS "SwalekhaShareTransactions" (
                "Id" uuid NOT NULL,
                "CreatedAt" timestamp without time zone NOT NULL DEFAULT now(),
                "UpdatedAt" timestamp without time zone NULL,
                "Synced" boolean NOT NULL DEFAULT false,
                "Deleted" boolean NOT NULL DEFAULT false,
                "OwnerId" uuid NOT NULL DEFAULT '00000000-0000-0000-0000-000000000000',
                "HoldingId" uuid NOT NULL,
                "TransactionType" integer NOT NULL DEFAULT 0,
                "TransactionDate" timestamp without time zone NOT NULL DEFAULT now(),
                "Quantity" numeric(18,2) NOT NULL DEFAULT 0,
                "PricePerShare" numeric(18,2) NOT NULL DEFAULT 0,
                "Amount" numeric(18,2) NOT NULL DEFAULT 0,
                "Narration" text NOT NULL DEFAULT '',
                "InvestedAmountRemoved" numeric(18,2) NULL,
                "RealizedPnLOnSale" numeric(18,2) NULL,
                CONSTRAINT "PK_SwalekhaShareTransactions" PRIMARY KEY ("Id")
            );

            ALTER TABLE "SwalekhaShareTransactions" ADD COLUMN IF NOT EXISTS "OwnerId" uuid NOT NULL DEFAULT '00000000-0000-0000-0000-000000000000';
            ALTER TABLE "SwalekhaShareTransactions" ADD COLUMN IF NOT EXISTS "HoldingId" uuid NOT NULL DEFAULT '00000000-0000-0000-0000-000000000000';
            ALTER TABLE "SwalekhaShareTransactions" ADD COLUMN IF NOT EXISTS "TransactionType" integer NOT NULL DEFAULT 0;
            ALTER TABLE "SwalekhaShareTransactions" ADD COLUMN IF NOT EXISTS "TransactionDate" timestamp without time zone NOT NULL DEFAULT now();
            ALTER TABLE "SwalekhaShareTransactions" ADD COLUMN IF NOT EXISTS "Quantity" numeric(18,2) NOT NULL DEFAULT 0;
            ALTER TABLE "SwalekhaShareTransactions" ADD COLUMN IF NOT EXISTS "PricePerShare" numeric(18,2) NOT NULL DEFAULT 0;
            ALTER TABLE "SwalekhaShareTransactions" ADD COLUMN IF NOT EXISTS "Amount" numeric(18,2) NOT NULL DEFAULT 0;
            ALTER TABLE "SwalekhaShareTransactions" ADD COLUMN IF NOT EXISTS "Narration" text NOT NULL DEFAULT '';
            ALTER TABLE "SwalekhaShareTransactions" ADD COLUMN IF NOT EXISTS "InvestedAmountRemoved" numeric(18,2) NULL;
            ALTER TABLE "SwalekhaShareTransactions" ADD COLUMN IF NOT EXISTS "RealizedPnLOnSale" numeric(18,2) NULL;

            CREATE INDEX IF NOT EXISTS "IX_SwalekhaShareTransactions_OwnerId" ON "SwalekhaShareTransactions" ("OwnerId");
            CREATE INDEX IF NOT EXISTS "IX_SwalekhaShareTransactions_HoldingId_TransactionDate"
                ON "SwalekhaShareTransactions" ("HoldingId", "TransactionDate");

            CREATE TABLE IF NOT EXISTS "SwalekhaOtherAssets" (
                "Id" uuid NOT NULL,
                "CreatedAt" timestamp without time zone NOT NULL DEFAULT now(),
                "UpdatedAt" timestamp without time zone NULL,
                "Synced" boolean NOT NULL DEFAULT false,
                "Deleted" boolean NOT NULL DEFAULT false,
                "OwnerId" uuid NOT NULL DEFAULT '00000000-0000-0000-0000-000000000000',
                "AssetType" integer NOT NULL DEFAULT 0,
                "Name" text NOT NULL DEFAULT '',
                "CurrentValue" numeric(18,2) NOT NULL DEFAULT 0,
                "AsOfDate" timestamp without time zone NOT NULL DEFAULT now(),
                "IsActive" boolean NOT NULL DEFAULT true,
                "Notes" text NULL,
                CONSTRAINT "PK_SwalekhaOtherAssets" PRIMARY KEY ("Id")
            );

            ALTER TABLE "SwalekhaOtherAssets" ADD COLUMN IF NOT EXISTS "OwnerId" uuid NOT NULL DEFAULT '00000000-0000-0000-0000-000000000000';
            ALTER TABLE "SwalekhaOtherAssets" ADD COLUMN IF NOT EXISTS "AssetType" integer NOT NULL DEFAULT 0;
            ALTER TABLE "SwalekhaOtherAssets" ADD COLUMN IF NOT EXISTS "Name" text NOT NULL DEFAULT '';
            ALTER TABLE "SwalekhaOtherAssets" ADD COLUMN IF NOT EXISTS "CurrentValue" numeric(18,2) NOT NULL DEFAULT 0;
            ALTER TABLE "SwalekhaOtherAssets" ADD COLUMN IF NOT EXISTS "AsOfDate" timestamp without time zone NOT NULL DEFAULT now();
            ALTER TABLE "SwalekhaOtherAssets" ADD COLUMN IF NOT EXISTS "IsActive" boolean NOT NULL DEFAULT true;
            ALTER TABLE "SwalekhaOtherAssets" ADD COLUMN IF NOT EXISTS "Notes" text NULL;

            CREATE INDEX IF NOT EXISTS "IX_SwalekhaOtherAssets_OwnerId" ON "SwalekhaOtherAssets" ("OwnerId");
            """, cancellationToken);

        logger.LogInformation("Swalekha account storage repair check completed.");
    }
}
