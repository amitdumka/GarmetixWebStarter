using System.Globalization;
using System.Text;

namespace Garmetix.Api.Billing;

public sealed record InvoicePdfModel(
    string CompanyName,
    string CompanyAddress,
    string CompanyPhone,
    string Gstin,
    string StoreName,
    string InvoiceNumber,
    DateTime OnDate,
    string InvoiceStatus,
    string CustomerName,
    string CustomerMobileNumber,
    decimal MRP,
    decimal DiscountAmount,
    decimal NetAmount,
    decimal TaxAmount,
    decimal RoundOff,
    decimal BillAmount,
    decimal PaidAmount,
    decimal BalanceAmount,
    IReadOnlyList<ReceiptItemDto> Items,
    IReadOnlyList<ReceiptPaymentDto> Payments,
    string DocumentCode,
    string? Remarks = null,
    string? PolicyUrl = null);

public static class InvoicePdfDocument
{
    private const double A4Width = 595.28;
    private const double A4Height = 841.89;
    private const double A5Width = 419.53;
    private const double A5Height = 595.28;
    private const double Thermal2Width = 144;
    private const double Thermal3Width = 216;

    public static byte[] Build(InvoicePdfModel model, string format, string copy, bool reprint, bool signatures)
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

    private static byte[] BuildStandard(
        InvoicePdfModel model,
        double width,
        double height,
        string copy,
        bool reprint,
        bool signatures,
        bool compact)
    {
        var left = compact ? 18.0 : 26.0;
        var top = compact ? 14.0 : 22.0;
        var bodyWidth = width - left * 2;
        var rowHeight = compact ? 16.0 : 18.0;
        var firstPageRows = compact ? 10 : 18;
        // Keep continuation pages short enough so the final amount block stays inside the page summary box.
        var continuationRows = compact ? 16 : 26;
        var itemPages = new List<IReadOnlyList<ReceiptItemDto>>();
        var remaining = model.Items.ToList();
        if (remaining.Count == 0)
        {
            itemPages.Add(Array.Empty<ReceiptItemDto>());
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

            var currentTop = isFirstPage ? top + (compact ? 145 : 158) : top + (compact ? 84 : 96);
            var columns = compact
                ? new[] { 0.00, 0.40, 0.52, 0.65, 0.79, 1.00 }
                : new[] { 0.00, 0.30, 0.40, 0.50, 0.61, 0.72, 0.84, 1.00 };
            var headers = compact
                ? new[] { "Item", "Qty", "MRP", "Disc", "Amt" }
                : new[] { "Item / HSN", "Qty", "MRP", "Rate", "Disc", "GST", "Amount" };

            DrawHeader(canvas, left + 6, currentTop, bodyWidth - 12, rowHeight, headers, columns);
            currentTop += rowHeight;
            foreach (var item in itemPages[pageIndex])
            {
                canvas.StrokeRect(left + 6, currentTop, bodyWidth - 12, rowHeight, 0.18, 0.82, 0.85, 0.88);
                canvas.WrappedText(ItemPrintName(item), left + 8, currentTop + 4, bodyWidth * (compact ? 0.38 : 0.33), 6.4, compact ? 1 : 2);
                canvas.RightText(item.Quantity.ToString("N2", CultureInfo.InvariantCulture), left + 6 + (bodyWidth - 12) * columns[2] - 4, currentTop + 4, 6.5, false, 0.08, 0.12, 0.18);
                canvas.RightText(item.Mrp.ToString("N2", CultureInfo.InvariantCulture), left + 6 + (bodyWidth - 12) * columns[3] - 4, currentTop + 4, 6.5, false, 0.08, 0.12, 0.18);
                if (compact)
                {
                    canvas.RightText(item.DiscountAmount.ToString("N2", CultureInfo.InvariantCulture), left + 6 + (bodyWidth - 12) * columns[4] - 4, currentTop + 4, 6.5, false, 0.08, 0.12, 0.18);
                    canvas.RightText(item.Amount.ToString("N2", CultureInfo.InvariantCulture), left + bodyWidth - 10, currentTop + 4, 6.5, true, 0.08, 0.12, 0.18);
                }
                else
                {
                    var unitRate = item.Quantity == 0 ? item.Mrp : Math.Round(item.Amount / item.Quantity, 2, MidpointRounding.AwayFromZero);
                    canvas.RightText(unitRate.ToString("N2", CultureInfo.InvariantCulture), left + 6 + (bodyWidth - 12) * columns[4] - 4, currentTop + 4, 6.5, false, 0.08, 0.12, 0.18);
                    canvas.RightText(item.DiscountAmount.ToString("N2", CultureInfo.InvariantCulture), left + 6 + (bodyWidth - 12) * columns[5] - 4, currentTop + 4, 6.5, false, 0.08, 0.12, 0.18);
                    canvas.RightText(item.TaxAmount.ToString("N2", CultureInfo.InvariantCulture), left + 6 + (bodyWidth - 12) * columns[6] - 4, currentTop + 4, 6.5, false, 0.08, 0.12, 0.18);
                    canvas.RightText(item.Amount.ToString("N2", CultureInfo.InvariantCulture), left + bodyWidth - 10, currentTop + 4, 6.5, true, 0.08, 0.12, 0.18);
                }
                currentTop += rowHeight;
            }

            DrawSalesPageSummary(canvas, model, itemPages[pageIndex], left, height, bodyWidth, compact, isLastPage);
            if (signatures && isLastPage)
            {
                DrawSignatureStrip(canvas, left, bodyWidth, height, compact, new[] { "Prepared by", "Checked by", "Customer", "Authorized" });
            }
            DrawFooter(canvas, model, left, bodyWidth, height, compact, pageIndex + 1, itemPages.Count);
            pageContents.Add(canvas.Content);
        }

        return BuildPdf(width, height, pageContents);
    }

    private static void DrawTallyHeader(PdfCanvas canvas, InvoicePdfModel model, double left, double top, double bodyWidth, string copy, bool reprint, bool compact, int pageNumber, int pageCount, bool firstPage)
    {
        canvas.FillRect(left, top, bodyWidth, 44, 0.02, 0.09, 0.16);
        canvas.FillRect(left, top + 44, bodyWidth, 2.5, 0.02, 0.70, 0.64);
        canvas.Text(model.CompanyName, left + 12, top + 8, compact ? 12.5 : 15, true, 1, 1, 1);
        canvas.WrappedText(model.CompanyAddress, left + 12, top + 26, bodyWidth * 0.58, compact ? 6 : 7, 2, false, 0.82, 0.88, 0.94);
        canvas.Text("TAX INVOICE", left + bodyWidth - (compact ? 130 : 170), top + 8, compact ? 10 : 12.5, true, 1, 1, 1);
        canvas.Text($"{copy} | Page {pageNumber}/{pageCount}", left + bodyWidth - (compact ? 130 : 170), top + 25, 7, false, 0.82, 0.88, 0.94);
        if (firstPage)
        {
            canvas.Qr(model.DocumentCode, left + bodyWidth - 45, top + 4, 38);
        }
        if (reprint)
        {
            canvas.Text("REPRINT", left + bodyWidth - (compact ? 130 : 170), top + 36, 7, true, 0.98, 0.45, 0.45);
        }

        var infoTop = top + 55;
        var infoWidth = (bodyWidth - 12) / 4;
        DrawInfoBox(canvas, left + 6, infoTop, infoWidth, "Invoice No.", model.InvoiceNumber);
        DrawInfoBox(canvas, left + 6 + infoWidth, infoTop, infoWidth, "Date", model.OnDate.ToString("dd MMM yyyy", CultureInfo.InvariantCulture));
        DrawInfoBox(canvas, left + 6 + infoWidth * 2, infoTop, infoWidth, "Status", model.InvoiceStatus);
        DrawInfoBox(canvas, left + 6 + infoWidth * 3, infoTop, infoWidth, "Amount", $"INR {model.BillAmount:N2}");

        if (!firstPage)
        {
            canvas.Text($"Continued from previous page - {model.InvoiceNumber}", left + 10, infoTop + 42, 7.5, true, 0.08, 0.12, 0.18);
            return;
        }

        var customerTop = infoTop + 42;
        canvas.FillRect(left + 6, customerTop, bodyWidth - 12, compact ? 32 : 38, 0.95, 0.97, 0.98);
        canvas.StrokeRect(left + 6, customerTop, bodyWidth - 12, compact ? 32 : 38, 0.35, 0.74, 0.78, 0.82);
        canvas.Text("Bill To", left + 12, customerTop + 6, 6.8, true, 0.25, 0.30, 0.36);
        canvas.Text(EmptyAsDash(model.CustomerName), left + 12, customerTop + 17, 8, true, 0.08, 0.12, 0.18);
        canvas.Text($"Mobile: {EmptyAsDash(model.CustomerMobileNumber)}", left + bodyWidth * 0.56, customerTop + 17, 7, false, 0.25, 0.30, 0.36);
        canvas.Text($"Store: {model.StoreName}", left + bodyWidth * 0.56, customerTop + 29, 7, false, 0.25, 0.30, 0.36);
    }

    private static void DrawSalesPageSummary(PdfCanvas canvas, InvoicePdfModel model, IReadOnlyList<ReceiptItemDto> items, double left, double height, double bodyWidth, bool compact, bool isLastPage)
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

        var totalLeft = left + bodyWidth - (compact ? 172 : 210);
        DrawTotals(canvas, totalLeft, summaryTop + (compact ? 16 : 22), compact ? 166 : 202, model, compact);
        canvas.WrappedText($"Amount in words: {AmountInWords(model.BillAmount)} only", left + 12, summaryTop + 30, Math.Max(100, totalLeft - left - 24), 7, compact ? 3 : 4, true);
        if (!string.IsNullOrWhiteSpace(model.Remarks))
        {
            canvas.WrappedText($"Remarks: {model.Remarks}", left + 12, summaryTop + (compact ? 42 : 46), Math.Max(100, totalLeft - left - 24), 7, compact ? 3 : 4, false);
        }
        var paymentSummary = model.Payments.Count == 0
            ? "Payment: -"
            : $"Payment: {string.Join(", ", model.Payments.Take(3).Select(item => $"{item.PaymentMode} INR {item.Amount:N2}"))}";
        canvas.WrappedText(paymentSummary, left + 12, summaryTop + (compact ? 88 : 104), Math.Max(100, totalLeft - left - 24), 6.8, 2);
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

    private static void DrawFooter(PdfCanvas canvas, InvoicePdfModel model, double left, double bodyWidth, double height, bool compact, int pageNumber, int pageCount)
    {
        if (!string.IsNullOrWhiteSpace(model.PolicyUrl))
        {
            canvas.CenteredText("Exchange only within 7 days with invoice and original unused goods. No cash refund. Policy: " + model.PolicyUrl, left + 8, height - (compact ? 33 : 40), bodyWidth - 16, compact ? 5.4 : 6.0, false, 0.36, 0.40, 0.45);
        }
        canvas.CenteredText($"Scan code: {model.InvoiceNumber} | Page {pageNumber}/{pageCount}", left + 8, height - (compact ? 24 : 30), bodyWidth - 16, 6.5, true, 0.08, 0.12, 0.18);
        var footer = string.Join(" | ", new[]
        {
            string.IsNullOrWhiteSpace(model.CompanyPhone) ? null : $"Phone: {model.CompanyPhone}",
            string.IsNullOrWhiteSpace(model.Gstin) ? null : $"GSTIN: {model.Gstin}",
            "Generated by Garmetix"
        }.Where(value => !string.IsNullOrWhiteSpace(value)));
        canvas.CenteredText(footer, left + 8, height - 15, bodyWidth - 16, 6.2, false, 0.36, 0.40, 0.45);
    }

    private static byte[] BuildThermal(InvoicePdfModel model, double width, string copy, bool reprint)
    {
        var lineHeight = width <= Thermal2Width ? 10.0 : 11.0;
        var height = Math.Max(420, 230 + model.Items.Count * (lineHeight * 3.2) + model.Payments.Count * lineHeight);
        var canvas = new PdfCanvas(height);
        var left = width <= Thermal2Width ? 7.0 : 10.0;
        var bodyWidth = width - left * 2;
        var top = 10.0;
        var font = width <= Thermal2Width ? 6.4 : 7.2;

        canvas.CenteredText(model.CompanyName, left, top, bodyWidth, font + 2, true, 0.02, 0.09, 0.16);
        top += lineHeight + 2;
        canvas.CenteredWrappedText(model.CompanyAddress, left, top, bodyWidth, font, 2);
        top += lineHeight * 2;
        canvas.CenteredText(model.StoreName, left, top, bodyWidth, font, false, 0.08, 0.12, 0.18);
        top += lineHeight;
        if (!string.IsNullOrWhiteSpace(model.Gstin))
        {
            canvas.CenteredText($"GSTIN: {model.Gstin}", left, top, bodyWidth, font, false, 0.08, 0.12, 0.18);
            top += lineHeight;
        }
        canvas.Line(left, top, left + bodyWidth, top, 0.4, 0.45, 0.49, 0.54);
        top += 5;
        canvas.CenteredText($"TAX INVOICE - {copy}", left, top, bodyWidth, font + 0.5, true, 0.02, 0.09, 0.16);
        top += lineHeight;
        if (reprint)
        {
            canvas.CenteredText("REPRINT", left, top, bodyWidth, font, true, 0.78, 0.20, 0.20);
            top += lineHeight;
        }
        canvas.Text($"No: {model.InvoiceNumber}", left, top, font, false, 0.08, 0.12, 0.18);
        top += lineHeight;
        canvas.Text(model.OnDate.ToString("dd/MM/yyyy hh:mm tt", CultureInfo.InvariantCulture), left, top, font, false, 0.08, 0.12, 0.18);
        top += lineHeight;
        canvas.WrappedText($"Customer: {EmptyAsDash(model.CustomerName)}", left, top, bodyWidth, font, 2);
        top += lineHeight * 2;
        canvas.Line(left, top, left + bodyWidth, top, 0.4, 0.45, 0.49, 0.54);
        top += 5;

        foreach (var item in model.Items)
        {
            canvas.WrappedText(ItemPrintName(item), left, top, bodyWidth, font, 3);
            top += lineHeight * 2.0;
            canvas.Text($"{item.Quantity:N2} x {item.Mrp:N2}", left, top, font, false, 0.08, 0.12, 0.18);
            canvas.RightText(item.Amount.ToString("N2", CultureInfo.InvariantCulture), left + bodyWidth, top, font, true, 0.08, 0.12, 0.18);
            top += lineHeight;
        }

        canvas.Line(left, top, left + bodyWidth, top, 0.4, 0.45, 0.49, 0.54);
        top += 6;
        DrawThermalTotal(canvas, left, bodyWidth, top, "MRP", model.MRP, font); top += lineHeight;
        DrawThermalTotal(canvas, left, bodyWidth, top, "Discount", model.DiscountAmount, font); top += lineHeight;
        DrawThermalTotal(canvas, left, bodyWidth, top, "CGST", model.Items.Sum(item => item.CgstAmount ?? 0), font); top += lineHeight;
        DrawThermalTotal(canvas, left, bodyWidth, top, "SGST", model.Items.Sum(item => item.SgstAmount ?? 0), font); top += lineHeight;
        DrawThermalTotal(canvas, left, bodyWidth, top, "IGST", model.Items.Sum(item => item.IgstAmount ?? 0), font); top += lineHeight;
        DrawThermalTotal(canvas, left, bodyWidth, top, "Tax", model.TaxAmount, font); top += lineHeight;
        DrawThermalTotal(canvas, left, bodyWidth, top, "Round off", model.RoundOff, font); top += lineHeight;
        DrawThermalTotal(canvas, left, bodyWidth, top, "Bill", model.BillAmount, font + 1, true); top += lineHeight + 2;
        DrawThermalTotal(canvas, left, bodyWidth, top, "Paid", model.PaidAmount, font); top += lineHeight;
        DrawThermalTotal(canvas, left, bodyWidth, top, "Balance", model.BalanceAmount, font); top += lineHeight + 3;

        var paymentText = model.Payments.Count == 0 ? "Payment: -" : string.Join(", ", model.Payments.Select(item => $"{item.PaymentMode} {item.Amount:N2}"));
        canvas.WrappedText(paymentText, left, top, bodyWidth, font, 3);
        top += lineHeight * 3;
        canvas.Line(left, top, left + bodyWidth, top, 0.4, 0.45, 0.49, 0.54);
        top += 5;
        canvas.CenteredWrappedText("Exchange only within 7 days with invoice and original unused goods. No cash refund.", left, top, bodyWidth, Math.Max(5.2, font - 0.8), 3);
        top += lineHeight * 2.5;
        if (!string.IsNullOrWhiteSpace(model.PolicyUrl))
        {
            canvas.CenteredWrappedText("Policy: " + model.PolicyUrl, left, top, bodyWidth, Math.Max(5.0, font - 1.0), 3);
            top += lineHeight * 2.5;
        }
        canvas.CenteredText("Thank you for shopping with us", left, top, bodyWidth, font, false, 0.08, 0.12, 0.18);
        top += lineHeight;
        canvas.CenteredText($"Scan: {model.InvoiceNumber}", left, top, bodyWidth, font, true, 0.08, 0.12, 0.18);
        top += lineHeight;
        canvas.CenteredText("Generated by Garmetix", left, top, bodyWidth, font - 0.5, false, 0.36, 0.40, 0.45);

        return BuildPdf(width, height, canvas.Content);
    }

    private static void DrawInfoBox(PdfCanvas canvas, double x, double top, double width, string label, string value)
    {
        canvas.StrokeRect(x, top, width, 33, 0.35, 0.74, 0.78, 0.82);
        canvas.Text(label, x + 5, top + 5, 6.5, false, 0.38, 0.43, 0.49);
        canvas.Text(TrimTo(value, 20), x + 5, top + 16, 7.8, true, 0.08, 0.12, 0.18);
    }

    private static void DrawHeader(PdfCanvas canvas, double x, double top, double width, double height, string[] headers, double[] columns)
    {
        canvas.FillRect(x, top, width, height, 0.91, 0.98, 0.97);
        canvas.StrokeRect(x, top, width, height, 0.35, 0.74, 0.78, 0.82);
        for (var index = 0; index < headers.Length; index++)
        {
            canvas.Text(headers[index], x + width * columns[index] + 4, top + 6, 7, true, 0.09, 0.43, 0.40);
        }
    }

    private static void DrawTotals(PdfCanvas canvas, double x, double top, double width, InvoicePdfModel model, bool compact)
    {
        var rows = new[]
        {
            ("MRP", model.MRP, false),
            ("Discount", model.DiscountAmount, false),
            ("Net taxable", model.NetAmount, false),
            ("CGST", model.Items.Sum(item => item.CgstAmount ?? 0), false),
            ("SGST", model.Items.Sum(item => item.SgstAmount ?? 0), false),
            ("IGST", model.Items.Sum(item => item.IgstAmount ?? 0), false),
            ("Tax", model.TaxAmount, false),
            ("Round off", model.RoundOff, false),
            ("Bill amount", model.BillAmount, true),
            ("Paid", model.PaidAmount, false),
            ("Balance", model.BalanceAmount, true)
        };

        var rowHeight = compact ? 8.4 : 11.1;
        var fontSize = compact ? 5.8 : 6.7;
        var padding = compact ? 6.0 : 10.0;
        var boxHeight = rows.Length * rowHeight + padding;
        canvas.StrokeRect(x, top, width, boxHeight, 0.35, 0.74, 0.78, 0.82);
        var y = top + (compact ? 4.0 : 7.0);
        foreach (var (label, amount, bold) in rows)
        {
            canvas.Text(label, x + 7, y, fontSize, bold, 0.08, 0.12, 0.18);
            canvas.RightText($"INR {amount:N2}", x + width - 7, y, fontSize, bold, 0.08, 0.12, 0.18);
            y += rowHeight;
        }
    }

    private static void DrawThermalTotal(PdfCanvas canvas, double left, double width, double top, string label, decimal value, double size, bool bold = false)
    {
        canvas.Text(label, left, top, size, bold, 0.08, 0.12, 0.18);
        canvas.RightText(value.ToString("N2", CultureInfo.InvariantCulture), left + width, top, size, bold, 0.08, 0.12, 0.18);
    }

    private static string ItemPrintName(ReceiptItemDto item)
    {
        var barcode = string.IsNullOrWhiteSpace(item.Barcode) ? "Barcode missing" : $"Barcode: {item.Barcode.Trim()}";
        var details = string.Join(" | ", new[]
        {
            barcode,
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
        "transport" => "Transport Copy",
        _ => "Customer Copy"
    };

    private static string EmptyAsDash(string? value) => string.IsNullOrWhiteSpace(value) ? "-" : value.Trim();

    private static string TrimTo(string value, int length)
    {
        value = value ?? string.Empty;
        return value.Length <= length ? value : $"{value[..Math.Max(0, length - 3)]}...";
    }

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
        if (number == 0)
        {
            return "zero";
        }

        var units = new[]
        {
            (Value: 10_000_000L, Name: "crore"),
            (Value: 100_000L, Name: "lakh"),
            (Value: 1_000L, Name: "thousand"),
            (Value: 100L, Name: "hundred")
        };
        var parts = new List<string>();
        foreach (var unit in units)
        {
            if (number < unit.Value)
            {
                continue;
            }

            parts.Add($"{IndianNumberWords(number / unit.Value)} {unit.Name}");
            number %= unit.Value;
        }

        if (number > 0)
        {
            if (parts.Count > 0)
            {
                parts.Add("and");
            }

            parts.Add(UnderHundred((int)number));
        }

        return string.Join(" ", parts);
    }

    private static string UnderHundred(int number)
    {
        string[] belowTwenty =
        [
            "zero", "one", "two", "three", "four", "five", "six", "seven", "eight", "nine",
            "ten", "eleven", "twelve", "thirteen", "fourteen", "fifteen", "sixteen", "seventeen",
            "eighteen", "nineteen"
        ];
        string[] tens = ["", "", "twenty", "thirty", "forty", "fifty", "sixty", "seventy", "eighty", "ninety"];
        if (number < 20)
        {
            return belowTwenty[number];
        }

        return number % 10 == 0 ? tens[number / 10] : $"{tens[number / 10]}-{belowTwenty[number % 10]}";
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
        {
            FillRect(left, top, size, size, 1, 1, 1);
            StrokeRect(left, top, size, size, 0.6, 0.08, 0.12, 0.18);
            CenteredText("SCAN", left, top + size * 0.32, size, Math.Max(5, size * 0.13), true, 0.08, 0.12, 0.18);
            CenteredText("CODE", left, top + size * 0.52, size, Math.Max(5, size * 0.13), true, 0.08, 0.12, 0.18);
        }

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
                    if (lines.Count == maxLines)
                    {
                        break;
                    }
                }

                if (current.Length > 0)
                {
                    current.Append(' ');
                }

                current.Append(word.Length > maxLength ? word[..maxLength] : word);
            }

            if (current.Length > 0 && lines.Count < maxLines)
            {
                lines.Add(current.ToString());
            }

            if (lines.Count > 0 && words.Length > 0 && string.Join(" ", lines).Length < value.Length - 2)
            {
                lines[^1] = TrimTo(lines[^1], Math.Max(4, maxLength - 3)) + "...";
            }

            return lines;
        }

        private static string Escape(string value)
        {
            return Sanitize(value).Replace("\\", "\\\\").Replace("(", "\\(").Replace(")", "\\)");
        }

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
