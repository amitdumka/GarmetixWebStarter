using System.Globalization;
using System.Text;
using Garmetix.Api.ProductLookup;

namespace Garmetix.Api.Purchase;

public sealed record PurchaseReturnPdfModel(
    PurchaseReturnDetailDto Return,
    string CompanyName,
    string CompanyAddress,
    string CompanyPhone,
    string CompanyGstin,
    string StoreName,
    string DocumentCode);

public static class PurchaseReturnPdfDocument
{
    private const double A4Width = 595.28;
    private const double A4Height = 841.89;
    private const double A5Width = 419.53;
    private const double A5Height = 595.28;

    public static byte[] Build(PurchaseReturnPdfModel model, string? format, string? copy, bool reprint, bool signatures)
    {
        var compact = string.Equals(format, "a5", StringComparison.OrdinalIgnoreCase)
            || string.Equals(format, "a5-one", StringComparison.OrdinalIgnoreCase);
        var width = compact ? A5Width : A4Width;
        var height = compact ? A5Height : A4Height;
        var rowsPerPage = compact ? 8 : 18;
        var pages = model.Return.Items
            .Select((item, index) => new { item, index })
            .GroupBy(row => row.index / rowsPerPage)
            .Select(group => group.Select(row => row.item).ToList())
            .ToList();
        if (pages.Count == 0)
        {
            pages.Add([]);
        }

        var contents = new List<string>();
        for (var index = 0; index < pages.Count; index++)
        {
            contents.Add(BuildPage(
                model,
                pages[index],
                width,
                height,
                compact,
                NormalizeCopy(copy),
                reprint,
                signatures,
                index + 1,
                pages.Count,
                index == 0,
                index == pages.Count - 1));
        }

        return BuildPdf(width, height, contents);
    }

    private static string BuildPage(
        PurchaseReturnPdfModel model,
        IReadOnlyList<PurchaseReturnItemDto> items,
        double width,
        double height,
        bool compact,
        string copy,
        bool reprint,
        bool signatures,
        int page,
        int pageCount,
        bool firstPage,
        bool lastPage)
    {
        var document = model.Return;
        var canvas = new PdfCanvas(height);
        var left = compact ? 18d : 28d;
        var top = compact ? 16d : 24d;
        var bodyWidth = width - left * 2;
        var rowHeight = compact ? 25d : 27d;

        canvas.StrokeRect(left, top, bodyWidth, height - top * 2, 0.8, 0.30, 0.36, 0.42);
        canvas.FillRect(left, top, bodyWidth, compact ? 58 : 66, 0.02, 0.09, 0.16);
        canvas.FillRect(left, top + (compact ? 58 : 66), bodyWidth, 3, 0.02, 0.70, 0.64);
        canvas.Text(model.CompanyName, left + 14, top + 9, compact ? 13 : 17, true, 1, 1, 1);
        canvas.WrappedText(model.CompanyAddress, left + 14, top + 28, bodyWidth * 0.50, compact ? 6.2 : 7, 3, false, 0.78, 0.84, 0.90);
        canvas.Text("PURCHASE RETURN", left + bodyWidth - (compact ? 190 : 245), top + 9, compact ? 11 : 14, true, 1, 1, 1);
        canvas.Text("DEBIT NOTE", left + bodyWidth - (compact ? 190 : 245), top + 25, compact ? 9 : 11, true, 0.10, 0.85, 0.77);
        canvas.Text(copy, left + bodyWidth - (compact ? 190 : 245), top + 42, 7.5, false, 0.84, 0.90, 0.95);
        if (reprint)
        {
            canvas.Text("REPRINT", left + bodyWidth - (compact ? 105 : 150), top + 42, 7.5, true, 0.98, 0.45, 0.45);
        }
        if (firstPage)
        {
            canvas.Qr(model.DocumentCode, left + bodyWidth - 49, top + 5, 43);
        }

        var infoTop = top + (compact ? 72 : 82);
        var infoWidth = (bodyWidth - 12) / 4;
        Info(canvas, left + 6, infoTop, infoWidth, "Return No.", document.ReturnNumber);
        Info(canvas, left + 6 + infoWidth, infoTop, infoWidth, "Return Date", document.OnDate.ToString("dd MMM yyyy", CultureInfo.InvariantCulture));
        Info(canvas, left + 6 + infoWidth * 2, infoTop, infoWidth, "Debit Note", Empty(document.DebitNoteNumber));
        Info(canvas, left + 6 + infoWidth * 3, infoTop, infoWidth, "Return Type", document.ReturnKind);

        var partyTop = infoTop + 43;
        canvas.FillRect(left + 6, partyTop, bodyWidth - 12, compact ? 48 : 54, 0.95, 0.97, 0.98);
        canvas.StrokeRect(left + 6, partyTop, bodyWidth - 12, compact ? 48 : 54, 0.35, 0.74, 0.78, 0.82);
        canvas.Text("Vendor", left + 12, partyTop + 7, 6.5, true, 0.34, 0.39, 0.45);
        canvas.Text(Trim(document.VendorName, compact ? 30 : 46), left + 12, partyTop + 19, 8, true);
        canvas.Text($"GSTIN: {Empty(document.VendorGstin)}", left + 12, partyTop + 33, 7, false, 0.25, 0.30, 0.36);
        canvas.Text($"Original purchase: {document.OriginalInvoiceNumber}", left + bodyWidth * 0.51, partyTop + 19, 7.2, true);
        canvas.Text($"Purchase date: {document.OriginalInvoiceDate:dd MMM yyyy}", left + bodyWidth * 0.51, partyTop + 33, 7, false, 0.25, 0.30, 0.36);
        canvas.Text($"Store: {model.StoreName}", left + bodyWidth * 0.51, partyTop + 45, 6.5, false, 0.25, 0.30, 0.36);

        var reasonTop = partyTop + (compact ? 56 : 63);
        canvas.FillRect(left + 6, reasonTop, bodyWidth - 12, compact ? 30 : 34, 0.99, 0.96, 0.86);
        canvas.StrokeRect(left + 6, reasonTop, bodyWidth - 12, compact ? 30 : 34, 0.35, 0.88, 0.69, 0.20);
        canvas.WrappedText($"Reason: {Empty(document.Reason)}", left + 12, reasonTop + 7, bodyWidth - 24, 6.8, compact ? 2 : 3, true, 0.38, 0.25, 0.04);

        var tableTop = reasonTop + (compact ? 40 : 45);
        var columns = compact
            ? new[] { 0d, 0.42, 0.53, 0.66, 0.80, 1d }
            : new[] { 0d, 0.34, 0.45, 0.55, 0.66, 0.78, 0.89, 1d };
        var headers = compact
            ? new[] { "Item / HSN", "Qty", "Rate", "GST", "Amount" }
            : new[] { "Item / HSN", "Qty", "Rate", "Discount", "Taxable", "GST", "Amount" };
        Header(canvas, left + 6, tableTop, bodyWidth - 12, rowHeight, headers, columns);
        tableTop += rowHeight;

        foreach (var item in items)
        {
            var tableWidth = bodyWidth - 12;
            canvas.StrokeRect(left + 6, tableTop, tableWidth, rowHeight, 0.25, 0.82, 0.85, 0.88);
            canvas.WrappedText(ItemName(item), left + 10, tableTop + 5, tableWidth * (compact ? 0.38 : 0.31), compact ? 6.2 : 6.8, 2);
            canvas.RightText(item.ReturnedQuantity.ToString("N2", CultureInfo.InvariantCulture), left + 6 + tableWidth * columns[2] - 4, tableTop + 7, 6.8, false);
            canvas.RightText(item.UnitRate.ToString("N2", CultureInfo.InvariantCulture), left + 6 + tableWidth * columns[3] - 4, tableTop + 7, 6.8, false);
            if (compact)
            {
                canvas.RightText($"{item.TaxRate:N2}%", left + 6 + tableWidth * columns[4] - 4, tableTop + 7, 6.8, false);
            }
            else
            {
                canvas.RightText(item.DiscountAmount.ToString("N2", CultureInfo.InvariantCulture), left + 6 + tableWidth * columns[4] - 4, tableTop + 7, 6.8, false);
                canvas.RightText(item.TaxableAmount.ToString("N2", CultureInfo.InvariantCulture), left + 6 + tableWidth * columns[5] - 4, tableTop + 7, 6.8, false);
                canvas.RightText(item.TaxAmount.ToString("N2", CultureInfo.InvariantCulture), left + 6 + tableWidth * columns[6] - 4, tableTop + 7, 6.8, false);
            }
            canvas.RightText(item.ReturnAmount.ToString("N2", CultureInfo.InvariantCulture), left + bodyWidth - 10, tableTop + 7, 6.8, true);
            tableTop += rowHeight;
        }

        if (lastPage)
        {
            var totalsTop = Math.Max(tableTop + 10, height - (compact ? 165 : 205));
            var totalsWidth = compact ? 174d : 210d;
            var totalsLeft = left + bodyWidth - totalsWidth - 6;
            var totals = new[]
            {
                ("Taxable reversal", document.TaxableAmount, false),
                ("CGST reversal", document.CgstAmount, false),
                ("SGST reversal", document.SgstAmount, false),
                ("IGST reversal", document.IgstAmount, false),
                ("Total GST reversal", document.TaxAmount, false),
                ("Debit note amount", document.ReturnAmount, true)
            };
            canvas.FillRect(totalsLeft, totalsTop, totalsWidth, totals.Length * 16 + 8, 0.94, 0.98, 0.97);
            canvas.StrokeRect(totalsLeft, totalsTop, totalsWidth, totals.Length * 16 + 8, 0.45, 0.14, 0.55, 0.51);
            var y = totalsTop + 7;
            foreach (var (label, amount, bold) in totals)
            {
                canvas.Text(label, totalsLeft + 7, y, 7, bold);
                canvas.RightText($"INR {amount:N2}", totalsLeft + totalsWidth - 7, y, 7, bold);
                y += 16;
            }

            var detailWidth = Math.Max(100, totalsLeft - left - 24);
            canvas.WrappedText(
                $"This document records goods returned to the vendor and the corresponding input-tax-credit reversal. Quantity: {document.Quantity:N2}. Items: {document.Items.Count}.",
                left + 12,
                totalsTop + 8,
                detailWidth,
                7,
                5);

            if (signatures)
            {
                var signatureTop = height - (compact ? 54 : 66);
                var labels = new[] { "Prepared by", "Checked by", "Vendor", "Authorized" };
                var signatureWidth = (bodyWidth - 24) / labels.Length;
                for (var index = 0; index < labels.Length; index++)
                {
                    var x = left + 12 + index * signatureWidth;
                    canvas.Line(x + 3, signatureTop, x + signatureWidth - 3, signatureTop, 0.45, 0.45, 0.49, 0.54);
                    canvas.CenteredText(labels[index], x, signatureTop + 6, signatureWidth, 6.5, false, 0.36, 0.40, 0.45);
                }
            }
        }

        canvas.Text($"Page {page} of {pageCount}", left + 10, height - 31, 6.5, false, 0.36, 0.40, 0.45);
        canvas.CenteredText($"Scan code: {document.ReturnNumber}", left + 8, height - 31, bodyWidth - 16, 6.8, true, 0.08, 0.12, 0.18);
        var footer = string.Join(" | ", new[]
        {
            string.IsNullOrWhiteSpace(model.CompanyPhone) ? null : $"Phone: {model.CompanyPhone}",
            string.IsNullOrWhiteSpace(model.CompanyGstin) ? null : $"GSTIN: {model.CompanyGstin}",
            "Generated by Garmetix"
        }.Where(value => !string.IsNullOrWhiteSpace(value)));
        canvas.CenteredText(footer, left + 8, height - 19, bodyWidth - 16, 6.3, false, 0.36, 0.40, 0.45);
        return canvas.Content;
    }

    private static void Info(PdfCanvas canvas, double x, double top, double width, string label, string value)
    {
        canvas.StrokeRect(x, top, width, 34, 0.35, 0.74, 0.78, 0.82);
        canvas.Text(label, x + 5, top + 5, 6.2, false, 0.38, 0.43, 0.49);
        canvas.Text(Trim(value, 20), x + 5, top + 17, 7.2, true);
    }

    private static void Header(PdfCanvas canvas, double x, double top, double width, double height, string[] headers, double[] columns)
    {
        canvas.FillRect(x, top, width, height, 0.02, 0.09, 0.16);
        for (var index = 0; index < headers.Length; index++)
        {
            canvas.Text(headers[index], x + width * columns[index] + 4, top + 8, 6.6, true, 1, 1, 1);
        }
    }

    private static string ItemName(PurchaseReturnItemDto item)
    {
        var suffix = string.Join(" | ", new[]
        {
            string.IsNullOrWhiteSpace(item.HsnCode) ? null : $"HSN {item.HsnCode}",
            string.IsNullOrWhiteSpace(item.Unit) ? null : item.Unit,
            string.IsNullOrWhiteSpace(item.Barcode) ? null : item.Barcode
        }.Where(value => !string.IsNullOrWhiteSpace(value)));
        return string.IsNullOrWhiteSpace(suffix) ? item.ProductName : $"{item.ProductName} | {suffix}";
    }

    private static string NormalizeCopy(string? value) => value?.Trim().ToLowerInvariant() switch
    {
        "office" => "Office Copy",
        "duplicate" => "Duplicate Copy",
        "supplier" => "Supplier Copy",
        _ => "Store Copy"
    };

    private static string Empty(string? value) => string.IsNullOrWhiteSpace(value) ? "-" : value.Trim();
    private static string Trim(string value, int length) => value.Length <= length ? value : $"{value[..Math.Max(0, length - 3)]}...";
    private static string F(double value) => value.ToString("0.##", CultureInfo.InvariantCulture);

    private static byte[] BuildPdf(double width, double height, IReadOnlyList<string> pageContents)
    {
        var pageCount = pageContents.Count;
        var normalFontObject = 3 + pageCount * 2;
        var boldFontObject = normalFontObject + 1;
        var objects = new List<string>
        {
            "<< /Type /Catalog /Pages 2 0 R >>",
            $"<< /Type /Pages /Kids [{string.Join(" ", Enumerable.Range(0, pageCount).Select(index => $"{3 + index * 2} 0 R"))}] /Count {pageCount} >>"
        };

        for (var index = 0; index < pageCount; index++)
        {
            var contentObject = 4 + index * 2;
            var bytes = Encoding.ASCII.GetBytes(pageContents[index]);
            objects.Add($"<< /Type /Page /Parent 2 0 R /MediaBox [0 0 {F(width)} {F(height)}] /Resources << /Font << /F1 {normalFontObject} 0 R /F2 {boldFontObject} 0 R >> >> /Contents {contentObject} 0 R >>");
            objects.Add($"<< /Length {bytes.Length} >>\nstream\n{pageContents[index]}endstream");
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

    private sealed class PdfCanvas(double pageHeight)
    {
        private readonly StringBuilder builder = new();
        public string Content => builder.ToString();

        public void Text(string value, double x, double top, double size, bool bold, double r = 0.08, double g = 0.12, double b = 0.18)
            => builder.Append(CultureInfo.InvariantCulture, $"BT /{(bold ? "F2" : "F1")} {F(size)} Tf {F(r)} {F(g)} {F(b)} rg 1 0 0 1 {F(x)} {F(pageHeight - top - size)} Tm ({Escape(value)}) Tj ET\n");

        public void RightText(string value, double right, double top, double size, bool bold)
            => Text(value, right - Sanitize(value).Length * size * (bold ? 0.55 : 0.5), top, size, bold);

        public void CenteredText(string value, double x, double top, double width, double size, bool bold, double r, double g, double b)
            => Text(value, x + Math.Max(0, (width - Sanitize(value).Length * size * (bold ? 0.55 : 0.5)) / 2), top, size, bold, r, g, b);

        public void WrappedText(string value, double x, double top, double width, double size, int maxLines, bool bold = false, double r = 0.08, double g = 0.12, double b = 0.18)
        {
            var max = Math.Max(8, (int)(width / (size * 0.52)));
            var words = Sanitize(value).Split(' ', StringSplitOptions.RemoveEmptyEntries);
            var lines = new List<string>();
            var current = new StringBuilder();
            foreach (var word in words)
            {
                if (current.Length > 0 && current.Length + word.Length + 1 > max)
                {
                    lines.Add(current.ToString());
                    current.Clear();
                    if (lines.Count == maxLines) break;
                }
                if (current.Length > 0) current.Append(' ');
                current.Append(word.Length > max ? word[..max] : word);
            }
            if (current.Length > 0 && lines.Count < maxLines) lines.Add(current.ToString());
            for (var index = 0; index < lines.Count; index++)
            {
                Text(lines[index], x, top + index * (size + 2), size, bold, r, g, b);
            }
        }

        public void FillRect(double x, double top, double width, double height, double r, double g, double b)
            => builder.Append(CultureInfo.InvariantCulture, $"{F(r)} {F(g)} {F(b)} rg {F(x)} {F(pageHeight - top - height)} {F(width)} {F(height)} re f\n");

        public void StrokeRect(double x, double top, double width, double height, double lineWidth, double r, double g, double b)
            => builder.Append(CultureInfo.InvariantCulture, $"{F(r)} {F(g)} {F(b)} RG {F(lineWidth)} w {F(x)} {F(pageHeight - top - height)} {F(width)} {F(height)} re S\n");

        public void Line(double x1, double top1, double x2, double top2, double lineWidth, double r, double g, double b)
            => builder.Append(CultureInfo.InvariantCulture, $"{F(r)} {F(g)} {F(b)} RG {F(lineWidth)} w {F(x1)} {F(pageHeight - top1)} m {F(x2)} {F(pageHeight - top2)} l S\n");

        public void Qr(string payload, double left, double top, double size)
            => DocumentCodeService.AppendPdfCommands(builder, pageHeight, left, top, size, payload);

        private static string Escape(string value) => Sanitize(value).Replace("\\", "\\\\").Replace("(", "\\(").Replace(")", "\\)");
        private static string Sanitize(string value) => new((value ?? string.Empty).Select(character => character is >= ' ' and <= '~' ? character : '?').ToArray());
    }
}
