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
using Microsoft.EntityFrameworkCore.ChangeTracking;
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
    public DbSet<StockOperationDocument> StockOperationDocuments => Set<StockOperationDocument>();
    public DbSet<StockOperationItem> StockOperationItems => Set<StockOperationItem>();
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
    public DbSet<PurchaseReturnItcReversal> PurchaseReturnItcReversals => Set<PurchaseReturnItcReversal>();
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
    public DbSet<TailoringServiceItem> TailoringServiceItems => Set<TailoringServiceItem>();
    public DbSet<TailoringOrder> TailoringOrders => Set<TailoringOrder>();
    public DbSet<TailoringOrderLine> TailoringOrderLines => Set<TailoringOrderLine>();
    public DbSet<TailoringCustomerReceipt> TailoringCustomerReceipts => Set<TailoringCustomerReceipt>();
    public DbSet<TailoringVendorPayment> TailoringVendorPayments => Set<TailoringVendorPayment>();
    public DbSet<TailoringOrderHistory> TailoringOrderHistories => Set<TailoringOrderHistory>();

    public DbSet<Bank> Banks => Set<Bank>();
    public DbSet<BankAccount> BankAccounts => Set<BankAccount>();
    public DbSet<BankAccountDetail> BankAccountDetails => Set<BankAccountDetail>();
    public DbSet<VendorBankAccount> VendorBankAccounts => Set<VendorBankAccount>();
    public DbSet<BankTransaction> BankTransactions => Set<BankTransaction>();
    public DbSet<ChequeLog> ChequeLogs => Set<ChequeLog>();
    public DbSet<BankCashTranscation> BankCashTranscations => Set<BankCashTranscation>();
    public DbSet<BankStatementLine> BankStatementLines => Set<BankStatementLine>();
    public DbSet<FinancialYearLock> FinancialYearLocks => Set<FinancialYearLock>();
    public DbSet<LedgerGroup> LedgerGroups => Set<LedgerGroup>();
    public DbSet<Ledger> Ledgers => Set<Ledger>();
    public DbSet<JournalEntry> JournalEntries => Set<JournalEntry>();
    public DbSet<JournalLine> JournalLines => Set<JournalLine>();
    public DbSet<Voucher> Vouchers => Set<Voucher>();
    public DbSet<CashVoucher> CashVouchers => Set<CashVoucher>();
    public DbSet<CashVoucherConversion> CashVoucherConversions => Set<CashVoucherConversion>();
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
        modelBuilder.Entity<CashVoucherConversion>().ToTable("CashVoucherConversions");

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
        modelBuilder.Entity<Stock>().Property(stock => stock.CostPrice).HasPrecision(18, 4);
        modelBuilder.Entity<StockMovement>().Property(movement => movement.CostPrice).HasPrecision(18, 4);
        modelBuilder.Entity<StockMovement>().Property(movement => movement.AverageCostBefore).HasPrecision(18, 4);
        modelBuilder.Entity<StockMovement>().Property(movement => movement.AverageCostAfter).HasPrecision(18, 4);
        modelBuilder.Entity<StockOperationDocument>().HasIndex(document => new { document.CompanyId, document.StoreId, document.DocumentNumber }).IsUnique(false);
        modelBuilder.Entity<StockOperationDocument>().HasIndex(document => new { document.CompanyId, document.OperationType, document.OnDate });
        modelBuilder.Entity<StockOperationDocument>().HasIndex(document => new { document.CompanyId, document.JournalEntryId });
        modelBuilder.Entity<StockOperationItem>().HasIndex(item => new { item.CompanyId, item.StockOperationDocumentId });
        modelBuilder.Entity<StockOperationItem>().HasIndex(item => new { item.CompanyId, item.ProductId, item.StockId });
        modelBuilder.Entity<Stock>().HasIndex(stock => new { stock.CompanyId, stock.StoreId, stock.IsOFB });
        modelBuilder.Entity<NonGstGoodsDocument>().HasIndex(document => new { document.CompanyId, document.StoreId, document.DocumentType, document.OnDate });
        modelBuilder.Entity<NonGstGoodsDocument>().HasIndex(document => new { document.CompanyId, document.DocumentNumber }).IsUnique(false);
        modelBuilder.Entity<NonGstGoodsItem>().HasIndex(item => new { item.CompanyId, item.DocumentId });
        modelBuilder.Entity<DocumentSequence>()
            .HasIndex(sequence => new { sequence.CompanyId, sequence.StoreGroupId, sequence.StoreId, sequence.DocumentType, sequence.SequenceDate })
            .HasDatabaseName("IX_DocumentSequences_Company_Store_Type_Date")
            .IsUnique()
            .AreNullsDistinct(false)
            .HasFilter("\"Deleted\" = false");
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
        modelBuilder.Entity<PurchaseReturnItcReversal>().HasIndex(item => new { item.CompanyId, item.PurchaseReturnId });
        modelBuilder.Entity<PurchaseReturnItcReversal>().HasIndex(item => new { item.CompanyId, item.PurchaseInvoiceId, item.PurchaseInvoiceItemId });
        modelBuilder.Entity<PurchaseReturnItcReversal>().HasIndex(item => new { item.CompanyId, item.JournalEntryId });
        modelBuilder.Entity<VendorSettlement>().HasIndex(item => new { item.CompanyId, item.StoreId, item.SettlementNumber }).IsUnique(false);
        modelBuilder.Entity<VendorSettlement>().HasIndex(item => new { item.CompanyId, item.VendorId, item.OnDate });
        modelBuilder.Entity<VendorSettlement>().HasIndex(item => new { item.CompanyId, item.PurchaseReturnId });
        modelBuilder.Entity<VendorSettlementAllocation>().HasIndex(item => new { item.CompanyId, item.VendorSettlementId });
        modelBuilder.Entity<VendorSettlementAllocation>().HasIndex(item => new { item.CompanyId, item.PurchaseInvoiceId });
        modelBuilder.Entity<PurchasePayment>().HasIndex(payment => new { payment.CompanyId, payment.StoreId, payment.PurchaseInvoiceId, payment.OnDate });
        modelBuilder.Entity<PurchasePayment>().HasIndex(payment => new { payment.CompanyId, payment.VendorId, payment.OnDate });
        modelBuilder.Entity<TailoringServiceItem>().HasIndex(item => new { item.CompanyId, item.StoreId, item.ServiceCode });
        modelBuilder.Entity<TailoringServiceItem>().HasIndex(item => new { item.CompanyId, item.StoreId, item.Category, item.Active });
        modelBuilder.Entity<TailoringOrder>().HasIndex(order => new { order.CompanyId, order.StoreId, order.OrderNumber }).IsUnique(false);
        modelBuilder.Entity<TailoringOrder>().HasIndex(order => new { order.CompanyId, order.StoreId, order.Status, order.ExpectedDeliveryDate });
        modelBuilder.Entity<TailoringOrder>().HasIndex(order => new { order.CompanyId, order.CustomerId, order.OnDate });
        modelBuilder.Entity<TailoringOrder>().HasIndex(order => new { order.CompanyId, order.VendorId, order.Status });
        modelBuilder.Entity<TailoringOrderLine>().HasIndex(line => new { line.CompanyId, line.TailoringOrderId });
        modelBuilder.Entity<TailoringCustomerReceipt>().HasIndex(receipt => new { receipt.CompanyId, receipt.TailoringOrderId, receipt.OnDate });
        modelBuilder.Entity<TailoringVendorPayment>().HasIndex(payment => new { payment.CompanyId, payment.TailoringOrderId, payment.OnDate });
        modelBuilder.Entity<TailoringOrderHistory>().HasIndex(history => new { history.CompanyId, history.TailoringOrderId, history.EventDate });
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
        modelBuilder.Entity<CashVoucherConversion>().HasIndex(item => new { item.CompanyId, item.StoreId, item.ConvertedAt });
        modelBuilder.Entity<CashVoucherConversion>().HasIndex(item => new { item.CashVoucherId, item.VoucherId });
        modelBuilder.Entity<JournalEntry>().HasIndex(entry => new { entry.CompanyId, entry.StoreId, entry.EntryNumber }).IsUnique(false);
        modelBuilder.Entity<JournalLine>().HasIndex(line => new { line.CompanyId, line.LedgerId, line.JournalEntryId });
        modelBuilder.Entity<FinancialYearLock>().HasIndex(period => new { period.CompanyId, period.FinancialYear, period.PeriodStart, period.PeriodEnd });
        modelBuilder.Entity<FinancialYearLock>().HasIndex(period => new { period.CompanyId, period.StoreGroupId, period.StoreId, period.Active });
        modelBuilder.Entity<BankTransaction>().HasIndex(transaction => new { transaction.CompanyId, transaction.BankAccountId, transaction.OnDate });
        modelBuilder.Entity<BankTransaction>().HasIndex(transaction => new { transaction.CompanyId, transaction.BankAccountId, transaction.Reconciled });
        modelBuilder.Entity<BankStatementLine>().HasIndex(line => new { line.CompanyId, line.BankAccountId, line.OnDate });
        modelBuilder.Entity<BankStatementLine>().HasIndex(line => new { line.CompanyId, line.BankAccountId, line.Reconciled });
        modelBuilder.Entity<ChequeLog>().HasIndex(cheque => new { cheque.CompanyId, cheque.BankAccountId, cheque.ChequeNumber });
        modelBuilder.Entity<ChequeLog>().HasIndex(cheque => new { cheque.CompanyId, cheque.Status, cheque.OnDate });
        modelBuilder.Entity<Employee>().HasIndex(employee => new { employee.CompanyId, employee.StoreId, employee.Mobile });
    }

    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        PrepareEntitiesForSave();
        await ValidateFinancialYearLocksAsync(cancellationToken);
        ValidateChangedJournalLines();
        return await base.SaveChangesAsync(cancellationToken);
    }

    public override int SaveChanges()
    {
        PrepareEntitiesForSave();
        ValidateFinancialYearLocks();
        ValidateChangedJournalLines();
        return base.SaveChanges();
    }



    private sealed record PeriodLockCandidate(
        Guid CompanyId,
        Guid? StoreGroupId,
        Guid? StoreId,
        DateTime OnDate,
        string Domain,
        string EntityName);

    private async Task ValidateFinancialYearLocksAsync(CancellationToken cancellationToken)
    {
        var candidates = BuildPeriodLockCandidates().ToList();
        if (candidates.Count == 0)
        {
            return;
        }

        var companyIds = candidates.Select(item => item.CompanyId).Distinct().ToList();
        var minDate = candidates.Min(item => item.OnDate.Date);
        var maxDate = candidates.Max(item => item.OnDate.Date);
        var locks = await FinancialYearLocks.AsNoTracking()
            .Where(item => item.Active
                && companyIds.Contains(item.CompanyId)
                && item.PeriodStart <= maxDate
                && item.PeriodEnd >= minDate)
            .ToListAsync(cancellationToken);

        ThrowIfAnyPeriodLocked(candidates, locks);
    }

    private void ValidateFinancialYearLocks()
    {
        var candidates = BuildPeriodLockCandidates().ToList();
        if (candidates.Count == 0)
        {
            return;
        }

        var companyIds = candidates.Select(item => item.CompanyId).Distinct().ToList();
        var minDate = candidates.Min(item => item.OnDate.Date);
        var maxDate = candidates.Max(item => item.OnDate.Date);
        var locks = FinancialYearLocks.AsNoTracking()
            .Where(item => item.Active
                && companyIds.Contains(item.CompanyId)
                && item.PeriodStart <= maxDate
                && item.PeriodEnd >= minDate)
            .ToList();

        ThrowIfAnyPeriodLocked(candidates, locks);
    }

    private IEnumerable<PeriodLockCandidate> BuildPeriodLockCandidates()
    {
        foreach (var entry in ChangeTracker.Entries()
            .Where(entry => entry.State is EntityState.Added or EntityState.Modified or EntityState.Deleted))
        {
            if (entry.Entity is FinancialYearLock)
            {
                continue;
            }

            var domain = ClassifyFinancialPeriodDomain(entry.Metadata.ClrType.Name);
            if (domain is null)
            {
                continue;
            }

            var companyId = ReadGuidProperty(entry, nameof(CompanyBase.CompanyId));
            var onDate = ReadDateProperty(entry, "OnDate", "TransactionDate", "FiledAt", "LockedAt");
            if (!companyId.HasValue || !onDate.HasValue)
            {
                continue;
            }

            yield return new PeriodLockCandidate(
                companyId.Value,
                ReadGuidProperty(entry, nameof(StoreBase.StoreGroupId)),
                ReadGuidProperty(entry, nameof(StoreBase.StoreId)),
                onDate.Value.Date,
                domain,
                entry.Metadata.ClrType.Name);
        }
    }

    private static Guid? ReadGuidProperty(EntityEntry entry, string propertyName)
    {
        if (entry.Metadata.FindProperty(propertyName) is null)
        {
            return null;
        }

        var value = entry.State == EntityState.Deleted
            ? entry.Property(propertyName).OriginalValue
            : entry.Property(propertyName).CurrentValue;
        return value is Guid guid && guid != Guid.Empty ? guid : null;
    }

    private static DateTime? ReadDateProperty(EntityEntry entry, params string[] propertyNames)
    {
        foreach (var propertyName in propertyNames)
        {
            if (entry.Metadata.FindProperty(propertyName) is null)
            {
                continue;
            }

            var value = entry.State == EntityState.Deleted
                ? entry.Property(propertyName).OriginalValue
                : entry.Property(propertyName).CurrentValue;
            if (value is DateTime dateTime)
            {
                return dateTime.Date;
            }
        }

        return null;
    }

    private static string? ClassifyFinancialPeriodDomain(string entityName)
    {
        if (entityName is "Invoice" or "InvoiceItem" or "InvoicePayment" or "CardPayment" or "CustomerAdvanceReceipt" or "LoyaltyProgram" or "LoyaltyPointLedger" or "TailoringOrder" or "TailoringOrderLine" or "TailoringCustomerReceipt")
        {
            return "Sales";
        }

        if (entityName.StartsWith("Purchase", StringComparison.Ordinal) || entityName is "VendorSettlement" or "VendorSettlementAllocation" or "VendorPayment" or "TailoringVendorPayment")
        {
            return "Purchase";
        }

        if (entityName is "Stock" or "StockMovement" or "StockOperationDocument" or "StockOperationItem" or "NonGstGoodsDocument" or "NonGstGoodsItem" or "Product" or "ProductDetail")
        {
            return "Inventory";
        }

        if (entityName is "GstReturnDraft" or "GstReturnAuditEntry")
        {
            return "GST";
        }

        if (entityName is "JournalEntry" or "JournalLine" or "Voucher" or "CashVoucher" or "CashVoucherConversion" or "BankTransaction" or "BankStatementLine" or "ChequeLog" or "CommercialNote" or "PettyCashSheet" or "BankCashTranscation")
        {
            return "Accounting";
        }

        return null;
    }

    private static void ThrowIfAnyPeriodLocked(IReadOnlyList<PeriodLockCandidate> candidates, IReadOnlyList<FinancialYearLock> locks)
    {
        foreach (var candidate in candidates)
        {
            var periodLock = locks.FirstOrDefault(item => PeriodLockApplies(item, candidate));
            if (periodLock is null)
            {
                continue;
            }

            throw new InvalidOperationException(
                $"Financial year/period '{periodLock.FinancialYear}' is locked for {candidate.Domain}. " +
                $"{candidate.EntityName} dated {candidate.OnDate:yyyy-MM-dd} cannot be saved in a locked period. " +
                "Unlock the period or post the correction in an open period.");
        }
    }

    private static bool PeriodLockApplies(FinancialYearLock periodLock, PeriodLockCandidate candidate)
    {
        if (periodLock.CompanyId != candidate.CompanyId)
        {
            return false;
        }

        if (periodLock.StoreGroupId.HasValue && periodLock.StoreGroupId.Value != candidate.StoreGroupId)
        {
            return false;
        }

        if (periodLock.StoreId.HasValue && periodLock.StoreId.Value != candidate.StoreId)
        {
            return false;
        }

        var candidateDate = candidate.OnDate.Date;
        if (periodLock.PeriodStart.Date > candidateDate || periodLock.PeriodEnd.Date < candidateDate)
        {
            return false;
        }

        return candidate.Domain switch
        {
            "Sales" => periodLock.LockSales,
            "Purchase" => periodLock.LockPurchase,
            "Inventory" => periodLock.LockInventory,
            "GST" => periodLock.LockGst,
            _ => periodLock.LockAccounting
        };
    }



    private void ValidateChangedJournalLines()
    {
        foreach (var entry in ChangeTracker.Entries<JournalLine>()
            .Where(entry => entry.State is EntityState.Added or EntityState.Modified))
        {
            var line = entry.Entity;
            if (line.Debit < 0 || line.Credit < 0)
            {
                throw new InvalidOperationException("Journal line debit and credit amounts cannot be negative.");
            }

            if (line.Debit > 0 && line.Credit > 0)
            {
                throw new InvalidOperationException("A journal line cannot contain both debit and credit amounts.");
            }

            if (line.Debit == 0 && line.Credit == 0)
            {
                throw new InvalidOperationException("A journal line must contain either a debit or credit amount.");
            }
        }
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
