using Garmetix.Core.Enums;

namespace Garmetix.Api.SaleImport;

public sealed record VyaparSaleImportPreviewDto(
    string SourceFileName,
    long FileSizeBytes,
    Guid CompanyId,
    Guid StoreGroupId,
    Guid StoreId,
    int InvoiceCount,
    int LineCount,
    int MatchedLineCount,
    int MissingLineCount,
    int InsufficientStockLineCount,
    int DuplicateInvoiceCount,
    decimal InvoiceAmountTotal,
    decimal LineAmountTotal,
    IReadOnlyList<string> Warnings,
    IReadOnlyList<VyaparSaleImportInvoiceDto> Invoices,
    IReadOnlyList<VyaparSaleImportMissingProductDto> MissingProducts,
    int FullyMatchedInvoiceCount,
    int ImportReadyInvoiceCount,
    int AutoHiddenExistingInvoiceCount,
    int UniqueCustomerCount,
    int UniquePaymentSourceCount,
    IReadOnlyList<VyaparSaleImportPaymentSourceDto> PaymentSources,
    IReadOnlyList<VyaparSaleImportImportedInvoiceDto> AutoHiddenInvoices);

public sealed record VyaparSaleImportInvoiceDto(
    string SourceInvoiceNumber,
    DateTime InvoiceDate,
    string CustomerName,
    string? CustomerMobileNumber,
    string? CustomerGstin,
    string? PaymentStatus,
    string? PaymentTypeRaw,
    decimal TotalAmount,
    decimal PaidAmount,
    decimal BalanceDue,
    bool DuplicateInvoice,
    bool ReadyForImport,
    IReadOnlyList<string> Warnings,
    IReadOnlyList<VyaparSaleImportPaymentDto> Payments,
    IReadOnlyList<VyaparSaleImportLineDto> Lines,
    bool FullyMatched = false,
    bool HiddenBecauseAlreadyImported = false,
    Guid? ExistingInvoiceId = null,
    string? ExistingInvoiceNumber = null,
    string? SourceDescription = null,
    string? ImportRemark = null,
    // True for a Sale Return/Credit Note adjustment (Vyapar Transaction Type) or an invoice Vyapar
    // itself marks Cancelled - either way this invoice is excluded from the Fully Matched and Ready
    // For Import buckets and must be reviewed/posted manually.
    bool IsReturnOrAdjustment = false);

public sealed record VyaparSaleImportLineDto(
    int LineNumber,
    string SourceInvoiceNumber,
    DateTime InvoiceDate,
    string PartyName,
    string ItemName,
    string? VyaparItemCode,
    string? GarmetixBarcode,
    string? OverrideBarcode,
    Guid? ProductId,
    string? ProductName,
    string? HsnCode,
    string? Category,
    string? Description,
    string? Size,
    decimal Quantity,
    string? Unit,
    decimal UnitPrice,
    decimal DiscountPercent,
    decimal DiscountAmount,
    decimal TaxRate,
    decimal TaxAmount,
    decimal LineTotal,
    decimal AvailableStock,
    string MatchStatus,
    bool ReviewRequired,
    bool CreateProductAndStock,
    bool ImportLine,
    string? ReviewMessage);

public sealed record VyaparSaleImportPaymentDto(
    string SourceName,
    PaymentMode PaymentMode,
    decimal Amount,
    Guid? BankAccountId,
    string? ReferenceNumber,
    string? SourceDescription = null,
    string? PaymentReferenceNote = null);

public sealed record VyaparSaleImportPaymentSourceDto(
    string SourceName,
    PaymentMode PaymentMode,
    decimal TotalAmount,
    int InvoiceCount,
    string? ExampleDescription,
    Guid? BankAccountId = null,
    // Purely descriptive - guessed from the Vyapar source name/description text so Step 3 of the
    // import UI can show and group Cash/Credit Card/Debit Card/UPI/Cheque/Bank Transfer separately,
    // even though the underlying PaymentMode enum only distinguishes Cash/Card/UPI/Cheque/etc.
    // Never used for accounting posting - only PaymentMode/BankAccountId drive that.
    string PaymentKindLabel = "Other");

public sealed record VyaparSaleImportMissingProductDto(
    string? VyaparItemCode,
    string ItemName,
    string? Category,
    string? HsnCode,
    string? Size,
    decimal Quantity,
    decimal Amount,
    decimal TaxRate,
    int InvoiceCount,
    string SuggestedBarcode,
    string? GarmetixBarcodeToFill);

public sealed record VyaparSaleImportConfirmRequest(
    Guid CompanyId,
    Guid StoreGroupId,
    Guid StoreId,
    bool UseVyaparInvoiceNumbers,
    bool CreateMissingProductsAndStock,
    bool AllowStockBridgeForInsufficientStock,
    Guid? DefaultBankAccountId,
    Guid? DefaultSalesmanId,
    IReadOnlyList<VyaparSaleImportInvoiceDto> Invoices,
    IReadOnlyList<VyaparPaymentBankMappingDto>? PaymentBankMappings = null,
    bool FinalApprovalConfirmed = false,
    Guid? ImportBatchId = null,
    string? SourceFileName = null);

public sealed record VyaparPaymentBankMappingDto(
    string SourceName,
    PaymentMode PaymentMode,
    Guid? BankAccountId);

public sealed record VyaparSaleImportConfirmResponse(
    Guid ImportBatchId,
    string ImportBatchReference,
    int ImportedInvoiceCount,
    int SkippedInvoiceCount,
    int ImportedLineCount,
    int CreatedProductCount,
    int CreatedStockCount,
    int BridgeStockMovementCount,
    decimal ImportedBillAmount,
    IReadOnlyList<string> ImportedInvoiceNumbers,
    IReadOnlyList<string> SkippedInvoices,
    IReadOnlyList<string> Warnings);

public sealed record VyaparSaleImportImportedInvoiceListDto(
    IReadOnlyList<VyaparSaleImportImportedInvoiceDto> Items,
    int Total,
    int Page,
    int PageSize,
    decimal BillAmountTotal,
    decimal PaidAmountTotal,
    decimal BalanceAmountTotal);

public sealed record VyaparSaleImportImportedInvoiceDto(
    Guid Id,
    string InvoiceNumber,
    string? SourceInvoiceNumber,
    DateTime InvoiceDate,
    string CustomerName,
    string CustomerMobileNumber,
    decimal BillAmount,
    decimal PaidAmount,
    decimal BalanceAmount,
    string InvoiceStatus,
    string? Remarks);


public sealed record VyaparSaleImportBatchListDto(
    IReadOnlyList<VyaparSaleImportBatchDto> Items,
    int Total,
    int Page,
    int PageSize);

public sealed record VyaparSaleImportBatchDto(
    Guid BatchId,
    string BatchReference,
    DateTime FromDate,
    DateTime ToDate,
    int InvoiceCount,
    int CancelledInvoiceCount,
    int ActiveInvoiceCount,
    decimal BillAmountTotal,
    decimal PaidAmountTotal,
    DateTime ImportedAt,
    IReadOnlyList<string> SampleInvoices,
    string Status);

public sealed record VyaparSaleImportUndoRequest(
    Guid CompanyId,
    Guid? StoreGroupId,
    Guid? StoreId,
    Guid BatchId,
    bool ConfirmUndo,
    string? Reason);

public sealed record VyaparSaleImportUndoResponse(
    Guid BatchId,
    int CancelledInvoiceCount,
    int SkippedInvoiceCount,
    decimal ReversedQuantity,
    decimal ReversedAmount,
    IReadOnlyList<string> CancelledInvoices,
    IReadOnlyList<string> SkippedInvoices);

public sealed record VyaparSaleImportClearHistoryRequest(
    Guid CompanyId,
    Guid? StoreGroupId,
    Guid? StoreId,
    DateTime? From,
    DateTime? To,
    bool CancelledOnly = true,
    bool AllowActiveHistoryClear = false,
    bool ConfirmClear = false,
    string? Reason = null);

public sealed record VyaparSaleImportClearHistoryResponse(
    int MatchedInvoiceCount,
    int ClearedInvoiceCount,
    int SkippedActiveInvoiceCount,
    IReadOnlyList<string> ClearedInvoices,
    IReadOnlyList<string> SkippedInvoices,
    string Message);

public sealed record VyaparBarcodeMappingDto(
    string? VyaparItemCode,
    string? ItemName,
    string GarmetixBarcode,
    string? Action);

public sealed record VyaparBarcodeMappingUploadResponse(
    string SourceFileName,
    int RowCount,
    int MappingCount,
    int IgnoredRowCount,
    IReadOnlyList<VyaparBarcodeMappingDto> Mappings);


public sealed record VyaparSaleImportPaymentModeSummaryDto(
    string PaymentMode,
    int PaymentRowCount,
    decimal TotalAmount,
    int BankMappedRowCount,
    int BankMissingRowCount);

public sealed record VyaparSaleImportGstSummaryDto(
    decimal TaxRate,
    int LineCount,
    decimal Quantity,
    decimal TaxableAmount,
    decimal TaxAmount,
    decimal CgstAmount,
    decimal SgstAmount,
    decimal IgstAmount,
    decimal LineAmount);

public sealed record VyaparSaleImportReconciliationIssueDto(
    string Severity,
    string Code,
    string Message,
    string? Reference,
    decimal? Difference = null);

public sealed record VyaparSaleImportFinalSummaryDto(
    DateTime GeneratedAt,
    Guid CompanyId,
    Guid? StoreId,
    DateTime? From,
    DateTime? To,
    string Status,
    int ImportedInvoiceCount,
    int ActiveInvoiceCount,
    int CancelledInvoiceCount,
    int BatchCount,
    int SourceInvoiceCount,
    int DuplicateSourceInvoiceCount,
    decimal BillAmountTotal,
    decimal PaidAmountTotal,
    decimal BalanceAmountTotal,
    decimal ItemAmountTotal,
    decimal PaymentRowTotal,
    decimal TaxableAmountTotal,
    decimal TaxAmountTotal,
    decimal CgstAmountTotal,
    decimal SgstAmountTotal,
    decimal IgstAmountTotal,
    decimal QuantitySold,
    int LineCount,
    int PaymentRowCount,
    int CashPaymentRowCount,
    int NonCashPaymentRowCount,
    int NonCashMissingBankRowCount,
    int MissingBatchMarkerCount,
    int BillItemMismatchCount,
    int PaidPaymentMismatchCount,
    int StockMovementInvoiceCoverageMissingCount,
    int HistoricalBridgeMovementCount,
    decimal HistoricalBridgeQuantity,
    decimal StockOutQuantity,
    decimal StockOutCostImpact,
    IReadOnlyList<VyaparSaleImportPaymentModeSummaryDto> PaymentModes,
    IReadOnlyList<VyaparSaleImportGstSummaryDto> GstSummary,
    IReadOnlyList<VyaparSaleImportReconciliationIssueDto> Issues,
    IReadOnlyList<string> CloseoutChecklist,
    IReadOnlyList<string> KnownLimitations,
    IReadOnlyList<string> NextModuleCandidates);
