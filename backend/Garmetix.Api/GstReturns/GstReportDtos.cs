namespace Garmetix.Api.GstReturns;

public sealed record GstHsnSummaryReport(
    string ReturnPeriod,
    string Direction,
    DateTime FromDate,
    DateTime ToDate,
    int RowCount,
    decimal TotalQuantity,
    decimal TotalTaxableValue,
    decimal TotalCgstAmount,
    decimal TotalSgstAmount,
    decimal TotalIgstAmount,
    decimal TotalTaxAmount,
    decimal TotalValue,
    IReadOnlyList<GstHsnSummaryReportRow> Rows);

public sealed record GstHsnSummaryReportRow(
    int SerialNumber,
    string Direction,
    string HsnCode,
    string Description,
    string Uqc,
    decimal Rate,
    decimal Quantity,
    decimal TaxableValue,
    decimal CgstAmount,
    decimal SgstAmount,
    decimal IgstAmount,
    decimal TaxAmount,
    decimal TotalValue);

public sealed record GstTaxRateSummaryReport(
    string ReturnPeriod,
    DateTime FromDate,
    DateTime ToDate,
    int RowCount,
    decimal OutputTaxableValue,
    decimal OutputCgstAmount,
    decimal OutputSgstAmount,
    decimal OutputIgstAmount,
    decimal InputTaxableValue,
    decimal InputCgstAmount,
    decimal InputSgstAmount,
    decimal InputIgstAmount,
    decimal NetTaxPayable,
    IReadOnlyList<GstTaxRateSummaryRow> Rows);

public sealed record GstTaxRateSummaryRow(
    decimal Rate,
    decimal SalesTaxableValue,
    decimal SalesCgstAmount,
    decimal SalesSgstAmount,
    decimal SalesIgstAmount,
    decimal PurchaseTaxableValue,
    decimal PurchaseCgstAmount,
    decimal PurchaseSgstAmount,
    decimal PurchaseIgstAmount,
    decimal NetTaxPayable);

public sealed record GstInvoiceRegisterReport(
    string ReturnPeriod,
    string Direction,
    DateTime FromDate,
    DateTime ToDate,
    int RowCount,
    decimal TaxableValue,
    decimal CgstAmount,
    decimal SgstAmount,
    decimal IgstAmount,
    decimal TaxAmount,
    decimal BillAmount,
    IReadOnlyList<GstInvoiceRegisterRow> Rows);

public sealed record GstInvoiceRegisterRow(
    string Direction,
    string InvoiceNumber,
    string? ReferenceNumber,
    DateTime OnDate,
    string PartyName,
    string? PartyGstin,
    string InvoiceStatus,
    bool IsReturn,
    decimal TaxableValue,
    decimal CgstAmount,
    decimal SgstAmount,
    decimal IgstAmount,
    decimal TaxAmount,
    decimal BillAmount);
