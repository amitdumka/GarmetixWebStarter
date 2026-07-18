using Garmetix.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Garmetix.Api.Configuration;

public static class ConfigSchemaRepairService
{
    public static async Task RepairConfigStorageAsync(GarmetixConfigDbContext db, ILogger logger, CancellationToken cancellationToken = default)
    {
        await db.Database.ExecuteSqlRawAsync("""
            CREATE TABLE IF NOT EXISTS "ConfigEntries" (
                "Id" uuid NOT NULL,
                "CreatedAt" timestamp without time zone NOT NULL DEFAULT now(),
                "UpdatedAt" timestamp without time zone NULL,
                "Synced" boolean NOT NULL DEFAULT false,
                "Deleted" boolean NOT NULL DEFAULT false,
                "Key" text NOT NULL DEFAULT '',
                "Category" text NOT NULL DEFAULT '',
                "DisplayName" text NULL,
                "Description" text NULL,
                "Scope" integer NOT NULL DEFAULT 0,
                "IsSecret" boolean NOT NULL DEFAULT false,
                "PlainValue" text NULL,
                "EncryptedValue" text NULL,
                "EnvVarName" text NULL,
                "WriteToApiEnv" boolean NOT NULL DEFAULT false,
                "LastAppliedAt" timestamp without time zone NULL,
                "UpdatedByUserName" text NULL,
                CONSTRAINT "PK_ConfigEntries" PRIMARY KEY ("Id")
            );

            ALTER TABLE "ConfigEntries" ADD COLUMN IF NOT EXISTS "Key" text NOT NULL DEFAULT '';
            ALTER TABLE "ConfigEntries" ADD COLUMN IF NOT EXISTS "Category" text NOT NULL DEFAULT '';
            ALTER TABLE "ConfigEntries" ADD COLUMN IF NOT EXISTS "DisplayName" text NULL;
            ALTER TABLE "ConfigEntries" ADD COLUMN IF NOT EXISTS "Description" text NULL;
            ALTER TABLE "ConfigEntries" ADD COLUMN IF NOT EXISTS "Scope" integer NOT NULL DEFAULT 0;
            ALTER TABLE "ConfigEntries" ADD COLUMN IF NOT EXISTS "IsSecret" boolean NOT NULL DEFAULT false;
            ALTER TABLE "ConfigEntries" ADD COLUMN IF NOT EXISTS "PlainValue" text NULL;
            ALTER TABLE "ConfigEntries" ADD COLUMN IF NOT EXISTS "EncryptedValue" text NULL;
            ALTER TABLE "ConfigEntries" ADD COLUMN IF NOT EXISTS "EnvVarName" text NULL;
            ALTER TABLE "ConfigEntries" ADD COLUMN IF NOT EXISTS "WriteToApiEnv" boolean NOT NULL DEFAULT false;
            ALTER TABLE "ConfigEntries" ADD COLUMN IF NOT EXISTS "LastAppliedAt" timestamp without time zone NULL;
            ALTER TABLE "ConfigEntries" ADD COLUMN IF NOT EXISTS "UpdatedByUserName" text NULL;

            CREATE UNIQUE INDEX IF NOT EXISTS "IX_ConfigEntries_Key" ON "ConfigEntries" ("Key");
            CREATE INDEX IF NOT EXISTS "IX_ConfigEntries_Category" ON "ConfigEntries" ("Category");
            CREATE INDEX IF NOT EXISTS "IX_ConfigEntries_Scope" ON "ConfigEntries" ("Scope");
            """, cancellationToken);

        logger.LogInformation("Configuration storage repair check completed.");
    }

    /// <summary>
    /// Seeds/refreshes catalog metadata (category, display name, description, secret flag, env
    /// var mapping) for every known key - never touches an existing row's stored Value, so this
    /// is safe to run on every startup even after values have been customized. New keys added to
    /// ConfigCatalog in later stages get picked up automatically without a migration.
    /// </summary>
    public static async Task EnsureCatalogEntriesAsync(GarmetixConfigDbContext db, CancellationToken cancellationToken = default)
    {
        var existingKeys = db.ConfigEntries.Select(e => e.Key).ToHashSet();
        var now = DateTime.UtcNow;

        foreach (var definition in ConfigCatalog.Definitions)
        {
            if (existingKeys.Contains(definition.Key))
            {
                continue;
            }

            db.ConfigEntries.Add(new Garmetix.Core.Models.Configuration.ConfigEntry
            {
                Key = definition.Key,
                Category = definition.Category,
                DisplayName = definition.DisplayName,
                Description = definition.Description,
                Scope = Garmetix.Core.Models.Configuration.ConfigScope.Configuration,
                IsSecret = definition.IsSecret,
                EnvVarName = definition.EnvVarName,
                WriteToApiEnv = definition.WriteToApiEnv,
                CreatedAt = now
            });
        }

        await db.SaveChangesAsync(cancellationToken);
    }
}
