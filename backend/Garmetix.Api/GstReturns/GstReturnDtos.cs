namespace Garmetix.Api.GstReturns;

public sealed record GstReturnPeriodRequest(
    string Gstin,
    string ReturnPeriod,
    decimal GrossTurnover,
    decimal CurrentTurnover,
    string LegalName,
    string TradeName);

public sealed record Gstr1ExportRequest(
    GstReturnPeriodRequest Header,
    IReadOnlyList<Gstr1B2BInvoiceRow> B2BInvoices,
    IReadOnlyList<Gstr1B2CSummaryRow> B2CSummaries,
    IReadOnlyList<Gstr1HsnSummaryRow> HsnSummaries,
    IReadOnlyList<Gstr1DocumentIssuedRow> DocumentsIssued,
    IReadOnlyList<Gstr1NilRatedRow> NilRatedSupplies);

public sealed record Gstr1B2BInvoiceRow(
    string RecipientGstin,
    string RecipientName,
    string InvoiceNumber,
    DateTime InvoiceDate,
    string PlaceOfSupply,
    string ReverseCharge,
    string InvoiceType,
    decimal InvoiceValue,
    decimal Rate,
    decimal TaxableValue,
    decimal IntegratedTax,
    decimal CentralTax,
    decimal StateTax,
    decimal Cess,
    string ECommerceGstin);

public sealed record Gstr1B2CSummaryRow(
    string Type,
    string PlaceOfSupply,
    string ECommerceGstin,
    decimal Rate,
    decimal TaxableValue,
    decimal IntegratedTax,
    decimal CentralTax,
    decimal StateTax,
    decimal Cess);

public sealed record Gstr1HsnSummaryRow(
    int SerialNumber,
    string HsnCode,
    string Description,
    string Uqc,
    decimal TotalQuantity,
    decimal TotalValue,
    decimal TaxableValue,
    decimal IntegratedTax,
    decimal CentralTax,
    decimal StateTax,
    decimal Cess);

public sealed record Gstr1DocumentIssuedRow(
    int SerialNumber,
    string NatureOfDocument,
    string FromSerialNumber,
    string ToSerialNumber,
    int TotalNumber,
    int CancelledNumber);

public sealed record Gstr1NilRatedRow(
    string Description,
    decimal NilRated,
    decimal Exempted,
    decimal NonGst);

public sealed record Gstr3BExportRequest(
    GstReturnPeriodRequest Header,
    Gstr3BSuppliesSummary Supplies,
    Gstr3BInterStateSupply InterStateSupplies,
    Gstr3BItcSummary Itc,
    Gstr3BInwardSummary InwardSupplies,
    Gstr3BInterestLateFee InterestLateFee);

public sealed record Gstr3BSuppliesSummary(
    decimal OutwardTaxableValue,
    decimal OutwardIntegratedTax,
    decimal OutwardCentralTax,
    decimal OutwardStateTax,
    decimal OutwardCess,
    decimal ZeroRatedTaxableValue,
    decimal ZeroRatedIntegratedTax,
    decimal NilExemptTaxableValue,
    decimal NonGstTaxableValue,
    decimal ReverseChargeTaxableValue,
    decimal ReverseChargeIntegratedTax,
    decimal ReverseChargeCentralTax,
    decimal ReverseChargeStateTax,
    decimal ReverseChargeCess);

public sealed record Gstr3BInterStateSupply(
    decimal UnregisteredTaxableValue,
    decimal UnregisteredIntegratedTax,
    decimal CompositionTaxableValue,
    decimal CompositionIntegratedTax,
    decimal UinTaxableValue,
    decimal UinIntegratedTax);

public sealed record Gstr3BItcSummary(
    decimal ImportGoodsIntegratedTax,
    decimal ImportGoodsCess,
    decimal ImportServicesIntegratedTax,
    decimal ReverseChargeIntegratedTax,
    decimal ReverseChargeCentralTax,
    decimal ReverseChargeStateTax,
    decimal ReverseChargeCess,
    decimal IsdIntegratedTax,
    decimal IsdCentralTax,
    decimal IsdStateTax,
    decimal IsdCess,
    decimal OtherIntegratedTax,
    decimal OtherCentralTax,
    decimal OtherStateTax,
    decimal OtherCess,
    decimal ReversalRule42IntegratedTax,
    decimal ReversalRule42CentralTax,
    decimal ReversalRule42StateTax,
    decimal ReversalRule42Cess,
    decimal ReversalOtherIntegratedTax,
    decimal ReversalOtherCentralTax,
    decimal ReversalOtherStateTax,
    decimal ReversalOtherCess,
    decimal IneligibleIntegratedTax,
    decimal IneligibleCentralTax,
    decimal IneligibleStateTax,
    decimal IneligibleCess);

public sealed record Gstr3BInwardSummary(
    decimal CompositionTaxableValue,
    decimal CompositionIntegratedTax,
    decimal CompositionCentralTax,
    decimal CompositionStateTax,
    decimal NilRatedTaxableValue,
    decimal NilRatedIntegratedTax,
    decimal NilRatedCentralTax,
    decimal NilRatedStateTax,
    decimal NonGstTaxableValue);

public sealed record Gstr3BInterestLateFee(
    decimal IntegratedTaxInterest,
    decimal CentralTaxInterest,
    decimal StateTaxInterest,
    decimal CessInterest,
    decimal CentralLateFee,
    decimal StateLateFee);

public sealed record GstValidationIssue(string Field, string Message);

public sealed record GstExportPreview(
    string Form,
    string Gstin,
    string ReturnPeriod,
    int RowCount,
    decimal TaxableValue,
    decimal IntegratedTax,
    decimal CentralTax,
    decimal StateTax,
    decimal Cess,
    IReadOnlyList<GstValidationIssue> Issues);

public sealed record GstReturnDraftSaveRequest(
    string Form,
    string Title,
    Guid? CompanyId,
    System.Text.Json.JsonElement Payload);

public sealed record GstReturnDraftSummaryDto(
    Guid Id,
    string Form,
    string Gstin,
    string ReturnPeriod,
    string Title,
    string Status,
    int RowCount,
    decimal TaxableValue,
    decimal IntegratedTax,
    decimal CentralTax,
    decimal StateTax,
    decimal Cess,
    DateTime CreatedAt,
    DateTime? UpdatedAt,
    string UpdatedByUserName);

public sealed record GstReturnDraftDetailDto(
    Guid Id,
    string Form,
    string Gstin,
    string ReturnPeriod,
    string Title,
    string Status,
    Guid CompanyId,
    string PayloadJson,
    string LastPreviewIssuesJson,
    int RowCount,
    decimal TaxableValue,
    decimal IntegratedTax,
    decimal CentralTax,
    decimal StateTax,
    decimal Cess,
    DateTime CreatedAt,
    DateTime? UpdatedAt,
    string CreatedByUserName,
    string UpdatedByUserName,
    DateTime? FiledAt,
    DateTime? LockedAt);

public sealed record GstReturnAuditDto(
    Guid Id,
    Guid DraftId,
    string Form,
    string ReturnPeriod,
    string Gstin,
    string Action,
    string Summary,
    string ActorName,
    DateTime CreatedAt,
    string DetailsJson);
