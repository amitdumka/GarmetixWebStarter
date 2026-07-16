using System.Linq.Expressions;
using Garmetix.Core.Models.Base;
using Garmetix.Core.Models.Swalekha;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Garmetix.Infrastructure.Data;

/// <summary>
/// A deliberately separate database/DbContext from GarmetixDbContext, backing the isolated
/// Swalekha (Personal &amp; Personal Finance) module. Data here belongs to a single Owner user,
/// not the multi-tenant Company/StoreGroup/Store hierarchy the rest of the platform uses -
/// entities added in later Swalekha stages should extend plain BaseEntity, not
/// CompanyBase/GroupBase/StoreBase. Same shared ASP.NET Core API process as GarmetixDbContext,
/// just a different Postgres database (see ConnectionStrings:Swalekha).
/// </summary>
public sealed class SwalekhaDbContext(DbContextOptions<SwalekhaDbContext> options) : DbContext(options)
{
    private static readonly ValueConverter<DateTime, DateTime> DateTimeKindConverter = new(
        value => NormalizeDateTime(value),
        value => NormalizeDateTime(value));

    private static readonly ValueConverter<DateTime?, DateTime?> NullableDateTimeKindConverter = new(
        value => NormalizeDateTime(value),
        value => NormalizeDateTime(value));

    public DbSet<SwalekhaAccount> SwalekhaAccounts => Set<SwalekhaAccount>();
    public DbSet<SwalekhaAccountTransaction> SwalekhaAccountTransactions => Set<SwalekhaAccountTransaction>();
    public DbSet<SwalekhaContact> SwalekhaContacts => Set<SwalekhaContact>();
    public DbSet<SwalekhaPersonLedgerEntry> SwalekhaPersonLedgerEntries => Set<SwalekhaPersonLedgerEntry>();
    public DbSet<SwalekhaExpenseSheet> SwalekhaExpenseSheets => Set<SwalekhaExpenseSheet>();
    public DbSet<SwalekhaExpenseEntry> SwalekhaExpenseEntries => Set<SwalekhaExpenseEntry>();
    public DbSet<SwalekhaIncomeEntry> SwalekhaIncomeEntries => Set<SwalekhaIncomeEntry>();
    public DbSet<SwalekhaRecurringBill> SwalekhaRecurringBills => Set<SwalekhaRecurringBill>();
    public DbSet<SwalekhaTrip> SwalekhaTrips => Set<SwalekhaTrip>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<SwalekhaAccountTransaction>()
            .HasIndex(transaction => new { transaction.AccountId, transaction.TransactionDate });
        modelBuilder.Entity<SwalekhaPersonLedgerEntry>()
            .HasIndex(entry => new { entry.ContactId, entry.EntryDate });
        modelBuilder.Entity<SwalekhaExpenseEntry>()
            .HasIndex(entry => new { entry.SheetId, entry.EntryDate });
        modelBuilder.Entity<SwalekhaIncomeEntry>()
            .HasIndex(entry => entry.EntryDate);

        // Same three global conventions GarmetixDbContext applies, reused here so Swalekha gets
        // the same soft-delete/decimal/DateTime correctness for free instead of re-deriving it:
        // every BaseEntity-derived type is auto-filtered on !Deleted, every decimal is (18,2),
        // and every DateTime is normalized to Kind=Unspecified before hitting a Postgres
        // "timestamp without time zone" column (Npgsql throws on Kind=Utc against that type).
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            var clrType = entityType.ClrType;

            if (entityType.BaseType is null && typeof(BaseEntity).IsAssignableFrom(clrType))
            {
                entityType.SetQueryFilter(CreateSoftDeleteFilter(clrType));
            }

            foreach (var property in entityType.GetProperties().Where(property => property.ClrType == typeof(decimal) || property.ClrType == typeof(decimal?)))
            {
                property.SetPrecision(18);
                property.SetScale(2);
            }

            foreach (var property in entityType.GetProperties().Where(property => property.ClrType == typeof(DateTime) || property.ClrType == typeof(DateTime?)))
            {
                property.SetColumnType("timestamp without time zone");
                property.SetValueConverter(property.ClrType == typeof(DateTime)
                    ? DateTimeKindConverter
                    : NullableDateTimeKindConverter);
            }
        }
    }

    private static DateTime NormalizeDateTime(DateTime value) => DateTime.SpecifyKind(value, DateTimeKind.Unspecified);

    private static DateTime? NormalizeDateTime(DateTime? value) => value.HasValue ? NormalizeDateTime(value.Value) : null;

    private static LambdaExpression CreateSoftDeleteFilter(Type entityType)
    {
        var parameter = Expression.Parameter(entityType, "entity");
        var property = Expression.Property(parameter, nameof(BaseEntity.Deleted));
        var compare = Expression.Equal(property, Expression.Constant(false));
        return Expression.Lambda(compare, parameter);
    }
}
