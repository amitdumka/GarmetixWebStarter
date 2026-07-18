using Garmetix.Core.Models.Configuration;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Garmetix.Infrastructure.Data;

/// <summary>
/// A deliberately separate database from GarmetixDbContext (garmetix_config_db, own connection
/// string) - a secure central store for environment-variable configuration and client settings.
/// Same isolation rationale as SwalekhaDbContext: this data shouldn't live in the shared
/// business database, and a failure here must never block the shared API's own startup.
/// </summary>
public sealed class GarmetixConfigDbContext(DbContextOptions<GarmetixConfigDbContext> options) : DbContext(options)
{
    private static readonly ValueConverter<DateTime, DateTime> DateTimeKindConverter = new(
        value => DateTime.SpecifyKind(value, DateTimeKind.Unspecified),
        value => DateTime.SpecifyKind(value, DateTimeKind.Utc));

    private static readonly ValueConverter<DateTime?, DateTime?> NullableDateTimeKindConverter = new(
        value => value.HasValue ? DateTime.SpecifyKind(value.Value, DateTimeKind.Unspecified) : value,
        value => value.HasValue ? DateTime.SpecifyKind(value.Value, DateTimeKind.Utc) : value);

    public DbSet<ConfigEntry> ConfigEntries => Set<ConfigEntry>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<ConfigEntry>(entity =>
        {
            entity.HasIndex(e => e.Key).IsUnique();
            entity.HasQueryFilter(e => !e.Deleted);
        });

        // Same convention GarmetixDbContext/SwalekhaDbContext apply: every DateTime is normalized
        // to Kind=Unspecified before hitting a Postgres "timestamp without time zone" column
        // (Npgsql throws on Kind=Utc against that type, and the column type must be told
        // explicitly - EF's own default for DateTime is "timestamp with time zone" otherwise,
        // which mismatches the raw CREATE TABLE SQL in ConfigSchemaRepairService).
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            foreach (var property in entityType.GetProperties())
            {
                if (property.ClrType == typeof(decimal) || property.ClrType == typeof(decimal?))
                {
                    property.SetPrecision(18);
                    property.SetScale(2);
                }

                if (property.ClrType == typeof(DateTime) || property.ClrType == typeof(DateTime?))
                {
                    property.SetColumnType("timestamp without time zone");
                    property.SetValueConverter(property.ClrType == typeof(DateTime)
                        ? DateTimeKindConverter
                        : NullableDateTimeKindConverter);
                }
            }
        }
    }
}
