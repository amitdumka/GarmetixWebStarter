using System.Linq.Expressions;
using Garmetix.Core.Models.Accounting;
using Garmetix.Core.Models.Authentication;
using Garmetix.Core.Models.Base;
using Garmetix.Core.Models.HRM;
using Garmetix.Core.Models.Inventory;
using Garmetix.Core.Models.Stores;
using Garmetix.Models.DayOperations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Garmetix.Infrastructure.Data;

public sealed class GarmetixDbContext(DbContextOptions<GarmetixDbContext> options) : DbContext(options)
{
    private static readonly ValueConverter<DateTime, DateTime> DateTimeKindConverter = new(
        value => NormalizeDateTime(value),
        value => NormalizeDateTime(value));

    private static readonly ValueConverter<DateTime?, DateTime?> NullableDateTimeKindConverter = new(
        value => NormalizeDateTime(value),
        value => NormalizeDateTime(value));

    public DbSet<Company> Companies => Set<Company>();
    public DbSet<StoreGroup> StoreGroups => Set<StoreGroup>();
    public DbSet<Store> Stores => Set<Store>();
    public DbSet<AppUser> Users => Set<AppUser>();

    public DbSet<Product> Products => Set<Product>();
    public DbSet<Stock> Stocks => Set<Stock>();
    public DbSet<Brand> Brands => Set<Brand>();
    public DbSet<ProductCategory> ProductCategories => Set<ProductCategory>();
    public DbSet<ProductSubCategory> ProductSubCategories => Set<ProductSubCategory>();
    public DbSet<Tax> Taxes => Set<Tax>();
    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<Vendor> Vendors => Set<Vendor>();
    public DbSet<Invoice> SalesInvoices => Set<Invoice>();
    public DbSet<PurchaseInvoice> PurchaseInvoices => Set<PurchaseInvoice>();
    public DbSet<InvoiceItem> InvoiceItems => Set<InvoiceItem>();
    public DbSet<PurchaseInvoiceItem> PurchaseInvoiceItems => Set<PurchaseInvoiceItem>();
    public DbSet<InvoicePayment> InvoicePayments => Set<InvoicePayment>();

    public DbSet<Bank> Banks => Set<Bank>();
    public DbSet<BankAccount> BankAccounts => Set<BankAccount>();
    public DbSet<BankAccountDetail> BankAccountDetails => Set<BankAccountDetail>();
    public DbSet<VendorBankAccount> VendorBankAccounts => Set<VendorBankAccount>();
    public DbSet<BankTransaction> BankTransactions => Set<BankTransaction>();
    public DbSet<ChequeLog> ChequeLogs => Set<ChequeLog>();
    public DbSet<BankCashTranscation> BankCashTranscations => Set<BankCashTranscation>();
    public DbSet<BankStatementLine> BankStatementLines => Set<BankStatementLine>();
    public DbSet<LedgerGroup> LedgerGroups => Set<LedgerGroup>();
    public DbSet<Ledger> Ledgers => Set<Ledger>();
    public DbSet<JournalEntry> JournalEntries => Set<JournalEntry>();
    public DbSet<JournalLine> JournalLines => Set<JournalLine>();
    public DbSet<Voucher> Vouchers => Set<Voucher>();
    public DbSet<CashVoucher> CashVouchers => Set<CashVoucher>();
    public DbSet<Transaction> Transactions => Set<Transaction>();
    public DbSet<Party> Parties => Set<Party>();
    public DbSet<PettyCashSheet> PettyCashSheets => Set<PettyCashSheet>();
    public DbSet<DayBegin> DayBegins => Set<DayBegin>();
    public DbSet<DayEnd> DayEnds => Set<DayEnd>();

    public DbSet<Employee> Employees => Set<Employee>();
    public DbSet<EmployeeDetail> EmployeeDetails => Set<EmployeeDetail>();
    public DbSet<Attendance> Attendance => Set<Attendance>();
    public DbSet<MonthlyAttendance> MonthlyAttendance => Set<MonthlyAttendance>();
    public DbSet<SalaryStructure> SalaryStructures => Set<SalaryStructure>();
    public DbSet<SalaryPaySlip> SalaryPaySlips => Set<SalaryPaySlip>();
    public DbSet<SalaryPayment> SalaryPayments => Set<SalaryPayment>();
    public DbSet<TimeSheet> TimeSheets => Set<TimeSheet>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<AppUser>().ToTable("Users");
        modelBuilder.Entity<AppUser>().HasKey(user => user.Id);
        modelBuilder.Entity<AppUser>().HasIndex(user => user.UserName).IsUnique();
        modelBuilder.Entity<AppUser>().HasIndex(user => user.Email).IsUnique(false);
        modelBuilder.Entity<VoucherBase>().UseTpcMappingStrategy();
        modelBuilder.Entity<Voucher>().ToTable("Vouchers");
        modelBuilder.Entity<CashVoucher>().ToTable("CashVouchers");

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

        modelBuilder.Entity<Company>().HasIndex(company => company.Name);
        modelBuilder.Entity<Store>().HasIndex(store => new { store.CompanyId, store.StoreGroupId, store.StoreCode }).IsUnique();
        modelBuilder.Entity<Product>().HasIndex(product => product.Barcode);
        modelBuilder.Entity<Invoice>().HasIndex(invoice => new { invoice.CompanyId, invoice.StoreId, invoice.InvoiceNumber }).IsUnique(false);
        modelBuilder.Entity<PurchaseInvoice>().HasIndex(invoice => new { invoice.CompanyId, invoice.VendorId, invoice.InvoiceNumber }).IsUnique(false);
        modelBuilder.Entity<Voucher>().HasIndex(voucher => new { voucher.CompanyId, voucher.StoreId, voucher.VoucherNumber }).IsUnique(false);
        modelBuilder.Entity<CashVoucher>().HasIndex(voucher => new { voucher.CompanyId, voucher.StoreId, voucher.VoucherNumber }).IsUnique(false);
        modelBuilder.Entity<JournalEntry>().HasIndex(entry => new { entry.CompanyId, entry.StoreId, entry.EntryNumber }).IsUnique(false);
        modelBuilder.Entity<JournalLine>().HasIndex(line => new { line.CompanyId, line.LedgerId, line.JournalEntryId });
        modelBuilder.Entity<BankTransaction>().HasIndex(transaction => new { transaction.CompanyId, transaction.BankAccountId, transaction.OnDate });
        modelBuilder.Entity<BankStatementLine>().HasIndex(line => new { line.CompanyId, line.BankAccountId, line.OnDate });
        modelBuilder.Entity<ChequeLog>().HasIndex(cheque => new { cheque.CompanyId, cheque.BankAccountId, cheque.ChequeNumber });
        modelBuilder.Entity<Employee>().HasIndex(employee => new { employee.CompanyId, employee.StoreId, employee.Mobile });
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        PrepareEntitiesForSave();
        return base.SaveChangesAsync(cancellationToken);
    }

    public override int SaveChanges()
    {
        PrepareEntitiesForSave();
        return base.SaveChanges();
    }

    private void PrepareEntitiesForSave()
    {
        StampAuditableEntities();
        NormalizeDateTimeKinds();
    }

    private void StampAuditableEntities()
    {
        var now = DateTime.SpecifyKind(DateTime.Now, DateTimeKind.Unspecified);

        foreach (var entry in ChangeTracker.Entries<BaseEntity>())
        {
            if (entry.State == EntityState.Added)
            {
                entry.Entity.CreatedAt = now;
                entry.Entity.UpdatedAt = now;
            }

            if (entry.State == EntityState.Modified)
            {
                entry.Entity.UpdatedAt = now;
            }
        }
    }

    private void NormalizeDateTimeKinds()
    {
        foreach (var entry in ChangeTracker.Entries().Where(entry => entry.State is EntityState.Added or EntityState.Modified))
        {
            foreach (var property in entry.Properties)
            {
                var clrType = property.Metadata.ClrType;
                if (clrType == typeof(DateTime) && property.CurrentValue is DateTime dateTime)
                {
                    property.CurrentValue = NormalizeDateTime(dateTime);
                }

                if (clrType == typeof(DateTime?) && property.CurrentValue is DateTime nullableDateTime)
                {
                    property.CurrentValue = NormalizeDateTime(nullableDateTime);
                }
            }
        }
    }

    private static DateTime NormalizeDateTime(DateTime value)
    {
        return DateTime.SpecifyKind(value, DateTimeKind.Unspecified);
    }

    private static DateTime? NormalizeDateTime(DateTime? value)
    {
        return value.HasValue ? NormalizeDateTime(value.Value) : null;
    }

    private static LambdaExpression CreateSoftDeleteFilter(Type entityType)
    {
        var parameter = Expression.Parameter(entityType, "entity");
        var property = Expression.Property(parameter, nameof(BaseEntity.Deleted));
        var compare = Expression.Equal(property, Expression.Constant(false));
        return Expression.Lambda(compare, parameter);
    }
}
