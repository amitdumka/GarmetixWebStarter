using System.Linq.Expressions;
using Garmetix.Core.Models.Accounting;
using Garmetix.Core.Models.Authentication;
using Garmetix.Core.Models.Base;
using Garmetix.Core.Models.HRM;
using Garmetix.Core.Models.GstReturns;
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
    public DbSet<PasswordResetToken> PasswordResetTokens => Set<PasswordResetToken>();
    public DbSet<GstReturnDraft> GstReturnDrafts => Set<GstReturnDraft>();
    public DbSet<GstReturnAuditEntry> GstReturnAuditEntries => Set<GstReturnAuditEntry>();

    public DbSet<Product> Products => Set<Product>();
    public DbSet<Stock> Stocks => Set<Stock>();
    public DbSet<StockMovement> StockMovements => Set<StockMovement>();
    public DbSet<NonGstGoodsDocument> NonGstGoodsDocuments => Set<NonGstGoodsDocument>();
    public DbSet<NonGstGoodsItem> NonGstGoodsItems => Set<NonGstGoodsItem>();
    public DbSet<DocumentSequence> DocumentSequences => Set<DocumentSequence>();
    public DbSet<ProductDetail> ProductDetails => Set<ProductDetail>();
    public DbSet<Brand> Brands => Set<Brand>();
    public DbSet<ProductCategory> ProductCategories => Set<ProductCategory>();
    public DbSet<ProductSubCategory> ProductSubCategories => Set<ProductSubCategory>();
    public DbSet<ProductAttribute> ProductAttributes => Set<ProductAttribute>();
    public DbSet<ProductAttributeValue> ProductAttributeValues => Set<ProductAttributeValue>();
    public DbSet<ProductTag> ProductTags => Set<ProductTag>();
    public DbSet<ProductTagMapping> ProductTagMappings => Set<ProductTagMapping>();
    public DbSet<Tax> Taxes => Set<Tax>();
    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<Vendor> Vendors => Set<Vendor>();
    public DbSet<Salesman> Salesmen => Set<Salesman>();
    public DbSet<Invoice> SalesInvoices => Set<Invoice>();
    public DbSet<PurchaseInvoice> PurchaseInvoices => Set<PurchaseInvoice>();
    public DbSet<PurchaseReturn> PurchaseReturns => Set<PurchaseReturn>();
    public DbSet<PurchaseReturnItem> PurchaseReturnItems => Set<PurchaseReturnItem>();
    public DbSet<VendorSettlement> VendorSettlements => Set<VendorSettlement>();
    public DbSet<VendorSettlementAllocation> VendorSettlementAllocations => Set<VendorSettlementAllocation>();
    public DbSet<InvoiceItem> InvoiceItems => Set<InvoiceItem>();
    public DbSet<PurchaseInvoiceItem> PurchaseInvoiceItems => Set<PurchaseInvoiceItem>();
    public DbSet<InvoicePayment> InvoicePayments => Set<InvoicePayment>();
    public DbSet<CardPayment> CardPayments => Set<CardPayment>();
    public DbSet<VendorPayment> VendorPayments => Set<VendorPayment>();
    public DbSet<PurchasePayment> PurchasePayments => Set<PurchasePayment>();
    public DbSet<CommercialNote> CommercialNotes => Set<CommercialNote>();
    public DbSet<CustomerAdvanceReceipt> CustomerAdvanceReceipts => Set<CustomerAdvanceReceipt>();
    public DbSet<LoyaltyProgram> LoyaltyPrograms => Set<LoyaltyProgram>();
    public DbSet<LoyaltyPointLedger> LoyaltyPointLedgers => Set<LoyaltyPointLedger>();

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
        modelBuilder.Entity<PasswordResetToken>().ToTable("PasswordResetTokens");
        modelBuilder.Entity<PasswordResetToken>().HasKey(token => token.Id);
        modelBuilder.Entity<PasswordResetToken>().HasIndex(token => token.TokenHash).IsUnique();
        modelBuilder.Entity<PasswordResetToken>().HasIndex(token => new { token.UserId, token.ExpiresAtUtc });
        modelBuilder.Entity<PasswordResetToken>().HasOne<AppUser>().WithMany().HasForeignKey(token => token.UserId).OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<GstReturnDraft>().ToTable("GstReturnDrafts");
        modelBuilder.Entity<GstReturnDraft>().HasIndex(draft => new { draft.CompanyId, draft.Form, draft.ReturnPeriod, draft.Gstin });
        modelBuilder.Entity<GstReturnDraft>().HasIndex(draft => new { draft.CompanyId, draft.Status, draft.UpdatedAt });
        modelBuilder.Entity<GstReturnAuditEntry>().ToTable("GstReturnAuditEntries");
        modelBuilder.Entity<GstReturnAuditEntry>().HasIndex(entry => new { entry.CompanyId, entry.DraftId, entry.CreatedAt });
        modelBuilder.Entity<GstReturnAuditEntry>().HasIndex(entry => new { entry.CompanyId, entry.Form, entry.ReturnPeriod });
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
        modelBuilder.Entity<Product>().HasIndex(product => new { product.CompanyId, product.Barcode }).IsUnique(false);
        modelBuilder.Entity<Product>().HasIndex(product => new { product.CompanyId, product.ProductGroup, product.ProductType });
        modelBuilder.Entity<ProductCategory>().HasIndex(category => new { category.CompanyId, category.ProductGroup, category.Name }).IsUnique(false);
        modelBuilder.Entity<ProductSubCategory>().HasIndex(category => new { category.CompanyId, category.CategoryId, category.Name }).IsUnique(false);
        modelBuilder.Entity<ProductDetail>().HasIndex(detail => new { detail.CompanyId, detail.ProductId, detail.Barcode }).IsUnique(false);
        modelBuilder.Entity<Brand>().HasIndex(brand => brand.BrandCode).IsUnique(false);
        modelBuilder.Entity<ProductAttribute>().HasKey(attribute => attribute.Id);
        modelBuilder.Entity<ProductAttributeValue>().HasKey(value => new { value.ProductId, value.AttributeId });
        modelBuilder.Entity<ProductTag>().HasKey(tag => tag.Id);
        modelBuilder.Entity<ProductTagMapping>().HasKey(mapping => new { mapping.ProductId, mapping.TagId });
        modelBuilder.Entity<StockMovement>().HasIndex(movement => new { movement.CompanyId, movement.StoreId, movement.ProductId, movement.OnDate });
        modelBuilder.Entity<StockMovement>().HasIndex(movement => new { movement.CompanyId, movement.SourceType, movement.SourceId });
        modelBuilder.Entity<Stock>().HasIndex(stock => new { stock.CompanyId, stock.StoreId, stock.IsOFB });
        modelBuilder.Entity<NonGstGoodsDocument>().HasIndex(document => new { document.CompanyId, document.StoreId, document.DocumentType, document.OnDate });
        modelBuilder.Entity<NonGstGoodsDocument>().HasIndex(document => new { document.CompanyId, document.DocumentNumber }).IsUnique(false);
        modelBuilder.Entity<NonGstGoodsItem>().HasIndex(item => new { item.CompanyId, item.DocumentId });
        modelBuilder.Entity<DocumentSequence>().HasIndex(sequence => new { sequence.CompanyId, sequence.StoreGroupId, sequence.StoreId, sequence.DocumentType, sequence.SequenceDate }).IsUnique(false);
        modelBuilder.Entity<Customer>().HasIndex(customer => new { customer.CompanyId, customer.GSTIN }).IsUnique(false);
        modelBuilder.Entity<Vendor>().HasIndex(vendor => new { vendor.CompanyId, vendor.GSTIN }).IsUnique(false);
        modelBuilder.Entity<Salesman>().HasIndex(salesman => new { salesman.CompanyId, salesman.StoreId, salesman.Name }).IsUnique(false);
        modelBuilder.Entity<Invoice>().HasIndex(invoice => new { invoice.CompanyId, invoice.StoreId, invoice.InvoiceNumber }).IsUnique(false);
        modelBuilder.Entity<PurchaseInvoice>().HasIndex(invoice => new { invoice.CompanyId, invoice.VendorId, invoice.InvoiceNumber }).IsUnique(false);
        modelBuilder.Entity<PurchaseInvoice>().HasIndex(invoice => new { invoice.CompanyId, invoice.StoreId, invoice.InwardNumber }).IsUnique(false);
        modelBuilder.Entity<PurchaseReturn>().HasIndex(item => new { item.CompanyId, item.StoreId, item.ReturnNumber }).IsUnique(false);
        modelBuilder.Entity<PurchaseReturn>().HasIndex(item => new { item.CompanyId, item.PurchaseInvoiceId, item.OnDate });
        modelBuilder.Entity<PurchaseReturnItem>().HasIndex(item => new { item.CompanyId, item.PurchaseReturnId });
        modelBuilder.Entity<PurchaseReturnItem>().HasIndex(item => new { item.CompanyId, item.PurchaseInvoiceId, item.PurchaseInvoiceItemId });
        modelBuilder.Entity<VendorSettlement>().HasIndex(item => new { item.CompanyId, item.StoreId, item.SettlementNumber }).IsUnique(false);
        modelBuilder.Entity<VendorSettlement>().HasIndex(item => new { item.CompanyId, item.VendorId, item.OnDate });
        modelBuilder.Entity<VendorSettlement>().HasIndex(item => new { item.CompanyId, item.PurchaseReturnId });
        modelBuilder.Entity<VendorSettlementAllocation>().HasIndex(item => new { item.CompanyId, item.VendorSettlementId });
        modelBuilder.Entity<VendorSettlementAllocation>().HasIndex(item => new { item.CompanyId, item.PurchaseInvoiceId });
        modelBuilder.Entity<PurchasePayment>().HasIndex(payment => new { payment.CompanyId, payment.StoreId, payment.PurchaseInvoiceId, payment.OnDate });
        modelBuilder.Entity<PurchasePayment>().HasIndex(payment => new { payment.CompanyId, payment.VendorId, payment.OnDate });
        modelBuilder.Entity<InvoicePayment>().HasIndex(payment => new { payment.CompanyId, payment.StoreId, payment.InvoiceId, payment.OnDate });
        modelBuilder.Entity<CardPayment>().HasIndex(payment => new { payment.CompanyId, payment.StoreId, payment.InvoiceId, payment.OnDate });
        modelBuilder.Entity<VendorPayment>().HasIndex(payment => new { payment.CompanyId, payment.VendorId, payment.OnDate });
        modelBuilder.Entity<CommercialNote>().HasIndex(note => new { note.CompanyId, note.StoreId, note.NoteNumber }).IsUnique(false);
        modelBuilder.Entity<CommercialNote>().HasIndex(note => new { note.CompanyId, note.PartyType, note.PartyName });
        modelBuilder.Entity<CustomerAdvanceReceipt>().HasIndex(receipt => new { receipt.CompanyId, receipt.StoreId, receipt.ReceiptNumber }).IsUnique(false);
        modelBuilder.Entity<CustomerAdvanceReceipt>().HasIndex(receipt => new { receipt.CompanyId, receipt.CustomerId, receipt.OnDate });
        modelBuilder.Entity<LoyaltyProgram>().HasIndex(program => new { program.CompanyId, program.StoreId });
        modelBuilder.Entity<LoyaltyPointLedger>().HasIndex(entry => new { entry.CompanyId, entry.CustomerId, entry.OnDate });
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
