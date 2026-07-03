using Garmetix.Api.Purchase;
using Garmetix.Core.Enums;

namespace Garmetix.Api.PurchaseImport;

public sealed record PurchaseInvoiceImportBatchDto(
    Guid Id,
    Guid CompanyId,
    Guid StoreGroupId,
    Guid StoreId,
    string Status,
    string SourceFileName,
    string ContentType,
    long FileSizeBytes,
    string Sha256Hash,
    string OcrProvider,
    string OcrStatus,
    decimal ConfidenceScore,
    string? ParserTemplate,
    string? ParserTemplateReason,
    string? ImportQaNotes,
    string? AcceptanceStatus,
    string? AcceptanceNotes,
    DateTime? AcceptanceTestedAt,
    string? AcceptanceTestedBy,
    string? CorrectionStatus,
    string? CorrectionNotes,
    DateTime? CorrectionRequestedAt,
    string? CorrectionRequestedBy,
    Guid? VendorId,
    string? VendorNameRaw,
    string? VendorNameFinal,
    string? VendorGstinRaw,
    string? VendorGstinFinal,
    string? VendorMobileNumber,
    string? VendorAddress,
    string? SupplierInvoiceNumber,
    DateTime? SupplierInvoiceDate,
    DateTime? DueDate,
    decimal TaxableAmount,
    decimal CgstAmount,
    decimal SgstAmount,
    decimal IgstAmount,
    decimal FreightAmount,
    decimal DiscountAmount,
    decimal RoundOff,
    decimal BillAmount,
    decimal PaidAmount,
    PaymentMode PaymentMode,
    Guid? BankAccountId,
    Guid? DuplicatePurchaseInvoiceId,
    Guid? PostedPurchaseInvoiceId,
    string? DuplicateOverrideReason,
    string? DuplicateOverrideBy,
    DateTime? DuplicateOverrideAt,
    string? ErrorMessage,
    DateTime CreatedAt,
    DateTime? UpdatedAt,
    DateTime? PostedAt,
    IReadOnlyList<PurchaseInvoiceImportLineDto> Lines,
    IReadOnlyList<string> Warnings,
    bool CanPost);

public sealed record PurchaseInvoiceImportListItemDto(
    Guid Id,
    string Status,
    string SourceFileName,
    string OcrStatus,
    string? VendorName,
    string? SupplierInvoiceNumber,
    DateTime? SupplierInvoiceDate,
    decimal BillAmount,
    int LineCount,
    Guid? PostedPurchaseInvoiceId,
    Guid? DuplicatePurchaseInvoiceId,
    DateTime CreatedAt,
    string? ParserTemplate,
    string? AcceptanceStatus,
    string? CorrectionStatus);

public sealed record PurchaseInvoiceImportLineDto(
    Guid Id,
    int LineNumber,
    Guid? ProductId,
    string? ProductNameRaw,
    string? ProductNameFinal,
    string? BarcodeRaw,
    string? BarcodeFinal,
    string? HsnCode,
    Unit Unit,
    decimal Quantity,
    decimal Mrp,
    decimal CostPrice,
    decimal UnitDiscount,
    decimal LineDiscount,
    decimal TaxRate,
    string GstPriceMode,
    Guid? TaxId,
    decimal TaxableAmount,
    decimal TaxAmount,
    decimal CgstAmount,
    decimal SgstAmount,
    decimal IgstAmount,
    decimal LineTotal,
    decimal ConfidenceScore,
    string MatchStatus,
    bool ReviewRequired,
    string? ReviewMessage,
    Guid? ProductCategoryId,
    Guid? ProductSubCategoryId,
    ProductType ProductType,
    ProductGroup ProductGroup,
    bool Ignored);

public sealed record PurchaseInvoiceImportUpdateRequest(
    Guid? VendorId,
    string? VendorNameFinal,
    string? VendorGstinFinal,
    string? VendorMobileNumber,
    string? VendorAddress,
    string? SupplierInvoiceNumber,
    DateTime? SupplierInvoiceDate,
    DateTime? DueDate,
    decimal FreightAmount,
    decimal DiscountAmount,
    decimal RoundOff,
    decimal BillAmount,
    decimal PaidAmount,
    PaymentMode PaymentMode,
    Guid? BankAccountId,
    string? ImportQaNotes,
    IReadOnlyList<PurchaseInvoiceImportLineUpdateRequest> Lines);

public sealed record PurchaseInvoiceImportLineUpdateRequest(
    Guid? Id,
    int LineNumber,
    Guid? ProductId,
    string? ProductNameFinal,
    string? BarcodeFinal,
    string? HsnCode,
    Unit Unit,
    decimal Quantity,
    decimal Mrp,
    decimal CostPrice,
    decimal UnitDiscount,
    decimal LineDiscount,
    decimal TaxRate,
    string GstPriceMode,
    Guid? TaxId,
    Guid? ProductCategoryId,
    Guid? ProductSubCategoryId,
    ProductType ProductType,
    ProductGroup ProductGroup,
    bool Ignored);

public sealed record PurchaseInvoiceImportPostRequest(PurchaseInwardRequest? Inward = null);

public sealed record PurchaseInvoiceImportPostResponse(
    Guid BatchId,
    Guid PurchaseInvoiceId,
    string InvoiceNumber,
    string InwardNumber,
    decimal BillAmount,
    int ItemCount,
    string Status);


public sealed record PurchaseInvoiceImportProductMatchOptionDto(
    Guid ProductId,
    string Name,
    string Barcode,
    string? HsnCode,
    Unit Unit,
    decimal Mrp,
    decimal TaxRate,
    Guid? TaxId,
    Guid? ProductCategoryId,
    Guid? ProductSubCategoryId,
    ProductType ProductType,
    ProductGroup ProductGroup,
    string MatchLabel);

public sealed record PurchaseInvoiceImportProductMatchRequest(Guid ProductId);

public sealed record PurchaseInvoiceImportSplitLineRequest(
    IReadOnlyList<string> SizeLabels,
    bool QuantityOnePerLine = true,
    bool AppendSizeToProductName = true,
    bool ClearProductMatchAndBarcode = true);

public sealed record PurchaseInvoiceImportDuplicateOverrideRequest(string Reason);

public sealed record PurchaseInvoiceImportFileDto(
    Guid Id,
    string FileKind,
    string OriginalFileName,
    string ContentType,
    long FileSizeBytes,
    string Sha256Hash,
    DateTime CreatedAt);

public sealed record PurchaseInvoiceImportDistributeDiscountRequest(decimal DiscountAmount);

public sealed record PurchaseInvoiceImportReparseTextRequest(string RawText, bool ReplaceLines = false);

public sealed record PurchaseInvoiceImportGenerateBarcodeRequest(bool OnlyMissing = true);

public sealed record PurchaseInvoiceImportVendorProfileDto(
    Guid Id,
    Guid? VendorId,
    string? VendorName,
    string? VendorGstin,
    int IgnoredPatternCount,
    int ProductAliasCount,
    int SuccessfulDraftCount,
    DateTime? LastLearnedAt,
    string? LearningNotes,
    string? PreferredParserTemplate,
    IReadOnlyList<string> IgnoredLinePatterns,
    IReadOnlyList<PurchaseInvoiceImportVendorProductAliasDto> ProductAliases);

public sealed record PurchaseInvoiceImportVendorProductAliasDto(
    string RawSignature,
    Guid? ProductId,
    string? ProductName,
    string? Barcode,
    string? HsnCode,
    decimal TaxRate,
    DateTime LearnedAt);

public sealed record PurchaseInvoiceImportVendorProfileResetRequest(bool ResetIgnoredLinePatterns = true, bool ResetProductAliases = true);

public sealed record PurchaseInvoiceImportVendorProfileRulesRequest(
    IReadOnlyList<string>? AddIgnoredLinePatterns = null,
    IReadOnlyList<string>? RemoveIgnoredLinePatterns = null,
    IReadOnlyList<string>? RemoveProductAliases = null,
    string? LearningNotes = null,
    string? PreferredParserTemplate = null);

public sealed record PurchaseInvoiceImportParserTemplateDto(
    string Key,
    string Name,
    string Description,
    bool IsVendorSpecific,
    IReadOnlyList<string> BestFor);

public sealed record PurchaseInvoiceImportAcceptanceUpdateRequest(
    string Status,
    string? Notes = null);

public sealed record PurchaseInvoiceImportCorrectionRequest(
    string Reason,
    string? RequestedAction = null);

public sealed record PurchaseInvoiceImportCorrectionSafetyDto(
    Guid BatchId,
    Guid? PostedPurchaseInvoiceId,
    string Status,
    string CorrectionStatus,
    bool IsPosted,
    bool CanDeleteDraft,
    bool CanRejectDraft,
    bool DirectUndoAvailable,
    IReadOnlyList<string> SafeActions,
    IReadOnlyList<string> Warnings);


public sealed record PurchaseInvoiceImportCorrectionPlanDto(
    Guid BatchId,
    Guid? PostedPurchaseInvoiceId,
    string Status,
    string CorrectionStatus,
    bool IsPosted,
    string RecommendedAction,
    IReadOnlyList<PurchaseInvoiceImportChecklistItemDto> Checklist,
    IReadOnlyList<string> Steps,
    IReadOnlyList<string> Warnings);

public sealed record PurchaseInvoiceImportBackupRestoreChecklistDto(
    string StorageRoot,
    string DockerVolume,
    int TotalBatches,
    int PostedBatches,
    int TotalFiles,
    long TotalStorageBytes,
    long ProtectedPostedProofBytes,
    long UnpostedStorageBytes,
    bool HasPostedProofs,
    IReadOnlyList<PurchaseInvoiceImportChecklistItemDto> Checklist,
    IReadOnlyList<string> BackupCommands,
    IReadOnlyList<string> RestoreVerificationSteps);

public sealed record PurchaseInvoiceImportParserTemplateStatDto(
    string ParserTemplate,
    int BatchCount,
    int PostedCount,
    int AcceptedPassCount,
    int CorrectionRequiredCount,
    decimal BillAmount,
    DateTime? LastUsedAt);

public sealed record PurchaseInvoiceImportVendorParserSummaryDto(
    string? VendorName,
    string? VendorGstin,
    string? PreferredParserTemplate,
    int BatchCount,
    int AcceptedPassCount,
    int CorrectionRequiredCount,
    DateTime? LastImportedAt);

public sealed record PurchaseInvoiceImportParserQaSummaryDto(
    int TotalBatches,
    int PostedBatches,
    int AcceptedPassBatches,
    int UntestedPostedBatches,
    int CorrectionRequiredBatches,
    IReadOnlyList<PurchaseInvoiceImportParserTemplateStatDto> Templates,
    IReadOnlyList<PurchaseInvoiceImportVendorParserSummaryDto> Vendors,
    IReadOnlyList<PurchaseInvoiceImportChecklistItemDto> Checklist);

public sealed record PurchaseInvoiceImportFinalClosureStatusDto(
    string ModuleStatus,
    bool IsComplete,
    string CompletionMode,
    int TotalBatches,
    int PostedBatches,
    int OpenDraftBatches,
    int UntestedPostedBatches,
    int CorrectionRequiredBatches,
    int AcceptedPassBatches,
    DateTime GeneratedAt,
    IReadOnlyList<PurchaseInvoiceImportChecklistItemDto> CloseoutChecklist,
    IReadOnlyList<string> KnownLimitations,
    IReadOnlyList<string> OperatorRules,
    IReadOnlyList<string> NextModuleCandidates,
    string RecommendedNextModule);

public sealed record PurchaseInvoiceImportReconciliationDto(
    decimal GrossTotal,
    decimal LineDiscountTotal,
    decimal GrossMinusDiscount,
    decimal TaxableTotal,
    decimal TaxTotal,
    decimal LineTotal,
    decimal FreightAmount,
    decimal RoundOff,
    decimal CalculatedGrandTotal,
    decimal ScannedBillAmount,
    decimal BillDifference,
    decimal HeaderDiscountAmount,
    decimal HeaderDiscountDifference);

public sealed record PurchaseInvoiceImportChecklistItemDto(
    string Key,
    string Label,
    string Status,
    string Detail);

public sealed record PurchaseInvoiceImportPostingReportDto(
    Guid BatchId,
    string Status,
    bool CanPost,
    int ActiveLineCount,
    int IgnoredLineCount,
    int NewProductCount,
    int MatchedProductCount,
    int MissingBarcodeCount,
    int DuplicateBarcodeGroupCount,
    int MissingTaxCount,
    int MissingProductDefaultsCount,
    int WarningCount,
    PurchaseInvoiceImportReconciliationDto Reconciliation,
    IReadOnlyList<PurchaseInvoiceImportChecklistItemDto> Checklist,
    IReadOnlyList<string> Warnings);

public sealed record PurchaseInvoiceImportAcceptanceStatusDto(
    string Status,
    int Count,
    decimal BillAmount,
    int LineCount,
    long FileBytes);

public sealed record PurchaseInvoiceImportAcceptanceSummaryDto(
    int TotalBatches,
    int PostedBatches,
    int ReadyToPostBatches,
    int NeedsReviewBatches,
    int FailedOrRejectedBatches,
    int DuplicateWarningBatches,
    int AcceptedPassBatches,
    int AcceptedFailBatches,
    int CorrectionRequiredBatches,
    int PostedToday,
    int PostedThisMonth,
    decimal PostedThisMonthBillAmount,
    int VendorProfileCount,
    int ProductAliasCount,
    int IgnoredPatternCount,
    long TotalStorageBytes,
    long ProtectedPostedProofBytes,
    long UnpostedStorageBytes,
    IReadOnlyList<PurchaseInvoiceImportAcceptanceStatusDto> Statuses,
    IReadOnlyList<PurchaseInvoiceImportChecklistItemDto> Checklist,
    IReadOnlyList<PurchaseInvoiceImportListItemDto> RecentBatches);

public sealed record PurchaseInvoiceImportDeleteBatchRequest(bool DeleteFiles = true);

public sealed record PurchaseInvoiceImportCleanupRequest(
    int OlderThanDays = 0,
    bool DeleteFailed = true,
    bool DeleteRejected = true,
    bool DeleteNeedsReview = false,
    bool DeleteReadyToPost = false,
    bool DeleteFiles = true);


public sealed record PurchaseInvoiceImportStorageStatusDto(
    string Status,
    int BatchCount,
    int LineCount,
    int FileCount,
    long FileBytes,
    decimal BillAmount);

public sealed record PurchaseInvoiceImportStorageSummaryDto(
    int TotalBatches,
    int PostedBatches,
    int UnpostedBatches,
    int FailedOrRejectedBatches,
    int TotalLines,
    int TotalFiles,
    long TotalFileBytes,
    long PostedProofBytes,
    long UnpostedFileBytes,
    DateTime? OldestUnpostedAt,
    DateTime? NewestImportAt,
    IReadOnlyList<PurchaseInvoiceImportStorageStatusDto> Statuses);

public sealed record PurchaseInvoiceImportCleanupResultDto(
    int DeletedBatches,
    int DeletedFiles,
    long DeletedBytes,
    IReadOnlyList<string> Messages);

