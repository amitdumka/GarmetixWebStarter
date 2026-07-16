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
            """, cancellationToken);

        logger.LogInformation("Swalekha account storage repair check completed.");
    }
}
