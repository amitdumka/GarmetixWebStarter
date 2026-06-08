using System.Globalization;
using System.IO.Compression;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace Garmetix.Api.GstReturns;

public static class GstReturnExportService
{
    private static readonly Regex GstinPattern = new("^[0-9]{2}[A-Z]{5}[0-9]{4}[A-Z][1-9A-Z]Z[0-9A-Z]$", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex PeriodPattern = new("^(0[1-9]|1[0-2])[0-9]{4}$", RegexOptions.Compiled);
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        PropertyNamingPolicy = null
    };

    public static GstExportPreview PreviewGstr1(Gstr1ExportRequest request)
    {
        var issues = ValidateHeader(request.Header).ToList();
        issues.AddRange(ValidateGstr1(request));
        var rowCount = request.B2BInvoices.Count + request.B2CSummaries.Count + request.HsnSummaries.Count + request.DocumentsIssued.Count + request.NilRatedSupplies.Count;
        return new GstExportPreview(
            "GSTR-1",
            Clean(request.Header.Gstin).ToUpperInvariant(),
            Clean(request.Header.ReturnPeriod),
            rowCount,
            request.B2BInvoices.Sum(row => row.TaxableValue) + request.B2CSummaries.Sum(row => row.TaxableValue),
            request.B2BInvoices.Sum(row => row.IntegratedTax) + request.B2CSummaries.Sum(row => row.IntegratedTax),
            request.B2BInvoices.Sum(row => row.CentralTax) + request.B2CSummaries.Sum(row => row.CentralTax),
            request.B2BInvoices.Sum(row => row.StateTax) + request.B2CSummaries.Sum(row => row.StateTax),
            request.B2BInvoices.Sum(row => row.Cess) + request.B2CSummaries.Sum(row => row.Cess),
            issues);
    }

    public static GstExportPreview PreviewGstr3B(Gstr3BExportRequest request)
    {
        var issues = ValidateHeader(request.Header).ToList();
        var supplies = request.Supplies;
        var itc = request.Itc;
        return new GstExportPreview(
            "GSTR-3B",
            Clean(request.Header.Gstin).ToUpperInvariant(),
            Clean(request.Header.ReturnPeriod),
            5,
            supplies.OutwardTaxableValue + supplies.ZeroRatedTaxableValue + supplies.NilExemptTaxableValue + supplies.NonGstTaxableValue + supplies.ReverseChargeTaxableValue,
            supplies.OutwardIntegratedTax + supplies.ZeroRatedIntegratedTax + supplies.ReverseChargeIntegratedTax + itc.ImportGoodsIntegratedTax + itc.ImportServicesIntegratedTax + itc.OtherIntegratedTax,
            supplies.OutwardCentralTax + supplies.ReverseChargeCentralTax + itc.OtherCentralTax,
            supplies.OutwardStateTax + supplies.ReverseChargeStateTax + itc.OtherStateTax,
            supplies.OutwardCess + supplies.ReverseChargeCess + itc.OtherCess,
            issues);
    }

    public static byte[] BuildGstr1Json(Gstr1ExportRequest request)
    {
        var payload = new
        {
            gstin = Clean(request.Header.Gstin).ToUpperInvariant(),
            fp = Clean(request.Header.ReturnPeriod),
            gt = request.Header.GrossTurnover,
            cur_gt = request.Header.CurrentTurnover,
            version = "garmetix-manual-v1",
            generated_at_utc = DateTimeOffset.UtcNow,
            source = "Garmetix GST Returns module - manual/separate entry",
            b2b = request.B2BInvoices
                .GroupBy(row => Clean(row.RecipientGstin).ToUpperInvariant())
                .Select(group => new
                {
                    ctin = group.Key,
                    inv = group.Select(row => new
                    {
                        inum = Clean(row.InvoiceNumber),
                        idt = row.InvoiceDate.ToString("dd-MM-yyyy", CultureInfo.InvariantCulture),
                        val = Round(row.InvoiceValue),
                        pos = Clean(row.PlaceOfSupply),
                        rchrg = NormalizeYesNo(row.ReverseCharge),
                        inv_typ = string.IsNullOrWhiteSpace(row.InvoiceType) ? "R" : Clean(row.InvoiceType),
                        etin = BlankAsNull(row.ECommerceGstin),
                        itms = new[]
                        {
                            new
                            {
                                num = 1,
                                itm_det = new
                                {
                                    rt = Round(row.Rate),
                                    txval = Round(row.TaxableValue),
                                    iamt = Round(row.IntegratedTax),
                                    camt = Round(row.CentralTax),
                                    samt = Round(row.StateTax),
                                    csamt = Round(row.Cess)
                                }
                            }
                        }
                    }).ToArray()
                }).ToArray(),
            b2cs = request.B2CSummaries.Select(row => new
            {
                sply_ty = string.IsNullOrWhiteSpace(row.Type) ? "INTRA" : Clean(row.Type),
                pos = Clean(row.PlaceOfSupply),
                etin = BlankAsNull(row.ECommerceGstin),
                rt = Round(row.Rate),
                txval = Round(row.TaxableValue),
                iamt = Round(row.IntegratedTax),
                camt = Round(row.CentralTax),
                samt = Round(row.StateTax),
                csamt = Round(row.Cess)
            }).ToArray(),
            hsn = new
            {
                data = request.HsnSummaries.Select(row => new
                {
                    num = row.SerialNumber,
                    hsn_sc = Clean(row.HsnCode),
                    desc = Clean(row.Description),
                    uqc = Clean(row.Uqc),
                    qty = Round(row.TotalQuantity),
                    val = Round(row.TotalValue),
                    txval = Round(row.TaxableValue),
                    iamt = Round(row.IntegratedTax),
                    camt = Round(row.CentralTax),
                    samt = Round(row.StateTax),
                    csamt = Round(row.Cess)
                }).ToArray()
            },
            nil = new
            {
                inv = request.NilRatedSupplies.Select(row => new
                {
                    sply_ty = Clean(row.Description),
                    nil_amt = Round(row.NilRated),
                    expt_amt = Round(row.Exempted),
                    ngsup_amt = Round(row.NonGst)
                }).ToArray()
            },
            doc_issue = new
            {
                doc_det = request.DocumentsIssued.Select(row => new
                {
                    doc_num = row.SerialNumber,
                    docs = new[]
                    {
                        new
                        {
                            num = row.SerialNumber,
                            from = Clean(row.FromSerialNumber),
                            to = Clean(row.ToSerialNumber),
                            totnum = row.TotalNumber,
                            cancel = row.CancelledNumber,
                            net_issue = Math.Max(0, row.TotalNumber - row.CancelledNumber),
                            doc_typ = Clean(row.NatureOfDocument)
                        }
                    }
                }).ToArray()
            }
        };

        return JsonSerializer.SerializeToUtf8Bytes(payload, JsonOptions);
    }

    public static byte[] BuildGstr3BJson(Gstr3BExportRequest request)
    {
        var s = request.Supplies;
        var interstate = request.InterStateSupplies;
        var itc = request.Itc;
        var inward = request.InwardSupplies;
        var fee = request.InterestLateFee;
        var payload = new
        {
            gstin = Clean(request.Header.Gstin).ToUpperInvariant(),
            ret_period = Clean(request.Header.ReturnPeriod),
            version = "garmetix-manual-v1",
            generated_at_utc = DateTimeOffset.UtcNow,
            source = "Garmetix GST Returns module - manual/separate entry",
            sup_details = new
            {
                osup_det = TaxBlock(s.OutwardTaxableValue, s.OutwardIntegratedTax, s.OutwardCentralTax, s.OutwardStateTax, s.OutwardCess),
                osup_zero = TaxBlock(s.ZeroRatedTaxableValue, s.ZeroRatedIntegratedTax, 0, 0, 0),
                osup_nil_exmp = TaxBlock(s.NilExemptTaxableValue, 0, 0, 0, 0),
                isup_rev = TaxBlock(s.ReverseChargeTaxableValue, s.ReverseChargeIntegratedTax, s.ReverseChargeCentralTax, s.ReverseChargeStateTax, s.ReverseChargeCess),
                osup_nongst = TaxBlock(s.NonGstTaxableValue, 0, 0, 0, 0)
            },
            inter_sup = new
            {
                unreg_details = new[] { InterstateBlock("Unregistered", interstate.UnregisteredTaxableValue, interstate.UnregisteredIntegratedTax) },
                comp_details = new[] { InterstateBlock("Composition", interstate.CompositionTaxableValue, interstate.CompositionIntegratedTax) },
                uin_details = new[] { InterstateBlock("UIN", interstate.UinTaxableValue, interstate.UinIntegratedTax) }
            },
            itc_elg = new
            {
                itc_avl = new[]
                {
                    ItcBlock("Import of goods", itc.ImportGoodsIntegratedTax, 0, 0, itc.ImportGoodsCess),
                    ItcBlock("Import of services", itc.ImportServicesIntegratedTax, 0, 0, 0),
                    ItcBlock("Reverse charge", itc.ReverseChargeIntegratedTax, itc.ReverseChargeCentralTax, itc.ReverseChargeStateTax, itc.ReverseChargeCess),
                    ItcBlock("ISD", itc.IsdIntegratedTax, itc.IsdCentralTax, itc.IsdStateTax, itc.IsdCess),
                    ItcBlock("All other ITC", itc.OtherIntegratedTax, itc.OtherCentralTax, itc.OtherStateTax, itc.OtherCess)
                },
                itc_rev = new[]
                {
                    ItcBlock("Rule 42/43", itc.ReversalRule42IntegratedTax, itc.ReversalRule42CentralTax, itc.ReversalRule42StateTax, itc.ReversalRule42Cess),
                    ItcBlock("Others", itc.ReversalOtherIntegratedTax, itc.ReversalOtherCentralTax, itc.ReversalOtherStateTax, itc.ReversalOtherCess)
                },
                itc_net = ItcBlock(
                    "Net ITC",
                    itc.ImportGoodsIntegratedTax + itc.ImportServicesIntegratedTax + itc.ReverseChargeIntegratedTax + itc.IsdIntegratedTax + itc.OtherIntegratedTax - itc.ReversalRule42IntegratedTax - itc.ReversalOtherIntegratedTax,
                    itc.ReverseChargeCentralTax + itc.IsdCentralTax + itc.OtherCentralTax - itc.ReversalRule42CentralTax - itc.ReversalOtherCentralTax,
                    itc.ReverseChargeStateTax + itc.IsdStateTax + itc.OtherStateTax - itc.ReversalRule42StateTax - itc.ReversalOtherStateTax,
                    itc.ImportGoodsCess + itc.ReverseChargeCess + itc.IsdCess + itc.OtherCess - itc.ReversalRule42Cess - itc.ReversalOtherCess),
                itc_inelg = new[] { ItcBlock("Ineligible ITC", itc.IneligibleIntegratedTax, itc.IneligibleCentralTax, itc.IneligibleStateTax, itc.IneligibleCess) }
            },
            inward_sup = new
            {
                isup_details = new[]
                {
                    new { ty = "Composition taxable person", inter = Round(inward.CompositionIntegratedTax), intra = Round(inward.CompositionCentralTax + inward.CompositionStateTax), txval = Round(inward.CompositionTaxableValue) },
                    new { ty = "Nil rated/exempt/non-GST", inter = Round(inward.NilRatedIntegratedTax), intra = Round(inward.NilRatedCentralTax + inward.NilRatedStateTax), txval = Round(inward.NilRatedTaxableValue + inward.NonGstTaxableValue) }
                }
            },
            intr_ltfee = new
            {
                intr_details = ItcBlock("Interest", fee.IntegratedTaxInterest, fee.CentralTaxInterest, fee.StateTaxInterest, fee.CessInterest),
                ltfee_details = new { c_lfee = Round(fee.CentralLateFee), s_lfee = Round(fee.StateLateFee) }
            }
        };

        return JsonSerializer.SerializeToUtf8Bytes(payload, JsonOptions);
    }

    public static byte[] BuildGstr1Excel(Gstr1ExportRequest request)
    {
        var sheets = new List<XlsxSheet>
        {
            new("Return Summary", new[] { "Field", "Value" }, new[]
            {
                new[] { "Form", "GSTR-1" },
                new[] { "GSTIN", Clean(request.Header.Gstin).ToUpperInvariant() },
                new[] { "Return Period", Clean(request.Header.ReturnPeriod) },
                new[] { "Legal Name", Clean(request.Header.LegalName) },
                new[] { "Trade Name", Clean(request.Header.TradeName) },
                new[] { "Gross Turnover", Money(request.Header.GrossTurnover) },
                new[] { "Current Turnover", Money(request.Header.CurrentTurnover) },
                new[] { "Generated At UTC", DateTimeOffset.UtcNow.ToString("u", CultureInfo.InvariantCulture) },
                new[] { "Note", "Manual/separate GST module export. Not linked to Billing/Purchase yet." }
            }),
            new("B2B", new[] { "Recipient GSTIN", "Recipient Name", "Invoice No", "Invoice Date", "POS", "Reverse Charge", "Invoice Type", "Invoice Value", "Rate", "Taxable", "IGST", "CGST", "SGST", "Cess", "E-Commerce GSTIN" },
                request.B2BInvoices.Select(row => new[] { Clean(row.RecipientGstin), Clean(row.RecipientName), Clean(row.InvoiceNumber), Date(row.InvoiceDate), Clean(row.PlaceOfSupply), NormalizeYesNo(row.ReverseCharge), Clean(row.InvoiceType), Money(row.InvoiceValue), Money(row.Rate), Money(row.TaxableValue), Money(row.IntegratedTax), Money(row.CentralTax), Money(row.StateTax), Money(row.Cess), Clean(row.ECommerceGstin) })),
            new("B2CS", new[] { "Type", "POS", "E-Commerce GSTIN", "Rate", "Taxable", "IGST", "CGST", "SGST", "Cess" },
                request.B2CSummaries.Select(row => new[] { Clean(row.Type), Clean(row.PlaceOfSupply), Clean(row.ECommerceGstin), Money(row.Rate), Money(row.TaxableValue), Money(row.IntegratedTax), Money(row.CentralTax), Money(row.StateTax), Money(row.Cess) })),
            new("HSN", new[] { "No", "HSN", "Description", "UQC", "Qty", "Total Value", "Taxable", "IGST", "CGST", "SGST", "Cess" },
                request.HsnSummaries.Select(row => new[] { row.SerialNumber.ToString(CultureInfo.InvariantCulture), Clean(row.HsnCode), Clean(row.Description), Clean(row.Uqc), Money(row.TotalQuantity), Money(row.TotalValue), Money(row.TaxableValue), Money(row.IntegratedTax), Money(row.CentralTax), Money(row.StateTax), Money(row.Cess) })),
            new("Documents", new[] { "No", "Nature", "From", "To", "Total", "Cancelled", "Net Issued" },
                request.DocumentsIssued.Select(row => new[] { row.SerialNumber.ToString(CultureInfo.InvariantCulture), Clean(row.NatureOfDocument), Clean(row.FromSerialNumber), Clean(row.ToSerialNumber), row.TotalNumber.ToString(CultureInfo.InvariantCulture), row.CancelledNumber.ToString(CultureInfo.InvariantCulture), Math.Max(0, row.TotalNumber - row.CancelledNumber).ToString(CultureInfo.InvariantCulture) })),
            new("Nil Exempt Non-GST", new[] { "Description", "Nil Rated", "Exempted", "Non-GST" },
                request.NilRatedSupplies.Select(row => new[] { Clean(row.Description), Money(row.NilRated), Money(row.Exempted), Money(row.NonGst) }))
        };

        return SimpleXlsxBuilder.Build(sheets);
    }

    public static byte[] BuildGstr3BExcel(Gstr3BExportRequest request)
    {
        var s = request.Supplies;
        var i = request.InterStateSupplies;
        var itc = request.Itc;
        var inward = request.InwardSupplies;
        var fee = request.InterestLateFee;
        var sheets = new List<XlsxSheet>
        {
            new("Return Summary", new[] { "Field", "Value" }, new[]
            {
                new[] { "Form", "GSTR-3B" },
                new[] { "GSTIN", Clean(request.Header.Gstin).ToUpperInvariant() },
                new[] { "Return Period", Clean(request.Header.ReturnPeriod) },
                new[] { "Legal Name", Clean(request.Header.LegalName) },
                new[] { "Trade Name", Clean(request.Header.TradeName) },
                new[] { "Generated At UTC", DateTimeOffset.UtcNow.ToString("u", CultureInfo.InvariantCulture) },
                new[] { "Note", "Manual/separate GST module export. Not linked to Billing/Purchase yet." }
            }),
            new("3.1 Supplies", new[] { "Nature", "Taxable", "IGST", "CGST", "SGST", "Cess" }, new[]
            {
                Row("Outward taxable supplies", s.OutwardTaxableValue, s.OutwardIntegratedTax, s.OutwardCentralTax, s.OutwardStateTax, s.OutwardCess),
                Row("Zero rated supplies", s.ZeroRatedTaxableValue, s.ZeroRatedIntegratedTax, 0, 0, 0),
                Row("Nil/exempt supplies", s.NilExemptTaxableValue, 0, 0, 0, 0),
                Row("Non-GST outward supplies", s.NonGstTaxableValue, 0, 0, 0, 0),
                Row("Inward liable to reverse charge", s.ReverseChargeTaxableValue, s.ReverseChargeIntegratedTax, s.ReverseChargeCentralTax, s.ReverseChargeStateTax, s.ReverseChargeCess)
            }),
            new("3.2 Interstate", new[] { "Type", "Taxable", "IGST" }, new[]
            {
                new[] { "Unregistered persons", Money(i.UnregisteredTaxableValue), Money(i.UnregisteredIntegratedTax) },
                new[] { "Composition taxable persons", Money(i.CompositionTaxableValue), Money(i.CompositionIntegratedTax) },
                new[] { "UIN holders", Money(i.UinTaxableValue), Money(i.UinIntegratedTax) }
            }),
            new("4 ITC", new[] { "Nature", "IGST", "CGST", "SGST", "Cess" }, new[]
            {
                ItcRow("Import goods", itc.ImportGoodsIntegratedTax, 0, 0, itc.ImportGoodsCess),
                ItcRow("Import services", itc.ImportServicesIntegratedTax, 0, 0, 0),
                ItcRow("Reverse charge", itc.ReverseChargeIntegratedTax, itc.ReverseChargeCentralTax, itc.ReverseChargeStateTax, itc.ReverseChargeCess),
                ItcRow("ISD", itc.IsdIntegratedTax, itc.IsdCentralTax, itc.IsdStateTax, itc.IsdCess),
                ItcRow("All other ITC", itc.OtherIntegratedTax, itc.OtherCentralTax, itc.OtherStateTax, itc.OtherCess),
                ItcRow("Reversal Rule 42/43", itc.ReversalRule42IntegratedTax, itc.ReversalRule42CentralTax, itc.ReversalRule42StateTax, itc.ReversalRule42Cess),
                ItcRow("Reversal other", itc.ReversalOtherIntegratedTax, itc.ReversalOtherCentralTax, itc.ReversalOtherStateTax, itc.ReversalOtherCess),
                ItcRow("Ineligible ITC", itc.IneligibleIntegratedTax, itc.IneligibleCentralTax, itc.IneligibleStateTax, itc.IneligibleCess)
            }),
            new("5 Inward", new[] { "Nature", "Taxable", "IGST", "CGST", "SGST" }, new[]
            {
                new[] { "Composition", Money(inward.CompositionTaxableValue), Money(inward.CompositionIntegratedTax), Money(inward.CompositionCentralTax), Money(inward.CompositionStateTax) },
                new[] { "Nil rated", Money(inward.NilRatedTaxableValue), Money(inward.NilRatedIntegratedTax), Money(inward.NilRatedCentralTax), Money(inward.NilRatedStateTax) },
                new[] { "Non-GST", Money(inward.NonGstTaxableValue), "0.00", "0.00", "0.00" }
            }),
            new("Interest Late Fee", new[] { "Nature", "IGST", "CGST", "SGST", "Cess", "CGST Late Fee", "SGST Late Fee" }, new[]
            {
                new[] { "Interest/Late fee", Money(fee.IntegratedTaxInterest), Money(fee.CentralTaxInterest), Money(fee.StateTaxInterest), Money(fee.CessInterest), Money(fee.CentralLateFee), Money(fee.StateLateFee) }
            })
        };

        return SimpleXlsxBuilder.Build(sheets);
    }

    private static IEnumerable<GstValidationIssue> ValidateHeader(GstReturnPeriodRequest header)
    {
        if (string.IsNullOrWhiteSpace(header.Gstin) || !GstinPattern.IsMatch(header.Gstin.Trim()))
        {
            yield return new GstValidationIssue("GSTIN", "GSTIN should be 15 characters in standard GSTIN format.");
        }

        if (string.IsNullOrWhiteSpace(header.ReturnPeriod) || !PeriodPattern.IsMatch(header.ReturnPeriod.Trim()))
        {
            yield return new GstValidationIssue("ReturnPeriod", "Return period should be MMYYYY, for example 042026.");
        }
    }

    private static IEnumerable<GstValidationIssue> ValidateGstr1(Gstr1ExportRequest request)
    {
        foreach (var row in request.B2BInvoices.Select((value, index) => (value, index)))
        {
            if (string.IsNullOrWhiteSpace(row.value.RecipientGstin) || !GstinPattern.IsMatch(row.value.RecipientGstin.Trim()))
            {
                yield return new GstValidationIssue($"B2B[{row.index + 1}].RecipientGstin", "Recipient GSTIN is invalid.");
            }

            if (string.IsNullOrWhiteSpace(row.value.InvoiceNumber))
            {
                yield return new GstValidationIssue($"B2B[{row.index + 1}].InvoiceNumber", "Invoice number is required.");
            }
        }
    }

    private static object TaxBlock(decimal taxable, decimal integrated, decimal central, decimal state, decimal cess) => new
    {
        txval = Round(taxable),
        iamt = Round(integrated),
        camt = Round(central),
        samt = Round(state),
        csamt = Round(cess)
    };

    private static object InterstateBlock(string type, decimal taxable, decimal integrated) => new
    {
        ty = type,
        txval = Round(taxable),
        iamt = Round(integrated)
    };

    private static object ItcBlock(string type, decimal integrated, decimal central, decimal state, decimal cess) => new
    {
        ty = type,
        iamt = Round(integrated),
        camt = Round(central),
        samt = Round(state),
        csamt = Round(cess)
    };

    private static string[] Row(string label, decimal taxable, decimal integrated, decimal central, decimal state, decimal cess) =>
        [label, Money(taxable), Money(integrated), Money(central), Money(state), Money(cess)];

    private static string[] ItcRow(string label, decimal integrated, decimal central, decimal state, decimal cess) =>
        [label, Money(integrated), Money(central), Money(state), Money(cess)];

    private static string NormalizeYesNo(string value) => string.Equals(value?.Trim(), "Y", StringComparison.OrdinalIgnoreCase) || string.Equals(value?.Trim(), "Yes", StringComparison.OrdinalIgnoreCase) ? "Y" : "N";
    private static string? BlankAsNull(string value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim().ToUpperInvariant();
    private static string Clean(string? value) => value?.Trim() ?? string.Empty;
    private static decimal Round(decimal value) => Math.Round(value, 2, MidpointRounding.AwayFromZero);
    private static string Money(decimal value) => Round(value).ToString("0.00", CultureInfo.InvariantCulture);
    private static string Date(DateTime value) => value.ToString("dd-MM-yyyy", CultureInfo.InvariantCulture);
}

public sealed record XlsxSheet(string Name, IReadOnlyList<string> Headers, IEnumerable<IReadOnlyList<string>> Rows);

public static class SimpleXlsxBuilder
{
    public static byte[] Build(IReadOnlyList<XlsxSheet> sheets)
    {
        using var stream = new MemoryStream();
        using (var zip = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            Add(zip, "[Content_Types].xml", BuildContentTypes(sheets.Count));
            Add(zip, "_rels/.rels", """
                <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
                <Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">
                  <Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument" Target="xl/workbook.xml"/>
                </Relationships>
                """);
            Add(zip, "xl/_rels/workbook.xml.rels", BuildWorkbookRels(sheets.Count));
            Add(zip, "xl/workbook.xml", BuildWorkbook(sheets));
            Add(zip, "xl/styles.xml", BuildStyles());

            for (var index = 0; index < sheets.Count; index++)
            {
                Add(zip, $"xl/worksheets/sheet{index + 1}.xml", BuildSheet(sheets[index]));
            }
        }

        return stream.ToArray();
    }

    private static void Add(ZipArchive zip, string path, string content)
    {
        var entry = zip.CreateEntry(path, CompressionLevel.Optimal);
        using var writer = new StreamWriter(entry.Open(), new UTF8Encoding(false));
        writer.Write(content);
    }

    private static string BuildContentTypes(int sheetCount)
    {
        var sb = new StringBuilder();
        sb.Append("""
            <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
            <Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types">
              <Default Extension="rels" ContentType="application/vnd.openxmlformats-package.relationships+xml"/>
              <Default Extension="xml" ContentType="application/xml"/>
              <Override PartName="/xl/workbook.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml"/>
              <Override PartName="/xl/styles.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.styles+xml"/>
            """);
        for (var i = 1; i <= sheetCount; i++)
        {
            sb.Append(CultureInfo.InvariantCulture, $"  <Override PartName=\"/xl/worksheets/sheet{i}.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml\"/>\n");
        }
        sb.Append("</Types>");
        return sb.ToString();
    }

    private static string BuildWorkbookRels(int sheetCount)
    {
        var sb = new StringBuilder("""
            <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
            <Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">
            """);
        for (var i = 1; i <= sheetCount; i++)
        {
            sb.Append(CultureInfo.InvariantCulture, $"  <Relationship Id=\"rId{i}\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet\" Target=\"worksheets/sheet{i}.xml\"/>\n");
        }
        sb.Append(CultureInfo.InvariantCulture, $"  <Relationship Id=\"rId{sheetCount + 1}\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/styles\" Target=\"styles.xml\"/>\n");
        sb.Append("</Relationships>");
        return sb.ToString();
    }

    private static string BuildWorkbook(IReadOnlyList<XlsxSheet> sheets)
    {
        var sb = new StringBuilder("""
            <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
            <workbook xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main" xmlns:r="http://schemas.openxmlformats.org/officeDocument/2006/relationships"><sheets>
            """);
        for (var i = 0; i < sheets.Count; i++)
        {
            sb.Append(CultureInfo.InvariantCulture, $"<sheet name=\"{Xml(SafeSheetName(sheets[i].Name))}\" sheetId=\"{i + 1}\" r:id=\"rId{i + 1}\"/>");
        }
        sb.Append("</sheets></workbook>");
        return sb.ToString();
    }

    private static string BuildStyles() => """
        <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
        <styleSheet xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main">
          <fonts count="2"><font><sz val="11"/><name val="Calibri"/></font><font><b/><sz val="11"/><name val="Calibri"/></font></fonts>
          <fills count="2"><fill><patternFill patternType="none"/></fill><fill><patternFill patternType="gray125"/></fill></fills>
          <borders count="1"><border><left/><right/><top/><bottom/><diagonal/></border></borders>
          <cellStyleXfs count="1"><xf numFmtId="0" fontId="0" fillId="0" borderId="0"/></cellStyleXfs>
          <cellXfs count="2"><xf numFmtId="0" fontId="0" fillId="0" borderId="0" xfId="0"/><xf numFmtId="0" fontId="1" fillId="0" borderId="0" xfId="0"/></cellXfs>
        </styleSheet>
        """;

    private static string BuildSheet(XlsxSheet sheet)
    {
        var rows = new List<IReadOnlyList<string>> { sheet.Headers };
        rows.AddRange(sheet.Rows);
        var sb = new StringBuilder("""
            <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
            <worksheet xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main"><sheetData>
            """);

        for (var rowIndex = 0; rowIndex < rows.Count; rowIndex++)
        {
            sb.Append(CultureInfo.InvariantCulture, $"<row r=\"{rowIndex + 1}\">");
            for (var columnIndex = 0; columnIndex < rows[rowIndex].Count; columnIndex++)
            {
                var cellRef = CellReference(rowIndex + 1, columnIndex + 1);
                var style = rowIndex == 0 ? " s=\"1\"" : string.Empty;
                sb.Append(CultureInfo.InvariantCulture, $"<c r=\"{cellRef}\" t=\"inlineStr\"{style}><is><t>{Xml(rows[rowIndex][columnIndex])}</t></is></c>");
            }
            sb.Append("</row>");
        }

        sb.Append("</sheetData></worksheet>");
        return sb.ToString();
    }

    private static string CellReference(int row, int column)
    {
        var name = string.Empty;
        while (column > 0)
        {
            var modulo = (column - 1) % 26;
            name = Convert.ToChar('A' + modulo) + name;
            column = (column - modulo) / 26;
        }
        return $"{name}{row}";
    }

    private static string SafeSheetName(string value)
    {
        var clean = new string(value.Select(ch => ":\\/?*[]".Contains(ch) ? '-' : ch).ToArray()).Trim();
        return string.IsNullOrWhiteSpace(clean) ? "Sheet" : clean[..Math.Min(31, clean.Length)];
    }

    private static string Xml(string value) => System.Security.SecurityElement.Escape(value) ?? string.Empty;
}
