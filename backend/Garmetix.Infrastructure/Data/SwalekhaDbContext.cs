using System.Linq.Expressions;
using Garmetix.Core.Models.Base;
using Garmetix.Core.Models.Swalekha;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Garmetix.Infrastructure.Data;

/// <summary>
/// A deliberately separate database/DbContext from GarmetixDbContext, backing the isolated
/// Swalekha (Personal &amp; Personal Finance) module. Data here belongs to a single Owner user,
/// not the multi-tenant Company/StoreGroup/Store hierarchy the rest of the platform uses -
/// entities added in later Swalekha stages should extend SwalekhaOwnedEntity, not
/// CompanyBase/GroupBase/StoreBase. Same shared ASP.NET Core API process as GarmetixDbContext,
/// just a different Postgres database (see ConnectionStrings:Swalekha).
///
/// PersonalFin_06: more than one Owner login can use Swalekha (e.g. two owners of the same
/// company), each with their own private data. Every SwalekhaOwnedEntity row is scoped to
/// exactly one Owner via a global query filter (so no endpoint can forget it and leak data
/// across owners) plus an auto-stamp on insert - both driven by the scoped SwalekhaOwnerContext
/// (populated per-request by SwalekhaOwnerMiddleware from the JWT's NameIdentifier claim).
/// </summary>
public sealed class SwalekhaDbContext(DbContextOptions<SwalekhaDbContext> options, SwalekhaOwnerContext? ownerContext = null) : DbContext(options)
{
    private static readonly ValueConverter<DateTime, DateTime> DateTimeKindConverter = new(
        value => NormalizeDateTime(value),
        value => NormalizeDateTime(value));

    private static readonly ValueConverter<DateTime?, DateTime?> NullableDateTimeKindConverter = new(
        value => NormalizeDateTime(value),
        value => NormalizeDateTime(value));

    /// <summary>
    /// The current request's Owner (Guid.Empty outside a real request - e.g. design-time
    /// tooling, where ownerContext is null - which matches no real row since OwnerId is never
    /// stamped as Guid.Empty for an actual insert).
    /// </summary>
    public Guid CurrentOwnerId => ownerContext?.OwnerId ?? Guid.Empty;

    public DbSet<SwalekhaAccount> SwalekhaAccounts => Set<SwalekhaAccount>();
    public DbSet<SwalekhaAccountTransaction> SwalekhaAccountTransactions => Set<SwalekhaAccountTransaction>();
    public DbSet<SwalekhaContact> SwalekhaContacts => Set<SwalekhaContact>();
    public DbSet<SwalekhaPersonLedgerEntry> SwalekhaPersonLedgerEntries => Set<SwalekhaPersonLedgerEntry>();
    public DbSet<SwalekhaExpenseSheet> SwalekhaExpenseSheets => Set<SwalekhaExpenseSheet>();
    public DbSet<SwalekhaExpenseEntry> SwalekhaExpenseEntries => Set<SwalekhaExpenseEntry>();
    public DbSet<SwalekhaIncomeEntry> SwalekhaIncomeEntries => Set<SwalekhaIncomeEntry>();
    public DbSet<SwalekhaRecurringBill> SwalekhaRecurringBills => Set<SwalekhaRecurringBill>();
    public DbSet<SwalekhaTrip> SwalekhaTrips => Set<SwalekhaTrip>();
    public DbSet<SwalekhaOwnerProfile> SwalekhaOwnerProfiles => Set<SwalekhaOwnerProfile>();
    public DbSet<SwalekhaFamilyMember> SwalekhaFamilyMembers => Set<SwalekhaFamilyMember>();
    public DbSet<SwalekhaFixedDeposit> SwalekhaFixedDeposits => Set<SwalekhaFixedDeposit>();
    public DbSet<SwalekhaRecurringDeposit> SwalekhaRecurringDeposits => Set<SwalekhaRecurringDeposit>();

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
        // every decimal is (18,2), and every DateTime is normalized to Kind=Unspecified before
        // hitting a Postgres "timestamp without time zone" column (Npgsql throws on Kind=Utc
        // against that type). The soft-delete filter is additionally combined with an OwnerId
        // filter for every SwalekhaOwnedEntity (PersonalFin_06) - see CreateOwnedFilter.
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            var clrType = entityType.ClrType;

            if (entityType.BaseType is null && typeof(SwalekhaOwnedEntity).IsAssignableFrom(clrType))
            {
                entityType.SetQueryFilter(CreateOwnedFilter(clrType));
            }
            else if (entityType.BaseType is null && typeof(BaseEntity).IsAssignableFrom(clrType))
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

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        StampOwnerId();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        StampOwnerId();
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    /// <summary>
    /// Every newly-inserted SwalekhaOwnedEntity gets OwnerId = the current request's Owner,
    /// unless the caller already set a different OwnerId explicitly - that one deliberate
    /// exception is the family-transfer sync (PersonalFin_07), which posts a row directly into
    /// the *recipient* owner's data on their behalf. Never overwrites an already-set OwnerId.
    /// </summary>
    private void StampOwnerId()
    {
        foreach (var entry in ChangeTracker.Entries<SwalekhaOwnedEntity>())
        {
            if (entry.State == EntityState.Added && entry.Entity.OwnerId == Guid.Empty)
            {
                entry.Entity.OwnerId = CurrentOwnerId;
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

    /// <summary>
    /// Builds `entity => !entity.Deleted &amp;&amp; entity.OwnerId == this.CurrentOwnerId` for the
    /// given SwalekhaOwnedEntity-derived type - the same generic per-entity-type expression-tree
    /// technique CreateSoftDeleteFilter already uses, extended to reference this DbContext
    /// instance's CurrentOwnerId (the standard EF Core "current tenant" global-filter pattern).
    /// </summary>
    private LambdaExpression CreateOwnedFilter(Type entityType)
    {
        var parameter = Expression.Parameter(entityType, "entity");

        var deletedProperty = Expression.Property(parameter, nameof(BaseEntity.Deleted));
        var notDeleted = Expression.Equal(deletedProperty, Expression.Constant(false));

        var ownerIdProperty = Expression.Property(parameter, nameof(SwalekhaOwnedEntity.OwnerId));
        var contextConstant = Expression.Constant(this);
        var currentOwnerIdProperty = Expression.Property(contextConstant, nameof(CurrentOwnerId));
        var ownerMatches = Expression.Equal(ownerIdProperty, currentOwnerIdProperty);

        var combined = Expression.AndAlso(notDeleted, ownerMatches);
        return Expression.Lambda(combined, parameter);
    }
}
