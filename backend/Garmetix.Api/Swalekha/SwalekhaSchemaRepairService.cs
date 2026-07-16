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
            """, cancellationToken);

        logger.LogInformation("Swalekha account storage repair check completed.");
    }
}
