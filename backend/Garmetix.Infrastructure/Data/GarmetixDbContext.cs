using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Linq.Expressions;
using System.Text.Json;
using Garmetix.Core.Models.Accounting;
using Garmetix.Core.Models.Audit;
using Garmetix.Core.Models.Authentication;
using Garmetix.Core.Models.Attendance;
using Garmetix.Core.Models.Base;
using Garmetix.Core.Models.FinalAccounts;
using Garmetix.Core.Models.HRM;
using Garmetix.Core.Models.GstReturns;
using Garmetix.Core.Models.GstTax;
using Garmetix.Core.Models.Inventory;
using Garmetix.Core.Models.Marketing;
using Garmetix.Core.Models.Printing;
using Garmetix.Core.Models.Stores;
using Garmetix.Models.DayOperations;
using Garmetix.Infrastructure.Audit;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Garmetix.Infrastructure.Data;

public sealed class GarmetixDbContext(DbContextOptions<GarmetixDbContext> options, AuditActorContext? auditActorContext = null) : DbContext(options)
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
    public DbSet<GstApiProvider> GstApiProviders => Set<GstApiProvider>();
    public DbSet<GstApiProviderFeature> GstApiProviderFeatures => Set<GstApiProviderFeature>();
    public DbSet<GstApiProviderCredential> GstApiProviderCredentials => Set<GstApiProviderCredential>();
    public DbSet<GstApiCallLog> GstApiCallLogs => Set<GstApiCallLog>();
    public DbSet<GstinVerificationCache> GstinVerificationCaches => Set<GstinVerificationCache>();
    public DbSet<GstHsnMaster> GstHsnMasters => Set<GstHsnMaster>();
    public DbSet<GstTaxRateRule> GstTaxRateRules => Set<GstTaxRateRule>();
    public DbSet<GstStateCode> GstStateCodes => Set<GstStateCode>();
    public DbSet<GstUqcCode> GstUqcCodes => Set<GstUqcCode>();
    public DbSet<GstAuditRule> GstAuditRules => Set<GstAuditRule>();
    public DbSet<GstAuditFinding> GstAuditFindings => Set<GstAuditFinding>();
    public DbSet<GstEinvoiceIrnRecord> GstEinvoiceIrnRecords => Set<GstEinvoiceIrnRecord>();
    public DbSet<GstEwaybillRecord> GstEwaybillRecords => Set<GstEwaybillRecord>();
    public DbSet<AuditLogEntry> AuditLogEntries => Set<AuditLogEntry>();
    public DbSet<DigitalInvoice> DigitalInvoices => Set<DigitalInvoice>();
    public DbSet<DigitalInvoiceEvent> DigitalInvoiceEvents => Set<DigitalInvoiceEvent>();
    public DbSet<StoreReviewSetting> StoreReviewSettings => Set<StoreReviewSetting>();
    public DbSet<CustomerFeedback> CustomerFeedback => Set<CustomerFeedback>();
    public DbSet<WhatsAppProviderSetting> WhatsAppProviderSettings => Set<WhatsAppProviderSetting>();
    public DbSet<WhatsAppMessageLog> WhatsAppMessageLogs => Set<WhatsAppMessageLog>();
    public DbSet<InvoiceAdBanner> InvoiceAdBanners => Set<InvoiceAdBanner>();
    public DbSet<DigitalBillCampaign> DigitalBillCampaigns => Set<DigitalBillCampaign>();
    public DbSet<DigitalBillCampaignRecipient> DigitalBillCampaignRecipients => Set<DigitalBillCampaignRecipient>();

    public DbSet<DotMatrixPrintSetting> DotMatrixPrintSettings => Set<DotMatrixPrintSetting>();
    public DbSet<DotMatrixPrintQueueEntry> DotMatrixPrintQueueEntries => Set<DotMatrixPrintQueueEntry>();

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
    public DbSet<PosHeldBill> PosHeldBills => Set<PosHeldBill>();
    public DbSet<Invoice> SalesInvoices => Set<Invoice>();
    public DbSet<PurchaseInvoice> PurchaseInvoices => Set<PurchaseInvoice>();
    public DbSet<PurchaseInvoiceImportBatch> PurchaseInvoiceImportBatches => Set<PurchaseInvoiceImportBatch>();
    public DbSet<PurchaseInvoiceImportLine> PurchaseInvoiceImportLines => Set<PurchaseInvoiceImportLine>();
    public DbSet<PurchaseInvoiceImportFile> PurchaseInvoiceImportFiles => Set<PurchaseInvoiceImportFile>();
    public DbSet<PurchaseInvoiceImportVendorProfile> PurchaseInvoiceImportVendorProfiles => Set<PurchaseInvoiceImportVendorProfile>();
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
    public DbSet<TailoringVendorServiceRate> TailoringVendorServiceRates => Set<TailoringVendorServiceRate>();
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
    public DbSet<CashDetail> CashDetails => Set<CashDetail>();
    public DbSet<FinalAccountsModuleSettings> FinalAccountsModuleSettings => Set<FinalAccountsModuleSettings>();
    public DbSet<FinalAccountsAccountGroup> FinalAccountsAccountGroups => Set<FinalAccountsAccountGroup>();
    public DbSet<FinalAccountsAccount> FinalAccountsAccounts => Set<FinalAccountsAccount>();
    public DbSet<FinalAccountsAccountMapping> FinalAccountsAccountMappings => Set<FinalAccountsAccountMapping>();
    public DbSet<FinalAccountsFiscalYear> FinalAccountsFiscalYears => Set<FinalAccountsFiscalYear>();
    public DbSet<FinalAccountsFiscalPeriod> FinalAccountsFiscalPeriods => Set<FinalAccountsFiscalPeriod>();
    public DbSet<FinalAccountsPostingRule> FinalAccountsPostingRules => Set<FinalAccountsPostingRule>();
    public DbSet<FinalAccountsPostingRuleLine> FinalAccountsPostingRuleLines => Set<FinalAccountsPostingRuleLine>();
    public DbSet<FinalAccountsJournalEntry> FinalAccountsJournalEntries => Set<FinalAccountsJournalEntry>();
    public DbSet<FinalAccountsJournalLine> FinalAccountsJournalLines => Set<FinalAccountsJournalLine>();
    public DbSet<FinalAccountsSourcePostingLink> FinalAccountsSourcePostingLinks => Set<FinalAccountsSourcePostingLink>();
    public DbSet<FinalAccountsSyncJob> FinalAccountsSyncJobs => Set<FinalAccountsSyncJob>();
    public DbSet<FinalAccountsSyncJobItem> FinalAccountsSyncJobItems => Set<FinalAccountsSyncJobItem>();
    public DbSet<FinalAccountsSyncCheckpoint> FinalAccountsSyncCheckpoints => Set<FinalAccountsSyncCheckpoint>();
    public DbSet<FinalAccountsSyncException> FinalAccountsSyncExceptions => Set<FinalAccountsSyncException>();
    public DbSet<FinalAccountsAdjustmentBatch> FinalAccountsAdjustmentBatches => Set<FinalAccountsAdjustmentBatch>();
    public DbSet<FinalAccountsAdjustmentLine> FinalAccountsAdjustmentLines => Set<FinalAccountsAdjustmentLine>();
    public DbSet<FinalAccountsAdjustmentAttachment> FinalAccountsAdjustmentAttachments => Set<FinalAccountsAdjustmentAttachment>();
    public DbSet<FinalAccountsAdjustmentComment> FinalAccountsAdjustmentComments => Set<FinalAccountsAdjustmentComment>();
    public DbSet<FinalAccountsStatementLineComment> FinalAccountsStatementLineComments => Set<FinalAccountsStatementLineComment>();
    public DbSet<FinalAccountsReportVersion> FinalAccountsReportVersions => Set<FinalAccountsReportVersion>();
    public DbSet<FinalAccountsCloseRun> FinalAccountsCloseRuns => Set<FinalAccountsCloseRun>();
    public DbSet<FinalAccountsCloseChecklistItem> FinalAccountsCloseChecklistItems => Set<FinalAccountsCloseChecklistItem>();
    public DbSet<FinalAccountsCloseReportSnapshot> FinalAccountsCloseReportSnapshots => Set<FinalAccountsCloseReportSnapshot>();
    public DbSet<FinalAccountsCloseBalanceSnapshot> FinalAccountsCloseBalanceSnapshots => Set<FinalAccountsCloseBalanceSnapshot>();

    public DbSet<Employee> Employees => Set<Employee>();
    public DbSet<EmployeeDetail> EmployeeDetails => Set<EmployeeDetail>();
    public DbSet<Attendance> Attendance => Set<Attendance>();
    public DbSet<MonthlyAttendance> MonthlyAttendance => Set<MonthlyAttendance>();
    public DbSet<SalaryStructure> SalaryStructures => Set<SalaryStructure>();
    public DbSet<SalaryPaySlip> SalaryPaySlips => Set<SalaryPaySlip>();
    public DbSet<SalaryPayment> SalaryPayments => Set<SalaryPayment>();
    public DbSet<EmployeePayrollAdjustment> EmployeePayrollAdjustments => Set<EmployeePayrollAdjustment>();
    public DbSet<TimeSheet> TimeSheets => Set<TimeSheet>();

    public DbSet<AttendanceDevice> AttendanceDevices => Set<AttendanceDevice>();
    public DbSet<AttendancePunch> AttendancePunches => Set<AttendancePunch>();
    public DbSet<AttendanceShift> AttendanceShifts => Set<AttendanceShift>();
    public DbSet<AttendancePolicy> AttendancePolicies => Set<AttendancePolicy>();
    public DbSet<EmployeeAttendanceShiftRule> EmployeeAttendanceShiftRules => Set<EmployeeAttendanceShiftRule>();
    public DbSet<EmployeeBiometricEnrollment> EmployeeBiometricEnrollments => Set<EmployeeBiometricEnrollment>();
    public DbSet<AttendanceRegularizationRequest> AttendanceRegularizationRequests => Set<AttendanceRegularizationRequest>();
    public DbSet<AttendanceApproval> AttendanceApprovals => Set<AttendanceApproval>();
    public DbSet<AttendanceMonthlySummary> AttendanceMonthlySummaries => Set<AttendanceMonthlySummary>();
    public DbSet<AttendancePayrollReview> AttendancePayrollReviews => Set<AttendancePayrollReview>();
    public DbSet<AttendanceSalarySlipDraft> AttendanceSalarySlipDrafts => Set<AttendanceSalarySlipDraft>();
    public DbSet<AttendancePhotoProof> AttendancePhotoProofs => Set<AttendancePhotoProof>();
    public DbSet<AttendanceKioskSyncBatch> AttendanceKioskSyncBatches => Set<AttendanceKioskSyncBatch>();

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
        modelBuilder.Entity<GstApiProvider>().ToTable("GstApiProviders");
        modelBuilder.Entity<GstApiProvider>().HasIndex(item => new { item.CompanyId, item.StoreId, item.IsEnabled, item.Priority });
        modelBuilder.Entity<GstApiProviderFeature>().ToTable("GstApiProviderFeatures");
        modelBuilder.Entity<GstApiProviderFeature>().HasIndex(item => new { item.ProviderId, item.FeatureCode }).IsUnique();
        modelBuilder.Entity<GstApiProviderCredential>().ToTable("GstApiProviderCredentials");
        modelBuilder.Entity<GstApiProviderCredential>().HasIndex(item => new { item.ProviderId, item.CredentialKey }).IsUnique();
        modelBuilder.Entity<GstApiCallLog>().ToTable("GstApiCallLogs");
        modelBuilder.Entity<GstApiCallLog>().HasIndex(item => new { item.CompanyId, item.FeatureCode, item.CreatedAt });
        modelBuilder.Entity<GstApiCallLog>().HasIndex(item => new { item.ProviderId, item.IsSuccess, item.CreatedAt });
        modelBuilder.Entity<GstinVerificationCache>().ToTable("GstinVerificationCaches");
        modelBuilder.Entity<GstinVerificationCache>().HasIndex(item => item.Gstin).IsUnique();
        modelBuilder.Entity<GstHsnMaster>().ToTable("GstHsnMasters");
        modelBuilder.Entity<GstHsnMaster>().HasIndex(item => item.HsnCode);
        modelBuilder.Entity<GstHsnMaster>().HasIndex(item => new { item.CodeType, item.IsActive });
        modelBuilder.Entity<GstTaxRateRule>().ToTable("GstTaxRateRules");
        modelBuilder.Entity<GstTaxRateRule>().HasIndex(item => new { item.HsnCode, item.EffectiveFrom });
        modelBuilder.Entity<GstTaxRateRule>().HasIndex(item => new { item.ProductCategory, item.EffectiveFrom });
        modelBuilder.Entity<GstStateCode>().ToTable("GstStateCodes");
        modelBuilder.Entity<GstStateCode>().HasIndex(item => item.StateCode).IsUnique();
        modelBuilder.Entity<GstUqcCode>().ToTable("GstUqcCodes");
        modelBuilder.Entity<GstUqcCode>().HasIndex(item => item.UqcCode).IsUnique();
        modelBuilder.Entity<GstAuditRule>().ToTable("GstAuditRules");
        modelBuilder.Entity<GstAuditRule>().HasIndex(item => item.RuleCode).IsUnique();
        modelBuilder.Entity<GstAuditFinding>().ToTable("GstAuditFindings");
        modelBuilder.Entity<GstAuditFinding>().HasIndex(item => new { item.CompanyId, item.Status, item.Severity, item.CreatedAt });
        modelBuilder.Entity<GstAuditFinding>().HasIndex(item => new { item.CompanyId, item.ModuleArea, item.RuleCode });
        modelBuilder.Entity<GstEinvoiceIrnRecord>().ToTable("GstEinvoiceIrnRecords");
        modelBuilder.Entity<GstEinvoiceIrnRecord>().HasIndex(item => new { item.CompanyId, item.InvoiceId }).IsUnique();
        modelBuilder.Entity<GstEinvoiceIrnRecord>().HasIndex(item => new { item.CompanyId, item.Status });
        modelBuilder.Entity<GstEwaybillRecord>().ToTable("GstEwaybillRecords");
        modelBuilder.Entity<GstEwaybillRecord>().HasIndex(item => new { item.CompanyId, item.InvoiceId });
        modelBuilder.Entity<GstEwaybillRecord>().HasIndex(item => new { item.CompanyId, item.PurchaseInvoiceId });
        modelBuilder.Entity<GstEwaybillRecord>().HasIndex(item => new { item.CompanyId, item.Status });
        modelBuilder.Entity<DigitalInvoice>().ToTable("DigitalInvoices");
        modelBuilder.Entity<DigitalInvoice>().HasIndex(item => item.PublicToken).IsUnique();
        modelBuilder.Entity<DigitalInvoice>().HasIndex(item => new { item.CompanyId, item.StoreId, item.InvoiceDate });
        modelBuilder.Entity<DigitalInvoice>().HasIndex(item => new { item.CompanyId, item.InvoiceId, item.InvoiceType });
        modelBuilder.Entity<DigitalInvoiceEvent>().ToTable("DigitalInvoiceEvents");
        modelBuilder.Entity<DigitalInvoiceEvent>().HasIndex(item => new { item.CompanyId, item.DigitalInvoiceId, item.EventAt });
        modelBuilder.Entity<DigitalInvoiceEvent>().HasIndex(item => new { item.CompanyId, item.StoreId, item.EventType, item.EventAt });
        modelBuilder.Entity<StoreReviewSetting>().ToTable("StoreReviewSettings");
        modelBuilder.Entity<StoreReviewSetting>().HasIndex(item => new { item.CompanyId, item.StoreId }).IsUnique(false);
        modelBuilder.Entity<CustomerFeedback>().ToTable("CustomerFeedback");
        modelBuilder.Entity<CustomerFeedback>().HasIndex(item => new { item.CompanyId, item.StoreId, item.SubmittedAt });
        modelBuilder.Entity<CustomerFeedback>().HasIndex(item => new { item.CompanyId, item.DigitalInvoiceId });
        modelBuilder.Entity<WhatsAppProviderSetting>().ToTable("WhatsAppProviderSettings");
        modelBuilder.Entity<WhatsAppProviderSetting>().HasIndex(item => new { item.CompanyId, item.StoreId }).IsUnique(false);
        modelBuilder.Entity<WhatsAppProviderSetting>().HasIndex(item => new { item.CompanyId, item.StoreId, item.IsEnabled, item.AutoSendDigitalBills });
        modelBuilder.Entity<WhatsAppMessageLog>().ToTable("WhatsAppMessageLogs");
        modelBuilder.Entity<WhatsAppMessageLog>().HasIndex(item => new { item.CompanyId, item.StoreId, item.Status, item.CreatedAt });
        modelBuilder.Entity<WhatsAppMessageLog>().HasIndex(item => new { item.CompanyId, item.DigitalInvoiceId });
        modelBuilder.Entity<InvoiceAdBanner>().ToTable("InvoiceAdBanners");
        modelBuilder.Entity<InvoiceAdBanner>().HasIndex(item => new { item.CompanyId, item.StoreId, item.Position, item.IsActive });
        modelBuilder.Entity<DigitalBillCampaign>().ToTable("DigitalBillCampaigns");
        modelBuilder.Entity<DigitalBillCampaign>().HasIndex(item => new { item.CompanyId, item.StoreId, item.Status, item.CreatedAt });
        modelBuilder.Entity<DigitalBillCampaign>().HasIndex(item => new { item.CompanyId, item.Segment, item.FromDate, item.ToDate });
        modelBuilder.Entity<DigitalBillCampaignRecipient>().ToTable("DigitalBillCampaignRecipients");
        modelBuilder.Entity<DigitalBillCampaignRecipient>().HasIndex(item => new { item.CompanyId, item.CampaignId, item.Status });
        modelBuilder.Entity<DigitalBillCampaignRecipient>().HasIndex(item => new { item.CompanyId, item.CustomerMobile, item.CreatedAt });
        modelBuilder.Entity<GstReturnAuditEntry>().HasIndex(entry => new { entry.CompanyId, entry.DraftId, entry.CreatedAt });
        modelBuilder.Entity<GstReturnAuditEntry>().HasIndex(entry => new { entry.CompanyId, entry.Form, entry.ReturnPeriod });
        modelBuilder.Entity<AuditLogEntry>().ToTable("AuditLogEntries");
        modelBuilder.Entity<AuditLogEntry>().HasIndex(entry => entry.OccurredAt);
        modelBuilder.Entity<AuditLogEntry>().HasIndex(entry => new { entry.CompanyId, entry.StoreId, entry.OccurredAt });
        modelBuilder.Entity<AuditLogEntry>().HasIndex(entry => new { entry.EntityName, entry.EntityId });
        modelBuilder.Entity<AuditLogEntry>().HasIndex(entry => new { entry.Module, entry.Action, entry.OccurredAt });
        modelBuilder.Entity<PosHeldBill>().ToTable("PosHeldBills");
        modelBuilder.Entity<PosHeldBill>().HasIndex(item => new { item.CompanyId, item.StoreGroupId, item.StoreId, item.Status, item.HeldAt });
        modelBuilder.Entity<PosHeldBill>().HasIndex(item => new { item.CompanyId, item.ClientHeldBillId });
        modelBuilder.Entity<DotMatrixPrintSetting>().ToTable("DotMatrixPrintSettings");
        modelBuilder.Entity<DotMatrixPrintSetting>().HasIndex(item => new { item.CompanyId, item.StoreId }).IsUnique(false);
        modelBuilder.Entity<DotMatrixPrintQueueEntry>().ToTable("DotMatrixPrintQueueEntries");
        modelBuilder.Entity<DotMatrixPrintQueueEntry>().HasIndex(item => new { item.CompanyId, item.StoreId, item.BusinessDate, item.SequenceNo });
        modelBuilder.Entity<DotMatrixPrintQueueEntry>().HasIndex(item => new { item.Status, item.CreatedAt });
        modelBuilder.Entity<DotMatrixPrintQueueEntry>().HasIndex(item => new { item.SourceType, item.SourceId, item.ActionType });
        modelBuilder.Entity<DotMatrixPrintQueueEntry>().HasIndex(item => item.DeduplicationKey);
        modelBuilder.Entity<VoucherBase>().UseTpcMappingStrategy();
        modelBuilder.Entity<Voucher>().ToTable("Vouchers");
        modelBuilder.Entity<CashVoucher>().ToTable("CashVouchers");
        modelBuilder.Entity<CashVoucherConversion>().ToTable("CashVoucherConversions");
        modelBuilder.Entity<FinalAccountsModuleSettings>().ToTable("fa_module_settings", "final_accounts");
        modelBuilder.Entity<FinalAccountsModuleSettings>().HasIndex(item => new { item.CompanyId, item.StoreGroupId, item.StoreId }).IsUnique();
        modelBuilder.Entity<FinalAccountsModuleSettings>().HasIndex(item => new { item.Enabled, item.UpdatedAt });
        modelBuilder.Entity<FinalAccountsModuleSettings>().Property(item => item.PostingMode).HasMaxLength(32);
        modelBuilder.Entity<FinalAccountsModuleSettings>().Property(item => item.StatementTemplate).HasMaxLength(80);
        modelBuilder.Entity<FinalAccountsModuleSettings>().Property(item => item.InventoryValuationMethod).HasMaxLength(48);
        modelBuilder.Entity<FinalAccountsModuleSettings>().Property(item => item.CreatedBy).HasMaxLength(120);
        modelBuilder.Entity<FinalAccountsModuleSettings>().Property(item => item.UpdatedBy).HasMaxLength(120);
        ConfigureFinalAccountsCatalog(modelBuilder);
        ConfigureFinalAccountsPostingRules(modelBuilder);
        ConfigureFinalAccountsGeneralLedger(modelBuilder);
        ConfigureFinalAccountsSync(modelBuilder);
        ConfigureFinalAccountsCaWorkspace(modelBuilder);
        ConfigureFinalAccountsPeriodClose(modelBuilder);

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
        modelBuilder.Entity<Stock>().HasIndex(stock => new { stock.CompanyId, stock.StoreId, stock.ProductId, stock.IsOFB });
        modelBuilder.Entity<Stock>().HasIndex(stock => new { stock.CompanyId, stock.StoreId, stock.Barcode });
        modelBuilder.Entity<PurchaseInvoiceImportBatch>().ToTable("PurchaseInvoiceImportBatches");
        modelBuilder.Entity<PurchaseInvoiceImportBatch>().HasIndex(item => new { item.CompanyId, item.StoreId, item.Status, item.CreatedAt });
        modelBuilder.Entity<PurchaseInvoiceImportBatch>().HasIndex(item => new { item.CompanyId, item.VendorGstinFinal, item.SupplierInvoiceNumber });
        modelBuilder.Entity<PurchaseInvoiceImportBatch>().HasIndex(item => item.Sha256Hash);
        modelBuilder.Entity<PurchaseInvoiceImportBatch>().HasIndex(item => item.PostedPurchaseInvoiceId);
        modelBuilder.Entity<PurchaseInvoiceImportLine>().ToTable("PurchaseInvoiceImportLines");
        modelBuilder.Entity<PurchaseInvoiceImportLine>().HasIndex(item => new { item.CompanyId, item.BatchId, item.LineNumber });
        modelBuilder.Entity<PurchaseInvoiceImportLine>().HasIndex(item => new { item.CompanyId, item.ProductId });
        modelBuilder.Entity<PurchaseInvoiceImportFile>().ToTable("PurchaseInvoiceImportFiles");
        modelBuilder.Entity<PurchaseInvoiceImportFile>().HasIndex(item => new { item.CompanyId, item.BatchId, item.FileKind });
        modelBuilder.Entity<PurchaseInvoiceImportFile>().HasIndex(item => item.Sha256Hash);
        modelBuilder.Entity<PurchaseInvoiceImportVendorProfile>().ToTable("PurchaseInvoiceImportVendorProfiles");
        modelBuilder.Entity<PurchaseInvoiceImportVendorProfile>().HasIndex(item => new { item.CompanyId, item.VendorGstin });
        modelBuilder.Entity<PurchaseInvoiceImportVendorProfile>().HasIndex(item => new { item.CompanyId, item.VendorId });
        modelBuilder.Entity<PurchaseInvoice>().HasIndex(invoice => new { invoice.CompanyId, invoice.StoreId, invoice.OnDate });
        modelBuilder.Entity<PurchaseInvoice>().HasIndex(invoice => new { invoice.CompanyId, invoice.StoreId, invoice.InwardDate });
        modelBuilder.Entity<PurchaseInvoice>().HasIndex(invoice => new { invoice.CompanyId, invoice.StoreId, invoice.VendorId, invoice.OnDate });
        modelBuilder.Entity<InvoiceItem>().HasIndex(item => new { item.CompanyId, item.InvoiceId });
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
        modelBuilder.Entity<TailoringVendorServiceRate>().HasIndex(item => new { item.CompanyId, item.StoreId, item.VendorId, item.ServiceItemId, item.Active });
        modelBuilder.Entity<TailoringVendorServiceRate>().HasIndex(item => new { item.CompanyId, item.VendorId, item.ServiceItemId });
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
        modelBuilder.Entity<Employee>().Property(employee => employee.MonthlySalary).HasPrecision(18, 2);
        modelBuilder.Entity<Employee>().Property(employee => employee.DailyWage).HasPrecision(18, 2);
        modelBuilder.Entity<Employee>().HasIndex(employee => new { employee.CompanyId, employee.StoreId, employee.Mobile });
        modelBuilder.Entity<Employee>().HasIndex(employee => new { employee.CompanyId, employee.StoreId, employee.EmployeeCode }).IsUnique(false);
        modelBuilder.Entity<EmployeePayrollAdjustment>().Property(item => item.Amount).HasPrecision(18, 2);
        modelBuilder.Entity<EmployeePayrollAdjustment>().Property(item => item.LeaveDays).HasPrecision(18, 2);
        modelBuilder.Entity<EmployeePayrollAdjustment>().Property(item => item.RecoveredAmount).HasPrecision(18, 2);
        modelBuilder.Entity<EmployeePayrollAdjustment>().Property(item => item.PfEmployee).HasPrecision(18, 2);
        modelBuilder.Entity<EmployeePayrollAdjustment>().Property(item => item.PfEmployer).HasPrecision(18, 2);
        modelBuilder.Entity<EmployeePayrollAdjustment>().Property(item => item.GratuityAmount).HasPrecision(18, 2);
        modelBuilder.Entity<EmployeePayrollAdjustment>().HasIndex(item => new { item.CompanyId, item.StoreId, item.EmployeeId, item.AdjustmentType, item.Status });
        modelBuilder.Entity<EmployeePayrollAdjustment>().HasIndex(item => new { item.CompanyId, item.OnDate });

        modelBuilder.Entity<AttendanceDevice>().HasIndex(item => new { item.CompanyId, item.StoreId, item.DeviceCode });
        modelBuilder.Entity<AttendanceDevice>().HasIndex(item => new { item.CompanyId, item.StoreId, item.Status });
        modelBuilder.Entity<AttendancePunch>().Property(item => item.Latitude).HasPrecision(12, 8);
        modelBuilder.Entity<AttendancePunch>().Property(item => item.Longitude).HasPrecision(12, 8);
        modelBuilder.Entity<AttendancePunch>().Property(item => item.ConfidenceScore).HasPrecision(7, 4);
        modelBuilder.Entity<AttendancePunch>().HasIndex(item => new { item.CompanyId, item.StoreId, item.EmployeeId, item.LocalPunchTime });
        modelBuilder.Entity<AttendancePunch>().HasIndex(item => new { item.CompanyId, item.StoreId, item.DeviceId, item.PunchTimeUtc });
        modelBuilder.Entity<AttendancePunch>().HasIndex(item => new { item.CompanyId, item.ClientPunchId });
        modelBuilder.Entity<AttendanceShift>().HasIndex(item => new { item.CompanyId, item.StoreId, item.Active });
        modelBuilder.Entity<EmployeeAttendanceShiftRule>().HasIndex(item => new { item.CompanyId, item.StoreId, item.Active, item.Priority });
        modelBuilder.Entity<EmployeeAttendanceShiftRule>().HasIndex(item => new { item.CompanyId, item.StoreId, item.EmployeeId, item.Active });
        modelBuilder.Entity<AttendancePolicy>().HasIndex(item => new { item.CompanyId, item.StoreId, item.Active });
        modelBuilder.Entity<Attendance>().HasIndex(item => new { item.CompanyId, item.StoreId, item.OnDate });
        modelBuilder.Entity<Attendance>().HasIndex(item => new { item.CompanyId, item.StoreId, item.EmployeeId, item.OnDate });
        modelBuilder.Entity<EmployeeBiometricEnrollment>().HasIndex(item => new { item.CompanyId, item.StoreId, item.EmployeeId });
        modelBuilder.Entity<AttendanceRegularizationRequest>().HasIndex(item => new { item.CompanyId, item.StoreId, item.EmployeeId, item.Status });
        modelBuilder.Entity<AttendanceApproval>().HasIndex(item => new { item.CompanyId, item.StoreId, item.RequestId });
        modelBuilder.Entity<AttendanceMonthlySummary>().Property(item => item.PresentDays).HasPrecision(18, 2);
        modelBuilder.Entity<AttendanceMonthlySummary>().Property(item => item.AbsentDays).HasPrecision(18, 2);
        modelBuilder.Entity<AttendanceMonthlySummary>().Property(item => item.LateDays).HasPrecision(18, 2);
        modelBuilder.Entity<AttendanceMonthlySummary>().Property(item => item.HalfDays).HasPrecision(18, 2);
        modelBuilder.Entity<AttendanceMonthlySummary>().Property(item => item.LeaveDays).HasPrecision(18, 2);
        modelBuilder.Entity<AttendanceMonthlySummary>().HasIndex(item => new { item.CompanyId, item.StoreId, item.EmployeeId, item.Year, item.Month });

        modelBuilder.Entity<AttendancePayrollReview>().Property(item => item.PresentDays).HasPrecision(18, 2);
        modelBuilder.Entity<AttendancePayrollReview>().Property(item => item.AbsentDays).HasPrecision(18, 2);
        modelBuilder.Entity<AttendancePayrollReview>().Property(item => item.LateDays).HasPrecision(18, 2);
        modelBuilder.Entity<AttendancePayrollReview>().Property(item => item.HalfDays).HasPrecision(18, 2);
        modelBuilder.Entity<AttendancePayrollReview>().Property(item => item.LeaveDays).HasPrecision(18, 2);
        modelBuilder.Entity<AttendancePayrollReview>().Property(item => item.PayableDays).HasPrecision(18, 2);
        modelBuilder.Entity<AttendancePayrollReview>().Property(item => item.DeductionDays).HasPrecision(18, 2);
        modelBuilder.Entity<AttendancePayrollReview>().Property(item => item.EstimatedDailyRate).HasPrecision(18, 2);
        modelBuilder.Entity<AttendancePayrollReview>().Property(item => item.EstimatedGrossPay).HasPrecision(18, 2);
        modelBuilder.Entity<AttendancePayrollReview>().HasIndex(item => new { item.CompanyId, item.StoreId, item.EmployeeId, item.Year, item.Month });
        modelBuilder.Entity<AttendancePayrollReview>().HasIndex(item => new { item.CompanyId, item.StoreId, item.Year, item.Month, item.ReviewStatus });

        modelBuilder.Entity<AttendanceSalarySlipDraft>().Property(item => item.PresentDays).HasPrecision(18, 2);
        modelBuilder.Entity<AttendanceSalarySlipDraft>().Property(item => item.AbsentDays).HasPrecision(18, 2);
        modelBuilder.Entity<AttendanceSalarySlipDraft>().Property(item => item.LateDays).HasPrecision(18, 2);
        modelBuilder.Entity<AttendanceSalarySlipDraft>().Property(item => item.HalfDays).HasPrecision(18, 2);
        modelBuilder.Entity<AttendanceSalarySlipDraft>().Property(item => item.LeaveDays).HasPrecision(18, 2);
        modelBuilder.Entity<AttendanceSalarySlipDraft>().Property(item => item.PayableDays).HasPrecision(18, 2);
        modelBuilder.Entity<AttendanceSalarySlipDraft>().Property(item => item.DeductionDays).HasPrecision(18, 2);
        modelBuilder.Entity<AttendanceSalarySlipDraft>().Property(item => item.MonthlySalary).HasPrecision(18, 2);
        modelBuilder.Entity<AttendanceSalarySlipDraft>().Property(item => item.DailyRate).HasPrecision(18, 2);
        modelBuilder.Entity<AttendanceSalarySlipDraft>().Property(item => item.AttendanceGrossPreview).HasPrecision(18, 2);
        modelBuilder.Entity<AttendanceSalarySlipDraft>().Property(item => item.AttendanceDeductionPreview).HasPrecision(18, 2);
        modelBuilder.Entity<AttendanceSalarySlipDraft>().Property(item => item.BonusPreview).HasPrecision(18, 2);
        modelBuilder.Entity<AttendanceSalarySlipDraft>().Property(item => item.LeaveEncashmentPreview).HasPrecision(18, 2);
        modelBuilder.Entity<AttendanceSalarySlipDraft>().Property(item => item.SalaryAdvanceRecoveryPreview).HasPrecision(18, 2);
        modelBuilder.Entity<AttendanceSalarySlipDraft>().Property(item => item.PfEmployeePreview).HasPrecision(18, 2);
        modelBuilder.Entity<AttendanceSalarySlipDraft>().Property(item => item.GratuityPreview).HasPrecision(18, 2);
        modelBuilder.Entity<AttendanceSalarySlipDraft>().Property(item => item.OtherDeductionPreview).HasPrecision(18, 2);
        modelBuilder.Entity<AttendanceSalarySlipDraft>().Property(item => item.NetPayPreview).HasPrecision(18, 2);
        modelBuilder.Entity<AttendanceSalarySlipDraft>().HasIndex(item => new { item.CompanyId, item.StoreId, item.EmployeeId, item.Year, item.Month });
        modelBuilder.Entity<AttendanceSalarySlipDraft>().HasIndex(item => new { item.CompanyId, item.StoreId, item.Year, item.Month, item.DraftStatus });
        modelBuilder.Entity<AttendanceSalarySlipDraft>().HasIndex(item => new { item.CompanyId, item.StoreId, item.Year, item.Month, item.PayrollPostStatus });
        modelBuilder.Entity<AttendanceSalarySlipDraft>().HasIndex(item => item.GeneratedSalaryPaySlipId);
        modelBuilder.Entity<AttendanceSalarySlipDraft>().HasIndex(item => item.GeneratedSalaryPaymentId);
        modelBuilder.Entity<AttendanceSalarySlipDraft>().HasIndex(item => new { item.CompanyId, item.StoreId, item.Year, item.Month, item.PaymentPostStatus });
        modelBuilder.Entity<AttendancePhotoProof>().HasIndex(item => new { item.CompanyId, item.StoreId, item.EmployeeId, item.CapturedAtUtc });
        modelBuilder.Entity<AttendancePhotoProof>().HasIndex(item => new { item.CompanyId, item.StoreId, item.ReviewStatus, item.CapturedAtUtc });
        modelBuilder.Entity<AttendancePhotoProof>().HasIndex(item => new { item.CompanyId, item.ClientPunchId });
        modelBuilder.Entity<AttendanceKioskSyncBatch>().HasIndex(item => new { item.CompanyId, item.StoreId, item.DeviceId, item.ReceivedAtUtc });
    }

    private static void ConfigureFinalAccountsCatalog(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<FinalAccountsAccountGroup>().ToTable("fa_account_groups", "final_accounts");
        modelBuilder.Entity<FinalAccountsAccountGroup>().HasIndex(item => new { item.CompanyId, item.StoreGroupId, item.StoreId, item.Code }).IsUnique();
        modelBuilder.Entity<FinalAccountsAccountGroup>().HasIndex(item => new { item.CompanyId, item.StoreGroupId, item.StoreId, item.AccountType, item.IsActive });
        modelBuilder.Entity<FinalAccountsAccountGroup>().HasIndex(item => item.ParentGroupId);
        modelBuilder.Entity<FinalAccountsAccountGroup>().Property(item => item.Code).HasMaxLength(40);
        modelBuilder.Entity<FinalAccountsAccountGroup>().Property(item => item.Name).HasMaxLength(160);
        modelBuilder.Entity<FinalAccountsAccountGroup>().Property(item => item.Description).HasMaxLength(500);
        modelBuilder.Entity<FinalAccountsAccountGroup>().Property(item => item.CreatedBy).HasMaxLength(120);
        modelBuilder.Entity<FinalAccountsAccountGroup>().Property(item => item.UpdatedBy).HasMaxLength(120);
        modelBuilder.Entity<FinalAccountsAccountGroup>().Property(item => item.Revision).IsConcurrencyToken();

        modelBuilder.Entity<FinalAccountsAccount>().ToTable("fa_accounts", "final_accounts");
        modelBuilder.Entity<FinalAccountsAccount>().HasIndex(item => new { item.CompanyId, item.StoreGroupId, item.StoreId, item.Code }).IsUnique();
        modelBuilder.Entity<FinalAccountsAccount>().HasIndex(item => new { item.CompanyId, item.StoreGroupId, item.StoreId, item.AccountGroupId, item.IsActive });
        modelBuilder.Entity<FinalAccountsAccount>().HasIndex(item => item.ParentAccountId);
        modelBuilder.Entity<FinalAccountsAccount>().Property(item => item.Code).HasMaxLength(40);
        modelBuilder.Entity<FinalAccountsAccount>().Property(item => item.Name).HasMaxLength(160);
        modelBuilder.Entity<FinalAccountsAccount>().Property(item => item.Description).HasMaxLength(500);
        modelBuilder.Entity<FinalAccountsAccount>().Property(item => item.OpeningBalance).HasPrecision(18, 2);
        modelBuilder.Entity<FinalAccountsAccount>().Property(item => item.CreatedBy).HasMaxLength(120);
        modelBuilder.Entity<FinalAccountsAccount>().Property(item => item.UpdatedBy).HasMaxLength(120);
        modelBuilder.Entity<FinalAccountsAccount>().Property(item => item.Revision).IsConcurrencyToken();

        modelBuilder.Entity<FinalAccountsAccountMapping>().ToTable("fa_account_mappings", "final_accounts");
        modelBuilder.Entity<FinalAccountsAccountMapping>().HasIndex(item => new { item.CompanyId, item.StoreGroupId, item.StoreId, item.SourceType, item.MappingKey }).IsUnique();
        modelBuilder.Entity<FinalAccountsAccountMapping>().HasIndex(item => new { item.AccountId, item.IsActive });
        modelBuilder.Entity<FinalAccountsAccountMapping>().Property(item => item.MappingKey).HasMaxLength(120);
        modelBuilder.Entity<FinalAccountsAccountMapping>().Property(item => item.DisplayName).HasMaxLength(160);
        modelBuilder.Entity<FinalAccountsAccountMapping>().Property(item => item.Notes).HasMaxLength(500);
        modelBuilder.Entity<FinalAccountsAccountMapping>().Property(item => item.CreatedBy).HasMaxLength(120);
        modelBuilder.Entity<FinalAccountsAccountMapping>().Property(item => item.UpdatedBy).HasMaxLength(120);
        modelBuilder.Entity<FinalAccountsAccountMapping>().Property(item => item.Revision).IsConcurrencyToken();

        modelBuilder.Entity<FinalAccountsFiscalYear>().ToTable("fa_fiscal_years", "final_accounts");
        modelBuilder.Entity<FinalAccountsFiscalYear>().HasIndex(item => new { item.CompanyId, item.StoreGroupId, item.StoreId, item.Name }).IsUnique();
        modelBuilder.Entity<FinalAccountsFiscalYear>().HasIndex(item => new { item.CompanyId, item.StoreGroupId, item.StoreId, item.StartDate, item.EndDate });
        modelBuilder.Entity<FinalAccountsFiscalYear>().Property(item => item.Name).HasMaxLength(80);
        modelBuilder.Entity<FinalAccountsFiscalYear>().Property(item => item.CreatedBy).HasMaxLength(120);
        modelBuilder.Entity<FinalAccountsFiscalYear>().Property(item => item.UpdatedBy).HasMaxLength(120);
        modelBuilder.Entity<FinalAccountsFiscalYear>().Property(item => item.Revision).IsConcurrencyToken();

        modelBuilder.Entity<FinalAccountsFiscalPeriod>().ToTable("fa_fiscal_periods", "final_accounts");
        modelBuilder.Entity<FinalAccountsFiscalPeriod>().HasIndex(item => new { item.FiscalYearId, item.PeriodNumber }).IsUnique();
        modelBuilder.Entity<FinalAccountsFiscalPeriod>().HasIndex(item => new { item.CompanyId, item.StoreGroupId, item.StoreId, item.StartDate, item.EndDate });
        modelBuilder.Entity<FinalAccountsFiscalPeriod>().Property(item => item.Name).HasMaxLength(80);
        modelBuilder.Entity<FinalAccountsFiscalPeriod>().Property(item => item.CreatedBy).HasMaxLength(120);
        modelBuilder.Entity<FinalAccountsFiscalPeriod>().Property(item => item.UpdatedBy).HasMaxLength(120);
        modelBuilder.Entity<FinalAccountsFiscalPeriod>().Property(item => item.Revision).IsConcurrencyToken();
    }

    private static void ConfigureFinalAccountsPostingRules(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<FinalAccountsPostingRule>().ToTable("fa_posting_rules", "final_accounts");
        modelBuilder.Entity<FinalAccountsPostingRule>().HasIndex(item => new { item.CompanyId, item.StoreGroupId, item.StoreId, item.SourceType, item.RuleCode, item.Version }).IsUnique();
        modelBuilder.Entity<FinalAccountsPostingRule>().HasIndex(item => new { item.CompanyId, item.StoreGroupId, item.StoreId, item.SourceType, item.IsActive });
        modelBuilder.Entity<FinalAccountsPostingRule>().Property(item => item.SourceType).HasMaxLength(80);
        modelBuilder.Entity<FinalAccountsPostingRule>().Property(item => item.RuleCode).HasMaxLength(80);
        modelBuilder.Entity<FinalAccountsPostingRule>().Property(item => item.Version).HasMaxLength(80);
        modelBuilder.Entity<FinalAccountsPostingRule>().Property(item => item.Name).HasMaxLength(160);
        modelBuilder.Entity<FinalAccountsPostingRule>().Property(item => item.Description).HasMaxLength(500);
        modelBuilder.Entity<FinalAccountsPostingRule>().Property(item => item.CreatedBy).HasMaxLength(120);
        modelBuilder.Entity<FinalAccountsPostingRule>().Property(item => item.UpdatedBy).HasMaxLength(120);
        modelBuilder.Entity<FinalAccountsPostingRule>().Property(item => item.Revision).IsConcurrencyToken();

        modelBuilder.Entity<FinalAccountsPostingRuleLine>().ToTable("fa_posting_rule_lines", "final_accounts");
        modelBuilder.Entity<FinalAccountsPostingRuleLine>().HasIndex(item => new { item.PostingRuleId, item.MappingKey }).IsUnique();
        modelBuilder.Entity<FinalAccountsPostingRuleLine>().HasIndex(item => new { item.CompanyId, item.StoreGroupId, item.StoreId, item.MappingCategory, item.IsRequired });
        modelBuilder.Entity<FinalAccountsPostingRuleLine>().Property(item => item.MappingKey).HasMaxLength(120);
        modelBuilder.Entity<FinalAccountsPostingRuleLine>().Property(item => item.DisplayName).HasMaxLength(160);
        modelBuilder.Entity<FinalAccountsPostingRuleLine>().Property(item => item.MappingCategory).HasMaxLength(80);
        modelBuilder.Entity<FinalAccountsPostingRuleLine>().Property(item => item.Direction).HasMaxLength(16);
        modelBuilder.Entity<FinalAccountsPostingRuleLine>().Property(item => item.ExpectedAccountType).HasMaxLength(40);
        modelBuilder.Entity<FinalAccountsPostingRuleLine>().Property(item => item.Notes).HasMaxLength(500);
        modelBuilder.Entity<FinalAccountsPostingRuleLine>().Property(item => item.CreatedBy).HasMaxLength(120);
        modelBuilder.Entity<FinalAccountsPostingRuleLine>().Property(item => item.UpdatedBy).HasMaxLength(120);
        modelBuilder.Entity<FinalAccountsPostingRuleLine>().Property(item => item.Revision).IsConcurrencyToken();
    }

    private static void ConfigureFinalAccountsGeneralLedger(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<FinalAccountsJournalEntry>().ToTable("fa_journal_entries", "final_accounts");
        modelBuilder.Entity<FinalAccountsJournalEntry>().HasIndex(item => new { item.CompanyId, item.StoreGroupId, item.StoreId, item.EntryNumber }).IsUnique();
        modelBuilder.Entity<FinalAccountsJournalEntry>().HasIndex(item => new { item.CompanyId, item.StoreGroupId, item.StoreId, item.OnDate, item.Status });
        modelBuilder.Entity<FinalAccountsJournalEntry>().HasIndex(item => new { item.CompanyId, item.StoreGroupId, item.StoreId, item.SourceType, item.SourceId });
        modelBuilder.Entity<FinalAccountsJournalEntry>().HasIndex(item => new { item.CompanyId, item.StoreGroupId, item.StoreId, item.IdempotencyKey }).IsUnique();
        modelBuilder.Entity<FinalAccountsJournalEntry>().HasIndex(item => item.FiscalPeriodId);
        modelBuilder.Entity<FinalAccountsJournalEntry>().HasIndex(item => item.ReversalOfJournalEntryId);
        modelBuilder.Entity<FinalAccountsJournalEntry>().HasIndex(item => item.ReversalJournalEntryId);
        modelBuilder.Entity<FinalAccountsJournalEntry>().Property(item => item.EntryNumber).HasMaxLength(48);
        modelBuilder.Entity<FinalAccountsJournalEntry>().Property(item => item.SourceType).HasMaxLength(80);
        modelBuilder.Entity<FinalAccountsJournalEntry>().Property(item => item.ReferenceNumber).HasMaxLength(120);
        modelBuilder.Entity<FinalAccountsJournalEntry>().Property(item => item.Narration).HasMaxLength(500);
        modelBuilder.Entity<FinalAccountsJournalEntry>().Property(item => item.IdempotencyKey).HasMaxLength(160);
        modelBuilder.Entity<FinalAccountsJournalEntry>().Property(item => item.PostedBy).HasMaxLength(120);
        modelBuilder.Entity<FinalAccountsJournalEntry>().Property(item => item.ReversedBy).HasMaxLength(120);
        modelBuilder.Entity<FinalAccountsJournalEntry>().Property(item => item.CreatedBy).HasMaxLength(120);
        modelBuilder.Entity<FinalAccountsJournalEntry>().Property(item => item.UpdatedBy).HasMaxLength(120);
        modelBuilder.Entity<FinalAccountsJournalEntry>().Property(item => item.Revision).IsConcurrencyToken();

        modelBuilder.Entity<FinalAccountsJournalLine>().ToTable("fa_journal_lines", "final_accounts");
        modelBuilder.Entity<FinalAccountsJournalLine>().HasIndex(item => new { item.CompanyId, item.StoreGroupId, item.StoreId, item.JournalEntryId, item.LineNumber }).IsUnique();
        modelBuilder.Entity<FinalAccountsJournalLine>().HasIndex(item => new { item.CompanyId, item.StoreGroupId, item.StoreId, item.AccountId, item.JournalEntryId });
        modelBuilder.Entity<FinalAccountsJournalLine>().ToTable(item =>
        {
            item.HasCheckConstraint("CK_fa_journal_lines_debit_credit_non_negative", @"""Debit"" >= 0 AND ""Credit"" >= 0");
            item.HasCheckConstraint("CK_fa_journal_lines_single_side", @"((""Debit"" > 0 AND ""Credit"" = 0) OR (""Credit"" > 0 AND ""Debit"" = 0))");
        });
        modelBuilder.Entity<FinalAccountsJournalLine>().Property(item => item.Debit).HasPrecision(18, 2);
        modelBuilder.Entity<FinalAccountsJournalLine>().Property(item => item.Credit).HasPrecision(18, 2);
        modelBuilder.Entity<FinalAccountsJournalLine>().Property(item => item.Narration).HasMaxLength(500);
        modelBuilder.Entity<FinalAccountsJournalLine>().Property(item => item.CreatedBy).HasMaxLength(120);
        modelBuilder.Entity<FinalAccountsJournalLine>().Property(item => item.UpdatedBy).HasMaxLength(120);
        modelBuilder.Entity<FinalAccountsJournalLine>().Property(item => item.Revision).IsConcurrencyToken();

        modelBuilder.Entity<FinalAccountsSourcePostingLink>().ToTable("fa_source_posting_links", "final_accounts");
        modelBuilder.Entity<FinalAccountsSourcePostingLink>().HasIndex(item => new { item.CompanyId, item.StoreGroupId, item.StoreId, item.SourceType, item.SourceId }).IsUnique();
        modelBuilder.Entity<FinalAccountsSourcePostingLink>().HasIndex(item => new { item.CompanyId, item.StoreGroupId, item.StoreId, item.IdempotencyKey }).IsUnique();
        modelBuilder.Entity<FinalAccountsSourcePostingLink>().HasIndex(item => item.JournalEntryId);
        modelBuilder.Entity<FinalAccountsSourcePostingLink>().Property(item => item.SourceType).HasMaxLength(80);
        modelBuilder.Entity<FinalAccountsSourcePostingLink>().Property(item => item.SourceReference).HasMaxLength(120);
        modelBuilder.Entity<FinalAccountsSourcePostingLink>().Property(item => item.SourceHash).HasMaxLength(128);
        modelBuilder.Entity<FinalAccountsSourcePostingLink>().Property(item => item.MappingVersion).HasMaxLength(80);
        modelBuilder.Entity<FinalAccountsSourcePostingLink>().Property(item => item.IdempotencyKey).HasMaxLength(160);
        modelBuilder.Entity<FinalAccountsSourcePostingLink>().Property(item => item.CreatedBy).HasMaxLength(120);
        modelBuilder.Entity<FinalAccountsSourcePostingLink>().Property(item => item.UpdatedBy).HasMaxLength(120);
        modelBuilder.Entity<FinalAccountsSourcePostingLink>().Property(item => item.Revision).IsConcurrencyToken();
    }

    private static void ConfigureFinalAccountsSync(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<FinalAccountsSyncJob>().ToTable("fa_sync_jobs", "final_accounts");
        modelBuilder.Entity<FinalAccountsSyncJob>().HasIndex(item => new { item.CompanyId, item.StoreGroupId, item.StoreId, item.JobNumber }).IsUnique();
        modelBuilder.Entity<FinalAccountsSyncJob>().HasIndex(item => new { item.CompanyId, item.StoreGroupId, item.StoreId, item.IdempotencyKey }).IsUnique();
        modelBuilder.Entity<FinalAccountsSyncJob>().HasIndex(item => new { item.CompanyId, item.StoreGroupId, item.StoreId, item.Status, item.CreatedAt });
        modelBuilder.Entity<FinalAccountsSyncJob>().HasIndex(item => new { item.CompanyId, item.StoreGroupId, item.StoreId, item.DryRun, item.Scheduled });
        modelBuilder.Entity<FinalAccountsSyncJob>().Property(item => item.JobNumber).HasMaxLength(64);
        modelBuilder.Entity<FinalAccountsSyncJob>().Property(item => item.Mode).HasMaxLength(32);
        modelBuilder.Entity<FinalAccountsSyncJob>().Property(item => item.ModulesCsv).HasMaxLength(300);
        modelBuilder.Entity<FinalAccountsSyncJob>().Property(item => item.IdempotencyKey).HasMaxLength(160);
        modelBuilder.Entity<FinalAccountsSyncJob>().Property(item => item.LastCheckpoint).HasMaxLength(240);
        modelBuilder.Entity<FinalAccountsSyncJob>().Property(item => item.ErrorPolicy).HasMaxLength(32);
        modelBuilder.Entity<FinalAccountsSyncJob>().Property(item => item.CreatedBy).HasMaxLength(120);
        modelBuilder.Entity<FinalAccountsSyncJob>().Property(item => item.UpdatedBy).HasMaxLength(120);
        modelBuilder.Entity<FinalAccountsSyncJob>().Property(item => item.Revision).IsConcurrencyToken();

        modelBuilder.Entity<FinalAccountsSyncJobItem>().ToTable("fa_sync_job_items", "final_accounts");
        modelBuilder.Entity<FinalAccountsSyncJobItem>().HasIndex(item => new { item.JobId, item.SourceType, item.SourceId }).IsUnique();
        modelBuilder.Entity<FinalAccountsSyncJobItem>().HasIndex(item => new { item.CompanyId, item.StoreGroupId, item.StoreId, item.SourceType, item.SourceId });
        modelBuilder.Entity<FinalAccountsSyncJobItem>().HasIndex(item => new { item.JobId, item.Status, item.NextAttemptAt });
        modelBuilder.Entity<FinalAccountsSyncJobItem>().Property(item => item.SourceType).HasMaxLength(80);
        modelBuilder.Entity<FinalAccountsSyncJobItem>().Property(item => item.SourceReference).HasMaxLength(120);
        modelBuilder.Entity<FinalAccountsSyncJobItem>().Property(item => item.SourceAmount).HasPrecision(18, 2);
        modelBuilder.Entity<FinalAccountsSyncJobItem>().Property(item => item.SourceHash).HasMaxLength(128);
        modelBuilder.Entity<FinalAccountsSyncJobItem>().Property(item => item.ErrorCode).HasMaxLength(80);
        modelBuilder.Entity<FinalAccountsSyncJobItem>().Property(item => item.ErrorMessage).HasMaxLength(1000);
        modelBuilder.Entity<FinalAccountsSyncJobItem>().Property(item => item.Revision).IsConcurrencyToken();

        modelBuilder.Entity<FinalAccountsSyncCheckpoint>().ToTable("fa_sync_checkpoints", "final_accounts");
        modelBuilder.Entity<FinalAccountsSyncCheckpoint>().HasIndex(item => new { item.CompanyId, item.StoreGroupId, item.StoreId, item.Module, item.CheckpointKey }).IsUnique();
        modelBuilder.Entity<FinalAccountsSyncCheckpoint>().HasIndex(item => new { item.CompanyId, item.StoreGroupId, item.StoreId, item.Module, item.UpdatedAt });
        modelBuilder.Entity<FinalAccountsSyncCheckpoint>().Property(item => item.Module).HasMaxLength(80);
        modelBuilder.Entity<FinalAccountsSyncCheckpoint>().Property(item => item.CheckpointKey).HasMaxLength(160);
        modelBuilder.Entity<FinalAccountsSyncCheckpoint>().Property(item => item.LastSourceHash).HasMaxLength(128);
        modelBuilder.Entity<FinalAccountsSyncCheckpoint>().Property(item => item.UpdatedBy).HasMaxLength(120);
        modelBuilder.Entity<FinalAccountsSyncCheckpoint>().Property(item => item.Revision).IsConcurrencyToken();

        modelBuilder.Entity<FinalAccountsSyncException>().ToTable("fa_sync_exceptions", "final_accounts");
        modelBuilder.Entity<FinalAccountsSyncException>().HasIndex(item => new { item.CompanyId, item.StoreGroupId, item.StoreId, item.Resolved, item.CreatedAt });
        modelBuilder.Entity<FinalAccountsSyncException>().HasIndex(item => new { item.JobId, item.JobItemId });
        modelBuilder.Entity<FinalAccountsSyncException>().HasIndex(item => new { item.SourceType, item.SourceId, item.Resolved });
        modelBuilder.Entity<FinalAccountsSyncException>().Property(item => item.SourceType).HasMaxLength(80);
        modelBuilder.Entity<FinalAccountsSyncException>().Property(item => item.Severity).HasMaxLength(24);
        modelBuilder.Entity<FinalAccountsSyncException>().Property(item => item.Category).HasMaxLength(80);
        modelBuilder.Entity<FinalAccountsSyncException>().Property(item => item.Code).HasMaxLength(80);
        modelBuilder.Entity<FinalAccountsSyncException>().Property(item => item.Message).HasMaxLength(1000);
        modelBuilder.Entity<FinalAccountsSyncException>().Property(item => item.ResolvedBy).HasMaxLength(120);
        modelBuilder.Entity<FinalAccountsSyncException>().Property(item => item.ResolutionNotes).HasMaxLength(500);
        modelBuilder.Entity<FinalAccountsSyncException>().Property(item => item.Revision).IsConcurrencyToken();
    }

    private static void ConfigureFinalAccountsCaWorkspace(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<FinalAccountsAdjustmentBatch>().ToTable("fa_ca_adjustment_batches", "final_accounts");
        modelBuilder.Entity<FinalAccountsAdjustmentBatch>().HasIndex(item => new { item.CompanyId, item.StoreGroupId, item.StoreId, item.BatchNumber }).IsUnique();
        modelBuilder.Entity<FinalAccountsAdjustmentBatch>().HasIndex(item => new { item.CompanyId, item.StoreGroupId, item.StoreId, item.Status, item.AdjustmentDate });
        modelBuilder.Entity<FinalAccountsAdjustmentBatch>().HasIndex(item => item.JournalEntryId);
        modelBuilder.Entity<FinalAccountsAdjustmentBatch>().HasIndex(item => item.ReversalJournalEntryId);
        modelBuilder.Entity<FinalAccountsAdjustmentBatch>().Property(item => item.BatchNumber).HasMaxLength(64);
        modelBuilder.Entity<FinalAccountsAdjustmentBatch>().Property(item => item.Title).HasMaxLength(160);
        modelBuilder.Entity<FinalAccountsAdjustmentBatch>().Property(item => item.Description).HasMaxLength(1000);
        modelBuilder.Entity<FinalAccountsAdjustmentBatch>().Property(item => item.ReferenceNumber).HasMaxLength(120);
        modelBuilder.Entity<FinalAccountsAdjustmentBatch>().Property(item => item.DecisionNotes).HasMaxLength(1000);
        modelBuilder.Entity<FinalAccountsAdjustmentBatch>().Property(item => item.SubmittedBy).HasMaxLength(120);
        modelBuilder.Entity<FinalAccountsAdjustmentBatch>().Property(item => item.ReviewedBy).HasMaxLength(120);
        modelBuilder.Entity<FinalAccountsAdjustmentBatch>().Property(item => item.ApprovedBy).HasMaxLength(120);
        modelBuilder.Entity<FinalAccountsAdjustmentBatch>().Property(item => item.RejectedBy).HasMaxLength(120);
        modelBuilder.Entity<FinalAccountsAdjustmentBatch>().Property(item => item.PostedBy).HasMaxLength(120);
        modelBuilder.Entity<FinalAccountsAdjustmentBatch>().Property(item => item.ReversedBy).HasMaxLength(120);
        modelBuilder.Entity<FinalAccountsAdjustmentBatch>().Property(item => item.CreatedBy).HasMaxLength(120);
        modelBuilder.Entity<FinalAccountsAdjustmentBatch>().Property(item => item.UpdatedBy).HasMaxLength(120);
        modelBuilder.Entity<FinalAccountsAdjustmentBatch>().Property(item => item.Revision).IsConcurrencyToken();

        modelBuilder.Entity<FinalAccountsAdjustmentLine>().ToTable("fa_ca_adjustment_lines", "final_accounts");
        modelBuilder.Entity<FinalAccountsAdjustmentLine>().HasIndex(item => new { item.AdjustmentBatchId, item.LineNumber }).IsUnique();
        modelBuilder.Entity<FinalAccountsAdjustmentLine>().HasIndex(item => new { item.CompanyId, item.StoreGroupId, item.StoreId, item.AccountId });
        modelBuilder.Entity<FinalAccountsAdjustmentLine>().Property(item => item.Debit).HasPrecision(18, 2);
        modelBuilder.Entity<FinalAccountsAdjustmentLine>().Property(item => item.Credit).HasPrecision(18, 2);
        modelBuilder.Entity<FinalAccountsAdjustmentLine>().Property(item => item.Narration).HasMaxLength(500);
        modelBuilder.Entity<FinalAccountsAdjustmentLine>().Property(item => item.StatementLineKey).HasMaxLength(120);
        modelBuilder.Entity<FinalAccountsAdjustmentLine>().Property(item => item.CreatedBy).HasMaxLength(120);
        modelBuilder.Entity<FinalAccountsAdjustmentLine>().Property(item => item.UpdatedBy).HasMaxLength(120);
        modelBuilder.Entity<FinalAccountsAdjustmentLine>().Property(item => item.Revision).IsConcurrencyToken();

        modelBuilder.Entity<FinalAccountsAdjustmentAttachment>().ToTable("fa_ca_adjustment_attachments", "final_accounts");
        modelBuilder.Entity<FinalAccountsAdjustmentAttachment>().HasIndex(item => new { item.AdjustmentBatchId, item.CreatedAt });
        modelBuilder.Entity<FinalAccountsAdjustmentAttachment>().Property(item => item.FileName).HasMaxLength(260);
        modelBuilder.Entity<FinalAccountsAdjustmentAttachment>().Property(item => item.ContentType).HasMaxLength(120);
        modelBuilder.Entity<FinalAccountsAdjustmentAttachment>().Property(item => item.StorageReference).HasMaxLength(500);
        modelBuilder.Entity<FinalAccountsAdjustmentAttachment>().Property(item => item.Notes).HasMaxLength(500);
        modelBuilder.Entity<FinalAccountsAdjustmentAttachment>().Property(item => item.UploadedBy).HasMaxLength(120);
        modelBuilder.Entity<FinalAccountsAdjustmentAttachment>().Property(item => item.Revision).IsConcurrencyToken();

        modelBuilder.Entity<FinalAccountsAdjustmentComment>().ToTable("fa_ca_adjustment_comments", "final_accounts");
        modelBuilder.Entity<FinalAccountsAdjustmentComment>().HasIndex(item => new { item.AdjustmentBatchId, item.CreatedAt });
        modelBuilder.Entity<FinalAccountsAdjustmentComment>().Property(item => item.Body).HasMaxLength(2000);
        modelBuilder.Entity<FinalAccountsAdjustmentComment>().Property(item => item.Visibility).HasMaxLength(40);
        modelBuilder.Entity<FinalAccountsAdjustmentComment>().Property(item => item.CreatedBy).HasMaxLength(120);
        modelBuilder.Entity<FinalAccountsAdjustmentComment>().Property(item => item.Revision).IsConcurrencyToken();

        modelBuilder.Entity<FinalAccountsStatementLineComment>().ToTable("fa_statement_line_comments", "final_accounts");
        modelBuilder.Entity<FinalAccountsStatementLineComment>().HasIndex(item => new { item.CompanyId, item.StoreGroupId, item.StoreId, item.StatementType, item.StatementLineKey, item.ReportVersion });
        modelBuilder.Entity<FinalAccountsStatementLineComment>().Property(item => item.StatementType).HasMaxLength(80);
        modelBuilder.Entity<FinalAccountsStatementLineComment>().Property(item => item.StatementLineKey).HasMaxLength(120);
        modelBuilder.Entity<FinalAccountsStatementLineComment>().Property(item => item.Body).HasMaxLength(2000);
        modelBuilder.Entity<FinalAccountsStatementLineComment>().Property(item => item.CreatedBy).HasMaxLength(120);
        modelBuilder.Entity<FinalAccountsStatementLineComment>().Property(item => item.UpdatedBy).HasMaxLength(120);
        modelBuilder.Entity<FinalAccountsStatementLineComment>().Property(item => item.Revision).IsConcurrencyToken();

        modelBuilder.Entity<FinalAccountsReportVersion>().ToTable("fa_report_versions", "final_accounts");
        modelBuilder.Entity<FinalAccountsReportVersion>().HasIndex(item => new { item.CompanyId, item.StoreGroupId, item.StoreId, item.ReportType, item.VersionKind, item.GeneratedAt });
        modelBuilder.Entity<FinalAccountsReportVersion>().Property(item => item.ReportType).HasMaxLength(80);
        modelBuilder.Entity<FinalAccountsReportVersion>().Property(item => item.GeneratedBy).HasMaxLength(120);
        modelBuilder.Entity<FinalAccountsReportVersion>().Property(item => item.Notes).HasMaxLength(500);
        modelBuilder.Entity<FinalAccountsReportVersion>().Property(item => item.Revision).IsConcurrencyToken();
    }

    private static void ConfigureFinalAccountsPeriodClose(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<FinalAccountsCloseRun>().ToTable("fa_close_runs", "final_accounts");
        modelBuilder.Entity<FinalAccountsCloseRun>().HasIndex(item => new { item.CompanyId, item.StoreGroupId, item.StoreId, item.RunNumber }).IsUnique();
        modelBuilder.Entity<FinalAccountsCloseRun>().HasIndex(item => new { item.CompanyId, item.StoreGroupId, item.StoreId, item.FiscalYearId, item.FiscalPeriodId, item.CloseType, item.Status });
        modelBuilder.Entity<FinalAccountsCloseRun>().HasIndex(item => item.FinancialYearLockId);
        modelBuilder.Entity<FinalAccountsCloseRun>().Property(item => item.RunNumber).HasMaxLength(64);
        modelBuilder.Entity<FinalAccountsCloseRun>().Property(item => item.ChecklistStatus).HasMaxLength(32);
        modelBuilder.Entity<FinalAccountsCloseRun>().Property(item => item.ReconciliationStatus).HasMaxLength(32);
        modelBuilder.Entity<FinalAccountsCloseRun>().Property(item => item.TrialBalanceStatus).HasMaxLength(32);
        modelBuilder.Entity<FinalAccountsCloseRun>().Property(item => item.BalanceSheetStatus).HasMaxLength(32);
        modelBuilder.Entity<FinalAccountsCloseRun>().Property(item => item.InventorySnapshotTotal).HasPrecision(18, 2);
        modelBuilder.Entity<FinalAccountsCloseRun>().Property(item => item.ProfitAfterTax).HasPrecision(18, 2);
        modelBuilder.Entity<FinalAccountsCloseRun>().Property(item => item.CurrentYearResultTransferStatus).HasMaxLength(32);
        modelBuilder.Entity<FinalAccountsCloseRun>().Property(item => item.OpeningJournalStatus).HasMaxLength(32);
        modelBuilder.Entity<FinalAccountsCloseRun>().Property(item => item.ReportSnapshotStatus).HasMaxLength(32);
        modelBuilder.Entity<FinalAccountsCloseRun>().Property(item => item.ReopenReason).HasMaxLength(1000);
        modelBuilder.Entity<FinalAccountsCloseRun>().Property(item => item.ApprovalNotes).HasMaxLength(1000);
        modelBuilder.Entity<FinalAccountsCloseRun>().Property(item => item.ClosedBy).HasMaxLength(120);
        modelBuilder.Entity<FinalAccountsCloseRun>().Property(item => item.ReopenRequestedBy).HasMaxLength(120);
        modelBuilder.Entity<FinalAccountsCloseRun>().Property(item => item.ReopenedBy).HasMaxLength(120);
        modelBuilder.Entity<FinalAccountsCloseRun>().Property(item => item.CreatedBy).HasMaxLength(120);
        modelBuilder.Entity<FinalAccountsCloseRun>().Property(item => item.UpdatedBy).HasMaxLength(120);
        modelBuilder.Entity<FinalAccountsCloseRun>().Property(item => item.Revision).IsConcurrencyToken();

        modelBuilder.Entity<FinalAccountsCloseChecklistItem>().ToTable("fa_close_checklist_items", "final_accounts");
        modelBuilder.Entity<FinalAccountsCloseChecklistItem>().HasIndex(item => new { item.CloseRunId, item.Key }).IsUnique();
        modelBuilder.Entity<FinalAccountsCloseChecklistItem>().Property(item => item.Key).HasMaxLength(80);
        modelBuilder.Entity<FinalAccountsCloseChecklistItem>().Property(item => item.Label).HasMaxLength(160);
        modelBuilder.Entity<FinalAccountsCloseChecklistItem>().Property(item => item.Status).HasMaxLength(32);
        modelBuilder.Entity<FinalAccountsCloseChecklistItem>().Property(item => item.Detail).HasMaxLength(1000);
        modelBuilder.Entity<FinalAccountsCloseChecklistItem>().Property(item => item.Amount).HasPrecision(18, 2);
        modelBuilder.Entity<FinalAccountsCloseChecklistItem>().Property(item => item.Revision).IsConcurrencyToken();

        modelBuilder.Entity<FinalAccountsCloseReportSnapshot>().ToTable("fa_close_report_snapshots", "final_accounts");
        modelBuilder.Entity<FinalAccountsCloseReportSnapshot>().HasIndex(item => new { item.CloseRunId, item.ReportType }).IsUnique();
        modelBuilder.Entity<FinalAccountsCloseReportSnapshot>().Property(item => item.ReportType).HasMaxLength(80);
        modelBuilder.Entity<FinalAccountsCloseReportSnapshot>().Property(item => item.Status).HasMaxLength(32);
        modelBuilder.Entity<FinalAccountsCloseReportSnapshot>().Property(item => item.PayloadHash).HasMaxLength(128);
        modelBuilder.Entity<FinalAccountsCloseReportSnapshot>().Property(item => item.Revision).IsConcurrencyToken();

        modelBuilder.Entity<FinalAccountsCloseBalanceSnapshot>().ToTable("fa_close_balance_snapshots", "final_accounts");
        modelBuilder.Entity<FinalAccountsCloseBalanceSnapshot>().HasIndex(item => new { item.CloseRunId, item.AccountId }).IsUnique();
        modelBuilder.Entity<FinalAccountsCloseBalanceSnapshot>().Property(item => item.AccountCode).HasMaxLength(40);
        modelBuilder.Entity<FinalAccountsCloseBalanceSnapshot>().Property(item => item.AccountName).HasMaxLength(160);
        modelBuilder.Entity<FinalAccountsCloseBalanceSnapshot>().Property(item => item.AccountType).HasMaxLength(40);
        modelBuilder.Entity<FinalAccountsCloseBalanceSnapshot>().Property(item => item.ClosingBalance).HasPrecision(18, 2);
        modelBuilder.Entity<FinalAccountsCloseBalanceSnapshot>().Property(item => item.OpeningBalance).HasPrecision(18, 2);
        modelBuilder.Entity<FinalAccountsCloseBalanceSnapshot>().Property(item => item.Revision).IsConcurrencyToken();
    }

    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        PrepareEntitiesForSave();
        await ValidateFinancialYearLocksAsync(cancellationToken);
        ValidateChangedJournalLines();
        AddAuditLogEntries();
        AddDotMatrixPrintQueueEntries();
        return await base.SaveChangesAsync(cancellationToken);
    }

    public override int SaveChanges()
    {
        PrepareEntitiesForSave();
        ValidateFinancialYearLocks();
        ValidateChangedJournalLines();
        AddAuditLogEntries();
        AddDotMatrixPrintQueueEntries();
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


        if (entityName.StartsWith("Attendance", StringComparison.Ordinal) || entityName is "EmployeeBiometricEnrollment")
        {
            return "HR";
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



    private void AddDotMatrixPrintQueueEntries()
    {
        var queueEntries = BuildDotMatrixPrintQueueEntries().ToList();
        if (queueEntries.Count == 0)
        {
            return;
        }

        DotMatrixPrintQueueEntries.AddRange(queueEntries);
    }

    private IEnumerable<DotMatrixPrintQueueEntry> BuildDotMatrixPrintQueueEntries()
    {
        var nowUtc = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified);
        var nowIst = ConvertUtcToIst(nowUtc);
        var candidates = ChangeTracker.Entries()
            .Where(entry => entry.State is EntityState.Added or EntityState.Modified or EntityState.Deleted)
            .Where(entry => entry.Entity is not AuditLogEntry)
            .Where(entry => entry.Entity is not DotMatrixPrintQueueEntry)
            .Where(entry => entry.Entity is not DotMatrixPrintSetting)
            .Where(entry => entry.Metadata.ClrType.Namespace?.StartsWith("Microsoft.", StringComparison.Ordinal) != true)
            .ToList();

        if (candidates.Count == 0)
        {
            yield break;
        }

        var storeIds = candidates
            .Select(ResolveDotMatrixStoreId)
            .Where(storeId => storeId.HasValue && storeId.Value != Guid.Empty)
            .Select(storeId => storeId!.Value)
            .Distinct()
            .ToArray();
        var storeContexts = ResolveDotMatrixStorePrintContexts(storeIds);
        if (storeContexts.Count == 0)
        {
            yield break;
        }

        var sequenceNo = nowUtc.Ticks;
        var queuedKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var entry in candidates)
        {
            if (!TryBuildDotMatrixPrintLine(entry, nowUtc, nowIst, sequenceNo++, storeContexts, out var queueEntry))
            {
                continue;
            }

            if (!string.IsNullOrWhiteSpace(queueEntry!.DeduplicationKey))
            {
                if (!queuedKeys.Add(queueEntry.DeduplicationKey) || DotMatrixDeduplicationKeyExists(queueEntry.DeduplicationKey))
                {
                    continue;
                }
            }

            yield return queueEntry!;
        }
    }

    private IReadOnlyDictionary<Guid, DotMatrixStorePrintContext> ResolveDotMatrixStorePrintContexts(IReadOnlyCollection<Guid> storeIds)
    {
        if (storeIds.Count == 0)
        {
            return new Dictionary<Guid, DotMatrixStorePrintContext>();
        }

        try
        {
            var stores = Stores.AsNoTracking()
                .Where(store => storeIds.Contains(store.Id))
                .Select(store => new { store.Id, store.CompanyId, store.StoreGroupId, store.Name, store.StoreCode })
                .ToList()
                .ToDictionary(store => store.Id);
            var settings = DotMatrixPrintSettings.AsNoTracking()
                .Where(setting => storeIds.Contains(setting.StoreId) && !setting.Deleted)
                .Select(setting => new
                {
                    setting.StoreId,
                    setting.Enabled,
                    setting.PrinterName,
                    setting.LineWidth,
                    setting.PrintTransactions,
                    setting.PrintEditsAndDeletes,
                    setting.PrintDayOpeningClosing,
                    setting.PrintAttendanceInDaySummary,
                    setting.PrintBankUpiSummary,
                    setting.TimeZoneId
                })
                .ToList()
                .GroupBy(setting => setting.StoreId)
                .ToDictionary(group => group.Key, group => group.OrderByDescending(setting => setting.Enabled).First());

            var result = new Dictionary<Guid, DotMatrixStorePrintContext>();
            foreach (var storeId in storeIds)
            {
                if (!stores.TryGetValue(storeId, out var store))
                {
                    continue;
                }

                settings.TryGetValue(storeId, out var setting);
                var storeCode = FirstNonBlank(store.StoreCode, store.Name, store.Id.ToString("N")[..6]).Trim();
                result[storeId] = new DotMatrixStorePrintContext(
                    storeId,
                    store.CompanyId,
                    store.StoreGroupId,
                    TruncatePlain(storeCode, 12),
                    TruncatePlain(FirstNonBlank(store.Name, storeCode), 80),
                    FirstNonBlank(setting?.PrinterName, "EPSON_LX810"),
                    Math.Clamp(setting?.LineWidth ?? 136, 80, 136),
                    setting?.Enabled ?? false,
                    setting?.PrintTransactions ?? true,
                    setting?.PrintEditsAndDeletes ?? true,
                    setting?.PrintDayOpeningClosing ?? true,
                    setting?.PrintAttendanceInDaySummary ?? true,
                    setting?.PrintBankUpiSummary ?? true,
                    FirstNonBlank(setting?.TimeZoneId, "Asia/Kolkata"));
            }

            return result;
        }
        catch
        {
            // DotMatrix tables may not exist yet during first schema repair or migration bootstrap.
            // In that case, do not block the business transaction; simply do not queue hardware prints.
            return new Dictionary<Guid, DotMatrixStorePrintContext>();
        }
    }

    private bool DotMatrixDeduplicationKeyExists(string deduplicationKey)
    {
        try
        {
            return DotMatrixPrintQueueEntries.AsNoTracking().Any(item => item.DeduplicationKey == deduplicationKey && !item.Deleted);
        }
        catch
        {
            return false;
        }
    }

    private bool TryBuildDotMatrixPrintLine(EntityEntry entry, DateTime nowUtc, DateTime nowIst, long sequenceNo, IReadOnlyDictionary<Guid, DotMatrixStorePrintContext> storeContexts, out DotMatrixPrintQueueEntry? queueEntry)
    {
        queueEntry = null;
        var entityName = entry.Metadata.ClrType.Name;
        var action = ResolveDotMatrixAction(entry);
        if (action is null)
        {
            return false;
        }

        var sourceType = DotMatrixSourceType(entityName);
        if (sourceType is null)
        {
            return false;
        }

        if (action == "Edit" && !HasMeaningfulDotMatrixChanges(entry))
        {
            return false;
        }

        if (entityName == nameof(CashDetail))
        {
            var source = ReadStringProperty(entry, nameof(CashDetail.CreatedBy));
            if (source is "DayOpening" or "DayClosing" or "StoreHoliday")
            {
                return false;
            }
        }

        var storeId = ResolveDotMatrixStoreId(entry) ?? Guid.Empty;
        if (storeId == Guid.Empty || !storeContexts.TryGetValue(storeId, out var storeContext) || !storeContext.Enabled)
        {
            return false;
        }

        var eventType = action == "Create" ? "Transaction" : "AuditMutation";
        if (!IsDotMatrixEventEnabled(eventType, storeContext))
        {
            return false;
        }

        var entityId = ReadEntityId(entry);
        if (!entityId.HasValue)
        {
            return false;
        }

        var businessDate = (ReadDateProperty(entry, "OnDate", "InwardDate", "TransactionDate", "LocalPunchTime") ?? nowIst).Date;
        var printTime = nowIst;
        var sourceNumber = ResolveDotMatrixSourceNumber(entry, entityName, entityId.Value);
        var party = ResolveDotMatrixParty(entry, entityName);
        var mode = ResolveDotMatrixPaymentMode(entry, entityName);
        var amount = ReadDecimalProperty(entry, "BillAmount", "Amount", "ClosingBalance", "OpeningBalance") ?? 0m;
        var particulars = BuildDotMatrixParticulars(entry, entityName, sourceNumber, party);
        var changes = BuildDotMatrixChangeSummary(entry);
        var reason = ResolveAuditReason(entry);
        var lineWidth = storeContext.LineWidth;
        var userName = FirstNonBlank(auditActorContext?.UserName, ReadStringProperty(entry, nameof(CompanyBase.CreatedBy)), "System");
        var header = action switch
        {
            "Edit" => FullLine(lineWidth, $"EDITED RECORD BELOW - {FormatIst(nowIst)} - User: {userName}"),
            "Delete" => FullLine(lineWidth, $"DELETED / CANCELLED RECORD BELOW - {FormatIst(nowIst)} - User: {userName}"),
            _ => string.Empty
        };
        var reasonLine = string.IsNullOrWhiteSpace(reason) || action == "Create" ? string.Empty : FullLine(lineWidth, $"Reason/Remark: {reason}");
        var line = FixedTransactionLine(lineWidth, printTime, storeContext.StoreCode, sourceType, action, sourceNumber, particulars, amount, mode);
        var printable = string.IsNullOrEmpty(header)
            ? line
            : string.Join(Environment.NewLine, new[] { Repeat('=', lineWidth), header, reasonLine, line, changes, Repeat('-', lineWidth) }.Where(x => !string.IsNullOrWhiteSpace(x)));
        var deduplicationKey = BuildDotMatrixDeduplicationKey(sourceType, entityId.Value, action, entry);

        queueEntry = new DotMatrixPrintQueueEntry
        {
            Id = Guid.NewGuid(),
            CreatedAt = nowUtc,
            UpdatedAt = nowUtc,
            CompanyId = ReadGuidProperty(entry, nameof(CompanyBase.CompanyId)) ?? storeContext.CompanyId,
            StoreGroupId = ReadGuidProperty(entry, nameof(StoreBase.StoreGroupId)) ?? storeContext.StoreGroupId,
            StoreId = storeId,
            BusinessDate = businessDate,
            OperationTimeUtc = nowUtc,
            EventType = eventType,
            ActionType = action,
            SourceType = sourceType,
            SourceId = entityId.Value,
            SourceNumber = sourceNumber,
            PartyName = TruncatePlain(party, 120),
            PaymentMode = TruncatePlain(mode, 120),
            Amount = Math.Round(amount, 2),
            SequenceNo = sequenceNo,
            LineWidth = lineWidth,
            PrinterName = storeContext.PrinterName,
            Status = "Pending",
            PrintableText = printable.TrimEnd() + Environment.NewLine,
            CreatedBy = userName,
            DeduplicationKey = deduplicationKey
        };
        return true;
    }

    private static Guid? ResolveDotMatrixStoreId(EntityEntry entry)
        => ReadGuidProperty(entry, nameof(StoreBase.StoreId)) ?? ReadGuidProperty(entry, nameof(Invoice.StoreId));

    private static bool IsDotMatrixEventEnabled(string eventType, DotMatrixStorePrintContext storeContext)
        => eventType switch
        {
            "Transaction" => storeContext.PrintTransactions,
            "AuditMutation" => storeContext.PrintEditsAndDeletes,
            "DayOpening" or "DayClosing" => storeContext.PrintDayOpeningClosing,
            _ => true
        };

    private static string? ResolveDotMatrixAction(EntityEntry entry)
    {
        if (entry.State == EntityState.Added)
        {
            return "Create";
        }

        if (entry.State == EntityState.Deleted)
        {
            return "Delete";
        }

        if (entry.State == EntityState.Modified)
        {
            var deletedProperty = entry.Properties.FirstOrDefault(property => property.Metadata.Name == nameof(BaseEntity.Deleted));
            if (deletedProperty is not null
                && deletedProperty.IsModified
                && deletedProperty.CurrentValue is bool currentDeleted
                && currentDeleted)
            {
                return "Delete";
            }

            return "Edit";
        }

        return null;
    }

    private static string? DotMatrixSourceType(string entityName) => entityName switch
    {
        nameof(Invoice) => "SALE",
        nameof(PurchaseInvoice) => "PURCHASE",
        nameof(Voucher) => "VOUCHER",
        nameof(CashVoucher) => "CASH VOUCHER",
        nameof(InvoicePayment) => "CUSTOMER RECEIPT",
        nameof(PurchasePayment) => "VENDOR PAYMENT",
        nameof(CustomerAdvanceReceipt) => "CUSTOMER ADVANCE",
        nameof(CommercialNote) => "COMMERCIAL NOTE",
        nameof(BankCashTranscation) => "BANK/CASH",
        nameof(CashDetail) => "CASH DETAIL",
        nameof(AttendancePunch) => "ATTENDANCE PUNCH",
        _ => null
    };

    private static bool HasMeaningfulDotMatrixChanges(EntityEntry entry)
        => entry.Properties.Any(property => property.IsModified && !IsDotMatrixIgnoredProperty(property.Metadata.Name));

    private static bool IsDotMatrixIgnoredProperty(string propertyName)
        => propertyName is nameof(BaseEntity.UpdatedAt) or nameof(BaseEntity.Synced) or "LastSeenAtUtc";

    private string BuildDotMatrixParticulars(EntityEntry entry, string entityName, string sourceNumber, string party)
    {
        return entityName switch
        {
            nameof(Invoice) => $"Inv {sourceNumber} Qty:{ReadDecimalProperty(entry, nameof(BaseInvoice.Quantity)) ?? 0:0.##} {party} Mob:{ReadStringProperty(entry, nameof(Invoice.CustomerMobileNumber)) ?? "-"}",
            nameof(PurchaseInvoice) => $"Inw {sourceNumber} SupInv:{ReadStringProperty(entry, nameof(BaseInvoice.InvoiceNumber)) ?? "-"} Vendor:{party} Qty:{ReadDecimalProperty(entry, nameof(BaseInvoice.Quantity)) ?? 0:0.##}",
            nameof(Voucher) => $"{ReadStringProperty(entry, nameof(Voucher.VoucherType)) ?? "Voucher"} {party} {ReadStringProperty(entry, nameof(Voucher.Particulars)) ?? string.Empty}",
            nameof(CashVoucher) => $"{ReadStringProperty(entry, nameof(CashVoucher.VoucherType)) ?? "Cash"} {party} {ReadStringProperty(entry, nameof(CashVoucher.Particulars)) ?? string.Empty}",
            nameof(InvoicePayment) => BuildInvoicePaymentParticulars(entry),
            nameof(PurchasePayment) => BuildPurchasePaymentParticulars(entry),
            nameof(CustomerAdvanceReceipt) => $"Advance receipt {sourceNumber} {party}",
            nameof(CommercialNote) => $"{ReadStringProperty(entry, "NoteType") ?? "Note"} {sourceNumber} {party} {ReadStringProperty(entry, "Reason") ?? string.Empty}",
            nameof(BankCashTranscation) => $"{ReadStringProperty(entry, "TransactionType") ?? "Bank/Cash"} {ReadStringProperty(entry, "Remarks") ?? string.Empty}",
            nameof(CashDetail) => $"Manual cash detail {ReadStringProperty(entry, nameof(CompanyBase.CreatedBy)) ?? string.Empty}",
            nameof(AttendancePunch) => BuildAttendancePunchParticulars(entry),
            _ => party
        };
    }

    private string BuildInvoicePaymentParticulars(EntityEntry entry)
    {
        var invoiceId = ReadGuidProperty(entry, nameof(InvoicePayment.InvoiceId));
        var invoice = invoiceId.HasValue ? FindInvoiceSnapshot(invoiceId.Value) : null;
        var number = FirstNonBlank(invoice?.InvoiceNumber, ShortGuid(invoiceId), "-");
        var customer = FirstNonBlank(invoice?.CustomerName, invoice?.CustomerMobileNumber, "Customer");
        return $"Receipt for Inv:{number} {customer} Ref:{ReadStringProperty(entry, nameof(InvoicePayment.ReferenceNumber)) ?? "-"}";
    }

    private string BuildPurchasePaymentParticulars(EntityEntry entry)
    {
        var purchaseId = ReadGuidProperty(entry, nameof(PurchasePayment.PurchaseInvoiceId));
        var purchase = purchaseId.HasValue ? FindPurchaseInvoiceSnapshot(purchaseId.Value) : null;
        var vendorId = ReadGuidProperty(entry, nameof(PurchasePayment.VendorId));
        var vendor = FirstNonBlank(purchase?.VendorName, vendorId.HasValue ? FindVendorName(vendorId.Value) : null, "Vendor");
        var number = FirstNonBlank(purchase?.InwardNumber, purchase?.InvoiceNumber, ShortGuid(purchaseId), "-");
        return $"Vendor payment Inw:{number} {vendor} Ref:{ReadStringProperty(entry, nameof(PurchasePayment.ReferenceNumber)) ?? ReadStringProperty(entry, nameof(PurchasePayment.Remarks)) ?? "-"}";
    }

    private string BuildAttendancePunchParticulars(EntityEntry entry)
    {
        var employeeId = ReadGuidProperty(entry, nameof(AttendancePunch.EmployeeId));
        var employeeName = employeeId.HasValue ? FindEmployeeName(employeeId.Value) : null;
        var punchType = ReadStringProperty(entry, nameof(AttendancePunch.PunchType)) ?? "Punch";
        var punchTime = ReadDateProperty(entry, nameof(AttendancePunch.LocalPunchTime));
        return $"Employee:{FirstNonBlank(employeeName, ShortGuid(employeeId), "-")} {punchType} {punchTime?.ToString("hh:mm tt", CultureInfo.InvariantCulture) ?? string.Empty}";
    }

    private string ResolveDotMatrixSourceNumber(EntityEntry entry, string entityName, Guid entityId)
    {
        if (entityName == nameof(PurchaseInvoice))
        {
            return FirstNonBlank(ReadStringProperty(entry, nameof(PurchaseInvoice.InwardNumber)), ReadStringProperty(entry, nameof(BaseInvoice.InvoiceNumber)), entityId.ToString("N")[..10]);
        }

        if (entityName == nameof(InvoicePayment))
        {
            return FirstNonBlank(ReadStringProperty(entry, nameof(InvoicePayment.ReferenceNumber)), FindInvoiceSnapshot(ReadGuidProperty(entry, nameof(InvoicePayment.InvoiceId)) ?? Guid.Empty)?.InvoiceNumber, entityId.ToString("N")[..10]);
        }

        if (entityName == nameof(PurchasePayment))
        {
            return FirstNonBlank(ReadStringProperty(entry, nameof(PurchasePayment.ReferenceNumber)), FindPurchaseInvoiceSnapshot(ReadGuidProperty(entry, nameof(PurchasePayment.PurchaseInvoiceId)) ?? Guid.Empty)?.InwardNumber, entityId.ToString("N")[..10]);
        }

        return FirstNonBlank(
            ReadStringProperty(entry, nameof(BaseInvoice.InvoiceNumber)),
            ReadStringProperty(entry, nameof(PurchaseInvoice.InwardNumber)),
            ReadStringProperty(entry, nameof(Voucher.VoucherNumber)),
            ReadStringProperty(entry, "ReceiptNumber"),
            ReadStringProperty(entry, "NoteNumber"),
            ReadStringProperty(entry, nameof(InvoicePayment.ReferenceNumber)),
            entityId.ToString("N")[..10]);
    }

    private string ResolveDotMatrixParty(EntityEntry entry, string entityName)
    {
        if (entityName == nameof(InvoicePayment))
        {
            var invoice = FindInvoiceSnapshot(ReadGuidProperty(entry, nameof(InvoicePayment.InvoiceId)) ?? Guid.Empty);
            return FirstNonBlank(invoice?.CustomerName, invoice?.CustomerMobileNumber, "Customer");
        }

        if (entityName == nameof(PurchasePayment))
        {
            var purchase = FindPurchaseInvoiceSnapshot(ReadGuidProperty(entry, nameof(PurchasePayment.PurchaseInvoiceId)) ?? Guid.Empty);
            var vendorId = ReadGuidProperty(entry, nameof(PurchasePayment.VendorId));
            return FirstNonBlank(purchase?.VendorName, vendorId.HasValue ? FindVendorName(vendorId.Value) : null, "Vendor");
        }

        return FirstNonBlank(
            ReadStringProperty(entry, nameof(Invoice.CustomerName)),
            ReadStringProperty(entry, nameof(PurchaseInvoice.VendorName)),
            ReadStringProperty(entry, nameof(Voucher.PartyName)),
            ReadStringProperty(entry, nameof(Invoice.CustomerMobileNumber)),
            ReadStringProperty(entry, nameof(Voucher.Remarks)),
            "-");
    }

    private string ResolveDotMatrixPaymentMode(EntityEntry entry, string entityName)
    {
        var mode = FirstNonBlank(ReadStringProperty(entry, nameof(Invoice.PaymentMode)), entityName == nameof(CashVoucher) ? "Cash" : null, "-");
        var bankAccountId = ReadGuidProperty(entry, nameof(InvoicePayment.BankAccountId)) ?? ReadGuidProperty(entry, nameof(PurchasePayment.BankAccountId)) ?? ReadGuidProperty(entry, "AccountNumber");
        var bank = bankAccountId.HasValue ? FindBankAccountLabel(bankAccountId.Value) : null;
        return string.IsNullOrWhiteSpace(bank) ? mode : $"{mode}/{bank}";
    }

    private DotMatrixInvoiceSnapshot? FindInvoiceSnapshot(Guid id)
    {
        if (id == Guid.Empty) return null;
        var local = ChangeTracker.Entries<Invoice>().FirstOrDefault(entry => entry.Entity.Id == id)?.Entity;
        if (local is not null)
        {
            return new DotMatrixInvoiceSnapshot(local.InvoiceNumber, local.CustomerName, local.CustomerMobileNumber);
        }

        return SalesInvoices.AsNoTracking()
            .Where(invoice => invoice.Id == id)
            .Select(invoice => new DotMatrixInvoiceSnapshot(invoice.InvoiceNumber, invoice.CustomerName, invoice.CustomerMobileNumber))
            .FirstOrDefault();
    }

    private DotMatrixPurchaseInvoiceSnapshot? FindPurchaseInvoiceSnapshot(Guid id)
    {
        if (id == Guid.Empty) return null;
        var local = ChangeTracker.Entries<PurchaseInvoice>().FirstOrDefault(entry => entry.Entity.Id == id)?.Entity;
        if (local is not null)
        {
            return new DotMatrixPurchaseInvoiceSnapshot(local.InwardNumber, local.InvoiceNumber, local.VendorName);
        }

        return PurchaseInvoices.AsNoTracking()
            .Where(invoice => invoice.Id == id)
            .Select(invoice => new DotMatrixPurchaseInvoiceSnapshot(invoice.InwardNumber, invoice.InvoiceNumber, invoice.VendorName))
            .FirstOrDefault();
    }

    private string? FindVendorName(Guid id)
    {
        if (id == Guid.Empty) return null;
        var local = ChangeTracker.Entries<Vendor>().FirstOrDefault(entry => entry.Entity.Id == id)?.Entity;
        if (local is not null) return local.Name;
        return Vendors.AsNoTracking().Where(vendor => vendor.Id == id).Select(vendor => vendor.Name).FirstOrDefault();
    }

    private string? FindEmployeeName(Guid id)
    {
        if (id == Guid.Empty) return null;
        var local = ChangeTracker.Entries<Employee>().FirstOrDefault(entry => entry.Entity.Id == id)?.Entity;
        if (local is not null) return FirstNonBlank($"{local.EmployeeCode} {local.FirstName} {local.LastName}", local.FirstName, local.LastName);
        return Employees.AsNoTracking()
            .Where(employee => employee.Id == id)
            .Select(employee => (employee.EmployeeCode + " " + employee.FirstName + " " + employee.LastName).Trim())
            .FirstOrDefault();
    }

    private string? FindBankAccountLabel(Guid id)
    {
        if (id == Guid.Empty) return null;
        var bank = BankAccounts.AsNoTracking()
            .Where(item => item.Id == id)
            .Select(item => new { item.AccountHolderName, item.AccountNumber })
            .FirstOrDefault();
        return bank is null ? null : FirstNonBlank(bank.AccountHolderName, bank.AccountNumber);
    }

    private static string? ShortGuid(Guid? value)
        => value.HasValue && value.Value != Guid.Empty ? value.Value.ToString("N")[..10] : null;

    private static string BuildDotMatrixDeduplicationKey(string sourceType, Guid sourceId, string action, EntityEntry entry)
        => TruncatePlain($"{sourceType}:{sourceId:N}:{action}:{BuildDotMatrixChangeHash(entry)}", 240);

    private static string BuildDotMatrixChangeHash(EntityEntry entry)
    {
        var payload = entry.State switch
        {
            EntityState.Added => "CREATE",
            EntityState.Deleted => "DELETE:" + string.Join("|", entry.Properties.Where(property => !IsDotMatrixIgnoredProperty(property.Metadata.Name)).OrderBy(property => property.Metadata.Name).Select(property => $"{property.Metadata.Name}={FormatAuditValue(property.OriginalValue)}")),
            _ => string.Join("|", entry.Properties.Where(property => property.IsModified && !IsDotMatrixIgnoredProperty(property.Metadata.Name)).OrderBy(property => property.Metadata.Name).Select(property => $"{property.Metadata.Name}:{FormatAuditValue(property.OriginalValue)}->{FormatAuditValue(property.CurrentValue)}"))
        };
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(payload));
        return Convert.ToHexString(hash)[..16];
    }

    private sealed record DotMatrixInvoiceSnapshot(string InvoiceNumber, string? CustomerName, string CustomerMobileNumber);
    private sealed record DotMatrixPurchaseInvoiceSnapshot(string InwardNumber, string InvoiceNumber, string? VendorName);

    private static string BuildDotMatrixChangeSummary(EntityEntry entry)
    {
        if (entry.State != EntityState.Modified)
        {
            return string.Empty;
        }

        var changes = entry.Properties
            .Where(property => property.IsModified && !IsDotMatrixIgnoredProperty(property.Metadata.Name))
            .Take(8)
            .Select(property => $"{property.Metadata.Name}: {FormatAuditValue(property.OriginalValue) ?? ""} -> {FormatAuditValue(property.CurrentValue) ?? ""}")
            .ToList();
        return changes.Count == 0 ? string.Empty : "Changed: " + string.Join(" | ", changes);
    }

    private static decimal? ReadDecimalProperty(EntityEntry entry, params string[] propertyNames)
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
            if (value is decimal decimalValue)
            {
                return decimalValue;
            }

            if (value is int intValue)
            {
                return intValue;
            }
        }

        return null;
    }

    private static DateTime ConvertUtcToIst(DateTime utc)
    {
        try
        {
            var tz = TimeZoneInfo.FindSystemTimeZoneById("Asia/Kolkata");
            return TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), tz);
        }
        catch
        {
            return DateTime.SpecifyKind(utc, DateTimeKind.Unspecified).AddHours(5).AddMinutes(30);
        }
    }

    private static string FormatIst(DateTime value) => value.ToString("dd-MM-yyyy hh:mm tt 'IST'", CultureInfo.InvariantCulture);

    private static string FixedTransactionLine(int width, DateTime onDate, string storeCode, string type, string action, string number, string particulars, decimal amount, string mode)
    {
        var typeText = action == "Create" ? type : $"{type} {action.ToUpperInvariant()}";
        var storeText = FirstNonBlank(storeCode, "STORE").ToUpperInvariant();
        var storeWidth = width >= 120 ? 10 : 6;
        var typeWidth = width >= 120 ? 18 : 14;
        var numberWidth = width >= 120 ? 16 : 12;
        var amountWidth = width >= 120 ? 14 : 12;
        var modeWidth = width >= 120 ? 18 : 12;
        var left = $"{onDate:dd-MM-yyyy hh:mm tt} "
            + TruncatePlain(storeText, storeWidth).PadRight(storeWidth) + " "
            + TruncatePlain(typeText, typeWidth).PadRight(typeWidth) + " "
            + TruncatePlain(number, numberWidth).PadRight(numberWidth) + " ";
        var amountText = amount.ToString("0.00", CultureInfo.InvariantCulture).PadLeft(amountWidth);
        var modeText = TruncatePlain(mode, modeWidth).PadRight(modeWidth);
        var particularsWidth = Math.Max(10, width - left.Length - amountText.Length - modeText.Length - 2);
        return left + TruncatePlain(particulars, particularsWidth).PadRight(particularsWidth) + " " + amountText + " " + modeText;
    }

    private static string FullLine(int width, string value)
        => TruncatePlain(value, width).PadRight(width);

    private static string Repeat(char ch, int count) => new(ch, Math.Max(1, count));

    private static string TruncatePlain(string? value, int width)
    {
        var clean = string.IsNullOrWhiteSpace(value) ? string.Empty : value.Replace("\r", " ").Replace("\n", " ").Trim();
        return clean.Length <= width ? clean : clean[..Math.Max(0, width - 1)] + "…";
    }

    private void AddAuditLogEntries()
    {
        var auditEntries = BuildAuditLogEntries().ToList();
        if (auditEntries.Count == 0)
        {
            return;
        }

        AuditLogEntries.AddRange(auditEntries);
    }

    private IEnumerable<AuditLogEntry> BuildAuditLogEntries()
    {
        var now = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified);
        foreach (var entry in ChangeTracker.Entries()
            .Where(entry => entry.State is EntityState.Added or EntityState.Modified or EntityState.Deleted)
            .Where(entry => entry.Entity is not AuditLogEntry))
        {
            if (entry.Metadata.ClrType.Namespace?.StartsWith("Microsoft.", StringComparison.Ordinal) == true)
            {
                continue;
            }

            if (entry.Metadata.FindProperty("Id") is null)
            {
                continue;
            }

            var entityId = ReadEntityId(entry);
            if (!entityId.HasValue)
            {
                continue;
            }

            var before = entry.State == EntityState.Added ? null : BuildAuditSnapshot(entry, originalValues: true);
            var after = entry.State == EntityState.Deleted ? null : BuildAuditSnapshot(entry, originalValues: false);
            var changes = BuildAuditChanges(entry, before, after).ToList();
            var action = ResolveAuditAction(entry);

            if (entry.State == EntityState.Modified && changes.Count == 0)
            {
                continue;
            }

            yield return new AuditLogEntry
            {
                Id = Guid.NewGuid(),
                OccurredAt = now,
                CreatedAt = now,
                UpdatedAt = now,
                Action = action,
                Module = ResolveAuditModule(entry.Metadata.ClrType.Name),
                EntityName = entry.Metadata.ClrType.Name,
                EntityDisplayName = ToDisplayName(entry.Metadata.ClrType.Name),
                EntityId = entityId.Value,
                Reference = ResolveAuditReference(entry),
                CompanyId = ReadGuidProperty(entry, nameof(CompanyBase.CompanyId)) ?? auditActorContext?.CompanyId,
                StoreGroupId = ReadGuidProperty(entry, nameof(StoreBase.StoreGroupId)) ?? auditActorContext?.StoreGroupId,
                StoreId = ReadGuidProperty(entry, nameof(StoreBase.StoreId)) ?? auditActorContext?.StoreId,
                UserId = auditActorContext?.UserId,
                UserName = FirstNonBlank(auditActorContext?.UserName, ReadStringProperty(entry, nameof(CompanyBase.CreatedBy)), "System"),
                Source = "DbContext.SaveChanges",
                RequestMethod = auditActorContext?.RequestMethod,
                RequestPath = auditActorContext?.RequestPath,
                IpAddress = auditActorContext?.IpAddress,
                TraceIdentifier = auditActorContext?.TraceIdentifier,
                Reason = ResolveAuditReason(entry),
                BeforeJson = before is null ? null : JsonSerializer.Serialize(before),
                AfterJson = after is null ? null : JsonSerializer.Serialize(after),
                ChangesJson = JsonSerializer.Serialize(changes),
                ChangedFieldCount = changes.Count
            };
        }
    }

    private static Guid? ReadEntityId(EntityEntry entry)
    {
        var value = entry.State == EntityState.Deleted
            ? entry.Property("Id").OriginalValue
            : entry.Property("Id").CurrentValue;
        return value is Guid guid && guid != Guid.Empty ? guid : null;
    }

    private static IReadOnlyDictionary<string, string?> BuildAuditSnapshot(EntityEntry entry, bool originalValues)
    {
        var values = new SortedDictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        foreach (var property in entry.Properties.Where(property => ShouldAuditProperty(property)))
        {
            var value = originalValues ? property.OriginalValue : property.CurrentValue;
            values[property.Metadata.Name] = FormatAuditValue(value);
        }

        return values;
    }

    private static IEnumerable<AuditFieldChange> BuildAuditChanges(
        EntityEntry entry,
        IReadOnlyDictionary<string, string?>? before,
        IReadOnlyDictionary<string, string?>? after)
    {
        if (entry.State == EntityState.Added && after is not null)
        {
            foreach (var item in after.Where(item => !string.IsNullOrWhiteSpace(item.Value)))
            {
                yield return new AuditFieldChange(item.Key, null, item.Value);
            }
            yield break;
        }

        if (entry.State == EntityState.Deleted && before is not null)
        {
            foreach (var item in before.Where(item => !string.IsNullOrWhiteSpace(item.Value)))
            {
                yield return new AuditFieldChange(item.Key, item.Value, null);
            }
            yield break;
        }

        if (before is null || after is null)
        {
            yield break;
        }

        foreach (var key in before.Keys.Union(after.Keys, StringComparer.OrdinalIgnoreCase).OrderBy(item => item, StringComparer.OrdinalIgnoreCase))
        {
            before.TryGetValue(key, out var oldValue);
            after.TryGetValue(key, out var newValue);
            if (!string.Equals(oldValue, newValue, StringComparison.Ordinal))
            {
                yield return new AuditFieldChange(key, oldValue, newValue);
            }
        }
    }

    private static bool ShouldAuditProperty(PropertyEntry property)
    {
        if (property.Metadata.IsShadowProperty())
        {
            return false;
        }

        var propertyName = property.Metadata.Name;
        if (propertyName is nameof(BaseEntity.UpdatedAt) or nameof(BaseEntity.Synced))
        {
            return false;
        }

        if (IsSensitiveAuditProperty(propertyName))
        {
            return false;
        }

        var clrType = Nullable.GetUnderlyingType(property.Metadata.ClrType) ?? property.Metadata.ClrType;
        return clrType.IsPrimitive
            || clrType.IsEnum
            || clrType == typeof(string)
            || clrType == typeof(decimal)
            || clrType == typeof(Guid)
            || clrType == typeof(DateTime);
    }

    private static bool IsSensitiveAuditProperty(string propertyName)
        => propertyName.Contains("Password", StringComparison.OrdinalIgnoreCase)
            || propertyName.Contains("Token", StringComparison.OrdinalIgnoreCase)
            || propertyName.Contains("Secret", StringComparison.OrdinalIgnoreCase)
            || propertyName.Contains("SigningKey", StringComparison.OrdinalIgnoreCase)
            || propertyName.Contains("ApiKey", StringComparison.OrdinalIgnoreCase)
            || propertyName.Contains("Api_Key", StringComparison.OrdinalIgnoreCase)
            || propertyName.Equals("Authorization", StringComparison.OrdinalIgnoreCase);

    private static string ResolveAuditAction(EntityEntry entry)
    {
        if (entry.State == EntityState.Added)
        {
            return "Created";
        }

        if (entry.State == EntityState.Deleted)
        {
            return "Deleted";
        }

        if (entry.Metadata.FindProperty(nameof(BaseEntity.Deleted)) is not null
            && entry.Property(nameof(BaseEntity.Deleted)).OriginalValue is false
            && entry.Property(nameof(BaseEntity.Deleted)).CurrentValue is true)
        {
            return "Deleted";
        }

        return "Updated";
    }

    private static string ResolveAuditModule(string entityName)
    {
        if (entityName is "Company" or "Store" or "StoreGroup") return "Company";
        if (entityName is "AppUser" or "PasswordResetToken") return "Security";
        if (entityName.StartsWith("Tailoring", StringComparison.Ordinal)) return "Tailoring";
        if (entityName is "Invoice" or "InvoiceItem" or "InvoicePayment" or "CardPayment" or "Customer" or "Salesman" or "CustomerAdvanceReceipt" or "LoyaltyProgram" or "LoyaltyPointLedger") return "Billing";
        if (entityName.StartsWith("Purchase", StringComparison.Ordinal) || entityName is "Vendor" or "VendorSettlement" or "VendorSettlementAllocation" or "VendorPayment") return "Purchase";
        if (entityName is "Stock" or "StockMovement" or "StockOperationDocument" or "StockOperationItem" or "NonGstGoodsDocument" or "NonGstGoodsItem" or "Product" or "ProductDetail" or "Brand" or "ProductCategory" or "ProductSubCategory" or "Tax") return "Inventory";
        if (entityName.StartsWith("Gst", StringComparison.Ordinal)) return "GST Returns";
        if (entityName is "Employee" or "EmployeeDetail" or "Attendance" or "MonthlyAttendance" or "TimeSheet") return "HR";
        if (entityName is "SalaryStructure" or "SalaryPaySlip" or "SalaryPayment") return "Payroll";
        if (entityName.Contains("Voucher", StringComparison.Ordinal) && entityName is not "CashVoucher" and not "CashVoucherConversion") return "Vouchers";
        if (entityName is "PettyCashSheet" or "DayBegin" or "DayEnd") return "Petty Cash";
        if (entityName is "JournalEntry" or "JournalLine" or "Ledger" or "LedgerGroup" or "Party" or "Bank" or "BankAccount" or "BankAccountDetail" or "VendorBankAccount" or "BankTransaction" or "BankStatementLine" or "ChequeLog" or "CommercialNote" or "CashVoucher" or "CashVoucherConversion" or "BankCashTranscation" or "FinancialYearLock") return "Accounting";
        return "System";
    }

    private static string ResolveAuditReference(EntityEntry entry)
    {
        foreach (var propertyName in new[]
        {
            "InvoiceNumber", "DocumentNumber", "VoucherNumber", "OrderNumber", "ReturnNumber", "EntryNumber",
            "SettlementNumber", "ReceiptNumber", "NoteNumber", "InwardNumber", "Barcode", "Name", "UserName",
            "Email", "AccountNumber", "ChequeNumber", "ReferenceNumber", "Reference", "ServiceCode", "ServiceName", "Title"
        })
        {
            var value = ReadStringProperty(entry, propertyName);
            if (!string.IsNullOrWhiteSpace(value))
            {
                return value!;
            }
        }

        return ReadEntityId(entry)?.ToString() ?? string.Empty;
    }

    private static string? ResolveAuditReason(EntityEntry entry)
    {
        foreach (var propertyName in new[] { "Reason", "Remarks", "Narration", "LockReason", "UnlockReason", "CancelReason", "Description" })
        {
            var value = ReadStringProperty(entry, propertyName);
            if (!string.IsNullOrWhiteSpace(value))
            {
                return value;
            }
        }

        return null;
    }

    private static string? ReadStringProperty(EntityEntry entry, string propertyName)
    {
        if (entry.Metadata.FindProperty(propertyName) is null)
        {
            return null;
        }

        var value = entry.State == EntityState.Deleted
            ? entry.Property(propertyName).OriginalValue
            : entry.Property(propertyName).CurrentValue;
        return value?.ToString();
    }

    private static string? FormatAuditValue(object? value)
    {
        return value switch
        {
            null => null,
            DateTime dateTime => dateTime.ToString("yyyy-MM-dd HH:mm:ss"),
            DateTimeOffset dateTimeOffset => dateTimeOffset.ToString("yyyy-MM-dd HH:mm:ss zzz"),
            decimal number => number.ToString("0.##"),
            _ => value.ToString()
        };
    }

    private static string ToDisplayName(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var chars = new List<char> { value[0] };
        for (var i = 1; i < value.Length; i++)
        {
            if (char.IsUpper(value[i]) && !char.IsWhiteSpace(value[i - 1]))
            {
                chars.Add(' ');
            }
            chars.Add(value[i]);
        }
        return new string(chars.ToArray());
    }

    private static string FirstNonBlank(params string?[] values)
        => values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)) ?? string.Empty;

    private sealed record AuditFieldChange(string Field, string? Before, string? After);
    private sealed record DotMatrixStorePrintContext(Guid StoreId, Guid CompanyId, Guid StoreGroupId, string StoreCode, string StoreName, string PrinterName, int LineWidth, bool Enabled, bool PrintTransactions, bool PrintEditsAndDeletes, bool PrintDayOpeningClosing, bool PrintAttendanceInDaySummary, bool PrintBankUpiSummary, string TimeZoneId);

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
