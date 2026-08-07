using System.Globalization;
using System.Text;
using Garmetix.Api.ProductLookup;

namespace Garmetix.Api.Purchase;

public sealed record PurchasePdfModel(
    string CompanyName,
    string CompanyAddress,
    string CompanyPhone,
    string Gstin,
    string StoreName,
    string InvoiceNumber,
    string InwardNumber,
    DateTime OnDate,
    DateTime InwardDate,
    DateTime? SupplierInvoiceDate,
    string InvoiceStatus,
    string VendorName,
    string? VendorGstin,
    decimal MRP,
    decimal DiscountAmount,
    decimal NetAmount,
    decimal TaxAmount,
    decimal FreightAmount,
    decimal RoundOff,
    decimal BillAmount,
    decimal PaidAmount,
    decimal BalanceAmount,
    IReadOnlyList<PurchaseReceiptItemDto> Items,
    string DocumentCode);

public static class PurchasePdfDocument
{
    private const double A4Width = 595.28;
    private const double A4Height = 841.89;
    private const double A5Width = 419.53;
    private const double A5Height = 595.28;
    private const double Thermal2Width = 144;
    private const double Thermal3Width = 216;

    public static byte[] Build(PurchasePdfModel model, string? format, string? copy, bool reprint, bool signatures)
    {
        var normalizedFormat = NormalizeFormat(format);
        var normalizedCopy = NormalizeCopy(copy);
        return normalizedFormat switch
        {
            "thermal-2" => BuildThermal(model, Thermal2Width, normalizedCopy, reprint),
            "thermal-3" => BuildThermal(model, Thermal3Width, normalizedCopy, reprint),
            "a5" => BuildStandard(model, A5Width, A5Height, normalizedCopy, reprint, signatures, compact: true),
            _ => BuildStandard(model, A4Width, A4Height, normalizedCopy, reprint, signatures, compact: false)
        };
    }

    private static byte[] BuildStandard(PurchasePdfModel model, double width, double height, string copy, bool reprint, bool signatures, bool compact)
    {
        var left = compact ? 18.0 : 26.0;
        var top = compact ? 14.0 : 22.0;
        var bodyWidth = width - left * 2;
        var rowHeight = compact ? 16.0 : 18.0;
        var firstPageRows = compact ? 10 : 18;
        // Keep continuation pages short enough so the final amount block stays inside the page summary box.
        var continuationRows = compact ? 16 : 26;
        var itemPages = new List<IReadOnlyList<PurchaseReceiptItemDto>>();
        var remaining = model.Items.ToList();
        if (remaining.Count == 0)
        {
            itemPages.Add(Array.Empty<PurchaseReceiptItemDto>());
        }
        else
        {
            itemPages.Add(remaining.Take(firstPageRows).ToList());
            remaining = remaining.Skip(firstPageRows).ToList();
            while (remaining.Count > 0)
            {
                itemPages.Add(remaining.Take(continuationRows).ToList());
                remaining = remaining.Skip(continuationRows).ToList();
            }
        }

        var pageContents = new List<string>();
        for (var pageIndex = 0; pageIndex < itemPages.Count; pageIndex++)
        {
            var isFirstPage = pageIndex == 0;
            var isLastPage = pageIndex == itemPages.Count - 1;
            var canvas = new PdfCanvas(height);
            canvas.StrokeRect(left, top, bodyWidth, height - top * 2, 0.8, 0.32, 0.38, 0.44);
            DrawTallyHeader(canvas, model, left, top, bodyWidth, copy, reprint, compact, pageIndex + 1, itemPages.Count, isFirstPage);

            var currentTop = isFirstPage ? top + (compact ? 150 : 165) : top + (compact ? 84 : 96);
            var columns = compact
                ? new[] { 0.00, 0.36, 0.48, 0.60, 0.74, 0.87, 1.00 }
                : new[] { 0.00, 0.26, 0.36, 0.44, 0.53, 0.63, 0.73, 0.84, 1.00 };
            var headers = compact
                ? new[] { "Item", "Qty", "MRP", "Basic", "GST", "Amt" }
                : new[] { "Item / HSN", "Qty", "MRP", "Basic", "Cost", "Disc", "GST", "Amount" };

            DrawHeader(canvas, left + 6, currentTop, bodyWidth - 12, rowHeight, headers, columns);
            currentTop += rowHeight;
            foreach (var item in itemPages[pageIndex])
            {
                canvas.StrokeRect(left + 6, currentTop, bodyWidth - 12, rowHeight, 0.18, 0.82, 0.85, 0.88);
                canvas.WrappedText(ItemPrintName(item), left + 8, currentTop + 4, bodyWidth * (compact ? 0.34 : 0.29), 6.4, compact ? 1 : 2);
                var values = compact
                    ? new[]
                    {
                        item.Quantity.ToString("N2", CultureInfo.InvariantCulture),
                        item.Mrp.ToString("N2", CultureInfo.InvariantCulture),
                        item.BasicRate.ToString("N2", CultureInfo.InvariantCulture),
                        item.TaxAmount.ToString("N2", CultureInfo.InvariantCulture),
                        item.Amount.ToString("N2", CultureInfo.InvariantCulture)
                    }
                    : new[]
                    {
                        item.Quantity.ToString("N2", CultureInfo.InvariantCulture),
                        item.Mrp.ToString("N2", CultureInfo.InvariantCulture),
                        item.BasicRate.ToString("N2", CultureInfo.InvariantCulture),
                        item.CostPrice.ToString("N2", CultureInfo.InvariantCulture),
                        item.DiscountAmount.ToString("N2", CultureInfo.InvariantCulture),
                        item.TaxAmount.ToString("N2", CultureInfo.InvariantCulture),
                        item.Amount.ToString("N2", CultureInfo.InvariantCulture)
                    };
                DrawRowValues(canvas, left + 6, currentTop + 4, bodyWidth - 12, columns, values);
                currentTop += rowHeight;
            }

            DrawPurchasePageSummary(canvas, model, itemPages[pageIndex], left, height, bodyWidth, compact, isLastPage);
            if (signatures && isLastPage)
            {
                DrawSignatureStrip(canvas, left, bodyWidth, height, compact, new[] { "Prepared by", "Checked by", "Supplier", "Authorized" });
            }
            DrawFooter(canvas, model, left, bodyWidth, height, compact, pageIndex + 1, itemPages.Count);
            pageContents.Add(canvas.Content);
        }

        return BuildPdf(width, height, pageContents);
    }

    private static void DrawTallyHeader(PdfCanvas canvas, PurchasePdfModel model, double left, double top, double bodyWidth, string copy, bool reprint, bool compact, int pageNumber, int pageCount, bool firstPage)
    {
        canvas.FillRect(left, top, bodyWidth, 44, 0.02, 0.09, 0.16);
        canvas.FillRect(left, top + 44, bodyWidth, 2.5, 0.02, 0.70, 0.64);
        canvas.Text(model.CompanyName, left + 12, top + 8, compact ? 12.5 : 15, true, 1, 1, 1);
        canvas.WrappedText(model.CompanyAddress, left + 12, top + 26, bodyWidth * 0.58, compact ? 6 : 7, 2, false, 0.82, 0.88, 0.94);
        canvas.Text("PURCHASE INVOICE", left + bodyWidth - (compact ? 142 : 185), top + 8, compact ? 9.5 : 12, true, 1, 1, 1);
        canvas.Text($"{copy} | Page {pageNumber}/{pageCount}", left + bodyWidth - (compact ? 142 : 185), top + 25, 7, false, 0.82, 0.88, 0.94);
        if (firstPage)
        {
            canvas.Qr(model.DocumentCode, left + bodyWidth - 45, top + 4, 38);
        }
        if (reprint)
        {
            canvas.Text("REPRINT", left + bodyWidth - (compact ? 142 : 185), top + 36, 7, true, 0.98, 0.45, 0.45);
        }

        var infoTop = top + 55;
        var infoWidth = (bodyWidth - 12) / 4;
        DrawInfoBox(canvas, left + 6, infoTop, infoWidth, "Invoice No.", model.InvoiceNumber);
        DrawInfoBox(canvas, left + 6 + infoWidth, infoTop, infoWidth, "Inward No.", model.InwardNumber);
        DrawInfoBox(canvas, left + 6 + infoWidth * 2, infoTop, infoWidth, "Invoice Date", (model.SupplierInvoiceDate ?? model.OnDate).ToString("dd MMM yyyy", CultureInfo.InvariantCulture));
        DrawInfoBox(canvas, left + 6 + infoWidth * 3, infoTop, infoWidth, "Inward Date", model.InwardDate.ToString("dd MMM yyyy", CultureInfo.InvariantCulture));

        if (!firstPage)
        {
            canvas.Text($"Continued from previous page - {model.InvoiceNumber}", left + 10, infoTop + 42, 7.5, true, 0.08, 0.12, 0.18);
            return;
        }

        var vendorTop = infoTop + 42;
        canvas.FillRect(left + 6, vendorTop, bodyWidth - 12, compact ? 34 : 42, 0.95, 0.97, 0.98);
        canvas.StrokeRect(left + 6, vendorTop, bodyWidth - 12, compact ? 34 : 42, 0.35, 0.74, 0.78, 0.82);
        canvas.Text("Supplier", left + 12, vendorTop + 6, 6.8, true, 0.25, 0.30, 0.36);
        canvas.Text(EmptyAsDash(model.VendorName), left + 12, vendorTop + 17, 8, true, 0.08, 0.12, 0.18);
        canvas.Text($"GSTIN: {EmptyAsDash(model.VendorGstin)}", left + 12, vendorTop + 29, 6.8, false, 0.25, 0.30, 0.36);
        canvas.Text($"Store: {model.StoreName}", left + bodyWidth * 0.56, vendorTop + 17, 7, false, 0.25, 0.30, 0.36);
        canvas.Text($"Status: {model.InvoiceStatus}", left + bodyWidth * 0.56, vendorTop + 29, 7, false, 0.25, 0.30, 0.36);
    }

    private static void DrawPurchasePageSummary(PdfCanvas canvas, PurchasePdfModel model, IReadOnlyList<PurchaseReceiptItemDto> items, double left, double height, double bodyWidth, bool compact, bool isLastPage)
    {
        var lastPanelTopOffset = compact ? 170 : 220;
        var lastPanelHeight = compact ? 118 : 158;
        var summaryTop = height - (isLastPage ? lastPanelTopOffset : (compact ? 92 : 112));
        var pageQty = items.Sum(item => item.Quantity);
        var pageTax = items.Sum(item => item.TaxAmount);
        var pageAmount = items.Sum(item => item.Amount);
        var summaryHeight = isLastPage ? lastPanelHeight : 52;
        canvas.FillRect(left + 6, summaryTop, bodyWidth - 12, summaryHeight, 0.98, 0.99, 1.00);
        canvas.StrokeRect(left + 6, summaryTop, bodyWidth - 12, summaryHeight, 0.35, 0.72, 0.75, 0.79);
        canvas.Text($"Page summary: Qty {pageQty:N2} | GST {pageTax:N2} | Amount {pageAmount:N2}", left + 12, summaryTop + 8, 7, true, 0.08, 0.12, 0.18);
        if (!isLastPage)
        {
            canvas.RightText("Continued on next page...", left + bodyWidth - 12, summaryTop + 26, 7, true, 0.42, 0.46, 0.53);
            return;
        }

        var gridTop = summaryTop + (compact ? 18 : 24);
        var gridRowHeight = compact ? 22.0 : 34.0;
        DrawTotalsGrid(canvas, left + 6, gridTop, bodyWidth - 12, gridRowHeight, model, compact);
        var wordsTop = gridTop + gridRowHeight * 2 + (compact ? 8 : 10);
        canvas.WrappedText($"Amount in words: {AmountInWords(model.BillAmount)} only", left + 12, wordsTop, bodyWidth - 24, 7, compact ? 2 : 3, true);
    }

    private static void DrawSignatureStrip(PdfCanvas canvas, double left, double bodyWidth, double height, bool compact, string[] labels)
    {
        var signatureTop = height - (compact ? 42 : 52);
        var signatureWidth = (bodyWidth - 24) / labels.Length;
        for (var index = 0; index < labels.Length; index++)
        {
            var x = left + 12 + index * signatureWidth;
            canvas.Line(x + 4, signatureTop, x + signatureWidth - 4, signatureTop, 0.5, 0.45, 0.49, 0.54);
            canvas.CenteredText(labels[index], x, signatureTop + 7, signatureWidth, 6.5, false, 0.36, 0.40, 0.45);
        }
    }

    private static void DrawFooter(PdfCanvas canvas, PurchasePdfModel model, double left, double bodyWidth, double height, bool compact, int pageNumber, int pageCount)
    {
        canvas.CenteredText($"Scan code: {model.InvoiceNumber} | Page {pageNumber}/{pageCount}", left + 8, height - (compact ? 24 : 30), bodyWidth - 16, 6.5, true, 0.08, 0.12, 0.18);
        var footer = string.Join(" | ", new[]
        {
            string.IsNullOrWhiteSpace(model.CompanyPhone) ? null : $"Phone: {model.CompanyPhone}",
            string.IsNullOrWhiteSpace(model.Gstin) ? null : $"GSTIN: {model.Gstin}",
            "Generated by Garmetix"
        }.Where(value => !string.IsNullOrWhiteSpace(value)));
        canvas.CenteredText(footer, left + 8, height - 15, bodyWidth - 16, 6.2, false, 0.36, 0.40, 0.45);
    }

    private static byte[] BuildThermal(PurchasePdfModel model, double width, string copy, bool reprint)
    {
        var lineHeight = width <= Thermal2Width ? 10.0 : 11.0;
        var height = Math.Max(420, 230 + model.Items.Count * (lineHeight * 3.2));
        var canvas = new PdfCanvas(height);
        var left = width <= Thermal2Width ? 7.0 : 10.0;
        var bodyWidth = width - left * 2;
        var top = 10.0;
        var font = width <= Thermal2Width ? 6.4 : 7.2;

        canvas.CenteredText(model.CompanyName, left, top, bodyWidth, font + 2, true, 0.02, 0.09, 0.16);
        top += lineHeight + 2;
        canvas.CenteredWrappedText(model.CompanyAddress, left, top, bodyWidth, font, 2);
        top += lineHeight * 2;
        canvas.CenteredText($"PURCHASE - {copy}", left, top, bodyWidth, font + 0.5, true, 0.02, 0.09, 0.16);
        top += lineHeight;
        if (reprint)
        {
            canvas.CenteredText("REPRINT", left, top, bodyWidth, font, true, 0.78, 0.20, 0.20);
            top += lineHeight;
        }
        canvas.Line(left, top, left + bodyWidth, top, 0.4, 0.45, 0.49, 0.54);
        top += 5;
        canvas.Text($"Inv: {model.InvoiceNumber}", left, top, font, false, 0.08, 0.12, 0.18);
        top += lineHeight;
        canvas.Text($"Inw: {model.InwardNumber}", left, top, font, false, 0.08, 0.12, 0.18);
        top += lineHeight;
        canvas.Text($"Inv Date: {(model.SupplierInvoiceDate ?? model.OnDate):dd/MM/yyyy}", left, top, font, false, 0.08, 0.12, 0.18);
        top += lineHeight;
        canvas.Text($"Inw Date: {model.InwardDate:dd/MM/yyyy}", left, top, font, false, 0.08, 0.12, 0.18);
        top += lineHeight;
        canvas.CenteredWrappedText(model.VendorName, left, top, bodyWidth, font, 2);
        top += lineHeight * 2;
        canvas.Line(left, top, left + bodyWidth, top, 0.4, 0.45, 0.49, 0.54);
        top += 5;

        foreach (var item in model.Items)
        {
            canvas.WrappedText(ItemPrintName(item), left, top, bodyWidth, font, 2);
            top += lineHeight * 1.7;
            canvas.Text($"{item.Quantity:N2} x Basic {item.BasicRate:N2} | Disc {item.DiscountAmount:N2} | GST {item.TaxPercentage:N2}%", left, top, font, false, 0.08, 0.12, 0.18);
            canvas.RightText(item.Amount.ToString("N2", CultureInfo.InvariantCulture), left + bodyWidth, top, font, true, 0.08, 0.12, 0.18);
            top += lineHeight;
        }

        canvas.Line(left, top, left + bodyWidth, top, 0.4, 0.45, 0.49, 0.54);
        top += 6;
        DrawThermalAmount(canvas, left, bodyWidth, ref top, "CGST", model.Items.Sum(item => item.CgstAmount ?? 0), font, lineHeight);
        DrawThermalAmount(canvas, left, bodyWidth, ref top, "SGST", model.Items.Sum(item => item.SgstAmount ?? 0), font, lineHeight);
        DrawThermalAmount(canvas, left, bodyWidth, ref top, "IGST", model.Items.Sum(item => item.IgstAmount ?? 0), font, lineHeight);
        DrawThermalAmount(canvas, left, bodyWidth, ref top, "Tax", model.TaxAmount, font, lineHeight);
        DrawThermalAmount(canvas, left, bodyWidth, ref top, "Freight", model.FreightAmount, font, lineHeight);
        DrawThermalAmount(canvas, left, bodyWidth, ref top, "Bill", model.BillAmount, font + 1, lineHeight, bold: true);
        DrawThermalAmount(canvas, left, bodyWidth, ref top, "Paid", model.PaidAmount, font, lineHeight);
        DrawThermalAmount(canvas, left, bodyWidth, ref top, "Balance", model.BalanceAmount, font, lineHeight);
        canvas.CenteredText($"Scan: {model.InvoiceNumber}", left, top + 8, bodyWidth, font, true, 0.08, 0.12, 0.18);
        canvas.CenteredText("Generated by Garmetix", left, top + 8 + lineHeight, bodyWidth, font, false, 0.36, 0.40, 0.45);

        return BuildPdf(width, height, canvas.Content);
    }

    private static void DrawHeader(PdfCanvas canvas, double left, double top, double width, double height, string[] headers, double[] columns)
    {
        canvas.FillRect(left, top, width, height, 0.02, 0.09, 0.16);
        for (var index = 0; index < headers.Length; index++)
        {
            var colLeft = left + width * columns[index];
            var colWidth = width * (columns[index + 1] - columns[index]);
            if (index == 0)
            {
                canvas.Text(headers[index], colLeft + 4, top + 6, 7, true, 1, 1, 1);
            }
            else
            {
                canvas.CenteredText(headers[index], colLeft, top + 6, colWidth, 7, true, 1, 1, 1);
            }
        }
    }

    private static void DrawRowValues(PdfCanvas canvas, double left, double top, double width, double[] columns, string[] values)
    {
        // values[0] is the first numeric column (column index 1, right after Item / HSN); the last value (Amount) is bold.
        for (var index = 0; index < values.Length; index++)
        {
            var columnIndex = index + 1;
            var colLeft = left + width * columns[columnIndex];
            var colWidth = width * (columns[columnIndex + 1] - columns[columnIndex]);
            var isLast = index == values.Length - 1;
            canvas.CenteredText(values[index], colLeft, top, colWidth, 6.5, isLast, 0.08, 0.12, 0.18);
        }
    }

    private static void DrawInfoBox(PdfCanvas canvas, double x, double top, double width, string label, string value)
    {
        canvas.StrokeRect(x, top, width, 33, 0.35, 0.74, 0.78, 0.82);
        canvas.Text(label, x + 5, top + 5, 6.5, false, 0.38, 0.43, 0.49);
        canvas.Text(TrimTo(value, 20), x + 5, top + 16, 7.8, true, 0.08, 0.12, 0.18);
    }

    private static void DrawTotalsGrid(PdfCanvas canvas, double left, double top, double width, double rowHeight, PurchasePdfModel model, bool compact)
    {
        var totalQty = model.Items.Sum(item => item.Quantity);
        var totalItems = model.Items.Count;
        var cgst = model.Items.Sum(item => item.CgstAmount ?? 0);
        var sgst = model.Items.Sum(item => item.SgstAmount ?? 0);
        var igst = model.Items.Sum(item => item.IgstAmount ?? 0);

        var row1 = new (string Label, string Value)[]
        {
            ("Total Qty", totalQty.ToString("N2", CultureInfo.InvariantCulture)),
            ("Items", totalItems.ToString(CultureInfo.InvariantCulture)),
            ("MRP", model.MRP.ToString("N2", CultureInfo.InvariantCulture)),
            ("Discount", model.DiscountAmount.ToString("N2", CultureInfo.InvariantCulture)),
            ("Taxable", model.NetAmount.ToString("N2", CultureInfo.InvariantCulture)),
            ("CGST", cgst.ToString("N2", CultureInfo.InvariantCulture)),
            ("SGST", sgst.ToString("N2", CultureInfo.InvariantCulture))
        };
        var row2 = new (string Label, string Value)[]
        {
            ("IGST", igst.ToString("N2", CultureInfo.InvariantCulture)),
            ("Tax", model.TaxAmount.ToString("N2", CultureInfo.InvariantCulture)),
            ("Freight", model.FreightAmount.ToString("N2", CultureInfo.InvariantCulture)),
            ("Round off", model.RoundOff.ToString("N2", CultureInfo.InvariantCulture)),
            ("Bill Amount", model.BillAmount.ToString("N2", CultureInfo.InvariantCulture)),
            ("Paid", model.PaidAmount.ToString("N2", CultureInfo.InvariantCulture)),
            ("Balance", model.BalanceAmount.ToString("N2", CultureInfo.InvariantCulture))
        };

        var colWidth = width / row1.Length;
        DrawTotalsRow(canvas, left, top, colWidth, rowHeight, row1, compact);
        DrawTotalsRow(canvas, left, top + rowHeight, colWidth, rowHeight, row2, compact);
    }

    private static void DrawTotalsRow(PdfCanvas canvas, double left, double top, double colWidth, double rowHeight, (string Label, string Value)[] cells, bool compact)
    {
        var labelSize = compact ? 5.2 : 6.2;
        var valueSize = compact ? 6.4 : 7.6;
        for (var index = 0; index < cells.Length; index++)
        {
            var x = left + index * colWidth;
            canvas.FillRect(x, top, colWidth, rowHeight, 0.95, 0.97, 0.98);
            canvas.StrokeRect(x, top, colWidth, rowHeight, 0.35, 0.74, 0.78, 0.82);
            canvas.CenteredText(cells[index].Label, x, top + (compact ? 5 : 7), colWidth, labelSize, false, 0.38, 0.43, 0.49);
            canvas.CenteredText(cells[index].Value, x, top + (compact ? 14 : 18), colWidth, valueSize, true, 0.08, 0.12, 0.18);
        }
    }

    private static void DrawThermalAmount(PdfCanvas canvas, double left, double width, ref double top, string label, decimal value, double size, double lineHeight, bool bold = false)
    {
        canvas.Text(label, left, top, size, bold, 0.08, 0.12, 0.18);
        canvas.RightText(value.ToString("N2", CultureInfo.InvariantCulture), left + width, top, size, bold, 0.08, 0.12, 0.18);
        top += lineHeight;
    }

    private static string ItemPrintName(PurchaseReceiptItemDto item)
    {
        var details = string.Join(" | ", new[]
        {
            string.IsNullOrWhiteSpace(item.HsnCode) ? null : $"HSN {item.HsnCode}",
            string.IsNullOrWhiteSpace(item.Unit) ? null : item.Unit
        }.Where(value => !string.IsNullOrWhiteSpace(value)));
        return string.IsNullOrWhiteSpace(details) ? item.ProductName : $"{item.ProductName} ({details})";
    }

    private static string NormalizeFormat(string? value) => value?.Trim().ToLowerInvariant() switch
    {
        "a5" or "a5-one" => "a5",
        "thermal-2" or "2-inch" or "thermal2" => "thermal-2",
        "thermal-3" or "3-inch" or "thermal3" => "thermal-3",
        _ => "a4"
    };

    private static string NormalizeCopy(string? value) => value?.Trim().ToLowerInvariant() switch
    {
        "office" => "Office Copy",
        "duplicate" => "Duplicate Copy",
        "supplier" => "Supplier Copy",
        _ => "Store Copy"
    };

    private static string EmptyAsDash(string? value) => string.IsNullOrWhiteSpace(value) ? "-" : value.Trim();
    private static string TrimTo(string value, int length) => value.Length <= length ? value : $"{value[..Math.Max(0, length - 3)]}...";

    private static string AmountInWords(decimal amount)
    {
        var rupees = (long)Math.Floor(Math.Abs(amount));
        var paise = (int)Math.Round((Math.Abs(amount) - rupees) * 100, MidpointRounding.AwayFromZero);
        if (paise == 100)
        {
            rupees++;
            paise = 0;
        }

        var words = $"Rupees {IndianNumberWords(rupees)}";
        return paise > 0 ? $"{words} and {IndianNumberWords(paise)} paise" : words;
    }

    private static string IndianNumberWords(long number)
    {
        if (number == 0) return "zero";
        var units = new[] { (Value: 10_000_000L, Name: "crore"), (Value: 100_000L, Name: "lakh"), (Value: 1_000L, Name: "thousand"), (Value: 100L, Name: "hundred") };
        var parts = new List<string>();
        foreach (var unit in units)
        {
            if (number < unit.Value) continue;
            parts.Add($"{IndianNumberWords(number / unit.Value)} {unit.Name}");
            number %= unit.Value;
        }
        if (number > 0)
        {
            if (parts.Count > 0) parts.Add("and");
            parts.Add(UnderHundred((int)number));
        }
        return string.Join(" ", parts);
    }

    private static string UnderHundred(int number)
    {
        string[] belowTwenty = ["zero", "one", "two", "three", "four", "five", "six", "seven", "eight", "nine", "ten", "eleven", "twelve", "thirteen", "fourteen", "fifteen", "sixteen", "seventeen", "eighteen", "nineteen"];
        string[] tens = ["", "", "twenty", "thirty", "forty", "fifty", "sixty", "seventy", "eighty", "ninety"];
        return number < 20 ? belowTwenty[number] : number % 10 == 0 ? tens[number / 10] : $"{tens[number / 10]}-{belowTwenty[number % 10]}";
    }

    private static byte[] BuildPdf(double width, double height, string content)
        => BuildPdf(width, height, new[] { content });

    private static byte[] BuildPdf(double width, double height, IReadOnlyList<string> pageContents)
    {
        var safePages = pageContents.Count == 0 ? new List<string> { string.Empty } : pageContents.ToList();
        var pageCount = safePages.Count;
        var font1Object = 3 + pageCount * 2;
        var font2Object = font1Object + 1;
        var kids = string.Join(" ", Enumerable.Range(0, pageCount).Select(index => $"{3 + index * 2} 0 R"));
        var objects = new List<string>
        {
            "<< /Type /Catalog /Pages 2 0 R >>",
            $"<< /Type /Pages /Kids [{kids}] /Count {pageCount} >>"
        };

        for (var index = 0; index < pageCount; index++)
        {
            var pageObject = 3 + index * 2;
            var contentObject = pageObject + 1;
            var content = safePages[index];
            var streamBytes = Encoding.ASCII.GetBytes(content);
            objects.Add($"<< /Type /Page /Parent 2 0 R /MediaBox [0 0 {F(width)} {F(height)}] /Resources << /Font << /F1 {font1Object} 0 R /F2 {font2Object} 0 R >> >> /Contents {contentObject} 0 R >>");
            objects.Add($"<< /Length {streamBytes.Length} >>\nstream\n{content}endstream");
        }

        objects.Add("<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica /Encoding /WinAnsiEncoding >>");
        objects.Add("<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica-Bold /Encoding /WinAnsiEncoding >>");

        using var output = new MemoryStream();
        Write(output, "%PDF-1.4\n%Garmetix\n");
        var offsets = new List<long> { 0 };
        for (var index = 0; index < objects.Count; index++)
        {
            offsets.Add(output.Position);
            Write(output, $"{index + 1} 0 obj\n{objects[index]}\nendobj\n");
        }

        var xrefOffset = output.Position;
        Write(output, $"xref\n0 {objects.Count + 1}\n0000000000 65535 f \n");
        for (var index = 1; index < offsets.Count; index++)
        {
            Write(output, $"{offsets[index]:D10} 00000 n \n");
        }

        Write(output, $"trailer\n<< /Size {objects.Count + 1} /Root 1 0 R >>\nstartxref\n{xrefOffset}\n%%EOF");
        return output.ToArray();
    }

    private static void Write(Stream stream, string value)
    {
        var bytes = Encoding.ASCII.GetBytes(value);
        stream.Write(bytes, 0, bytes.Length);
    }

    private static string F(double value) => value.ToString("0.##", CultureInfo.InvariantCulture);

    private sealed class PdfCanvas(double pageHeight)
    {
        private readonly StringBuilder builder = new();
        public string Content => builder.ToString();

        public void Text(string value, double x, double top, double size, bool bold, double r, double g, double b)
        {
            builder.Append(CultureInfo.InvariantCulture, $"BT /{(bold ? "F2" : "F1")} {F(size)} Tf {F(r)} {F(g)} {F(b)} rg 1 0 0 1 {F(x)} {F(pageHeight - top - size)} Tm ({Escape(value)}) Tj ET\n");
        }

        public void RightText(string value, double right, double top, double size, bool bold, double r, double g, double b)
        {
            var estimatedWidth = Sanitize(value).Length * size * (bold ? 0.55 : 0.5);
            Text(value, right - estimatedWidth, top, size, bold, r, g, b);
        }

        public void CenteredText(string value, double x, double top, double width, double size, bool bold, double r, double g, double b)
        {
            var estimatedWidth = Sanitize(value).Length * size * (bold ? 0.55 : 0.5);
            Text(value, x + Math.Max(0, (width - estimatedWidth) / 2), top, size, bold, r, g, b);
        }

        public void CenteredWrappedText(string value, double x, double top, double width, double size, int maxLines)
        {
            var charsPerLine = Math.Max(8, (int)(width / (size * 0.52)));
            var lines = Wrap(Sanitize(value), charsPerLine, maxLines);
            for (var index = 0; index < lines.Count; index++)
            {
                CenteredText(lines[index], x, top + index * (size + 2), width, size, false, 0.08, 0.12, 0.18);
            }
        }

        public void WrappedText(string value, double x, double top, double width, double size, int maxLines, bool bold = false)
            => WrappedText(value, x, top, width, size, maxLines, bold, 0.08, 0.12, 0.18);

        public void WrappedText(string value, double x, double top, double width, double size, int maxLines, bool bold, double r, double g, double b)
        {
            var charsPerLine = Math.Max(8, (int)(width / (size * 0.52)));
            var lines = Wrap(Sanitize(value), charsPerLine, maxLines);
            for (var index = 0; index < lines.Count; index++)
            {
                Text(lines[index], x, top + index * (size + 2), size, bold, r, g, b);
            }
        }

        public void FillRect(double x, double top, double width, double height, double r, double g, double b)
        {
            builder.Append(CultureInfo.InvariantCulture, $"{F(r)} {F(g)} {F(b)} rg {F(x)} {F(pageHeight - top - height)} {F(width)} {F(height)} re f\n");
        }

        public void StrokeRect(double x, double top, double width, double height, double lineWidth, double r, double g, double b)
        {
            builder.Append(CultureInfo.InvariantCulture, $"{F(r)} {F(g)} {F(b)} RG {F(lineWidth)} w {F(x)} {F(pageHeight - top - height)} {F(width)} {F(height)} re S\n");
        }

        public void Line(double x1, double top1, double x2, double top2, double lineWidth, double r, double g, double b)
        {
            builder.Append(CultureInfo.InvariantCulture, $"{F(r)} {F(g)} {F(b)} RG {F(lineWidth)} w {F(x1)} {F(pageHeight - top1)} m {F(x2)} {F(pageHeight - top2)} l S\n");
        }

        public void Qr(string payload, double left, double top, double size)
            => DocumentCodeService.AppendPdfCommands(builder, pageHeight, left, top, size, payload);

        private static List<string> Wrap(string value, int maxLength, int maxLines)
        {
            var words = value.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            var lines = new List<string>();
            var current = new StringBuilder();
            foreach (var word in words)
            {
                if (current.Length > 0 && current.Length + word.Length + 1 > maxLength)
                {
                    lines.Add(current.ToString());
                    current.Clear();
                    if (lines.Count == maxLines) break;
                }
                if (current.Length > 0) current.Append(' ');
                current.Append(word.Length > maxLength ? word[..maxLength] : word);
            }
            if (current.Length > 0 && lines.Count < maxLines) lines.Add(current.ToString());
            if (lines.Count > 0 && words.Length > 0 && string.Join(" ", lines).Length < value.Length - 2)
            {
                lines[^1] = TrimTo(lines[^1], Math.Max(4, maxLength - 3)) + "...";
            }
            return lines;
        }

        private static string Escape(string value) => Sanitize(value).Replace("\\", "\\\\").Replace("(", "\\(").Replace(")", "\\)");

        private static string Sanitize(string value)
        {
            value ??= string.Empty;
            var builder = new StringBuilder(value.Length);
            foreach (var character in value)
            {
                builder.Append(character is >= ' ' and <= '~' ? character : '?');
            }
            return builder.ToString();
        }
    }
}
