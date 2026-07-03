using System.Globalization;
using System.Text;
using Garmetix.Api.ProductLookup;

namespace Garmetix.Api.NonGstGoods;

public static class NonGstGoodsPdfDocument
{
    private const double A4Width = 595.28;
    private const double A4Height = 841.89;
    private const double A5Width = 419.53;
    private const double A5Height = 595.28;

    public static byte[] Build(NonGstPrintDto model, string format, bool reprint)
    {
        var compact = string.Equals(format, "a5", StringComparison.OrdinalIgnoreCase)
            || string.Equals(format, "a5-one", StringComparison.OrdinalIgnoreCase);
        var width = compact ? A5Width : A4Width;
        var height = compact ? A5Height : A4Height;
        var rowsPerPage = compact ? 10 : 22;
        var pages = model.Items
            .Select((item, index) => new { item, index })
            .GroupBy(row => row.index / rowsPerPage)
            .Select(group => group.Select(row => row.item).ToList())
            .ToList();
        if (pages.Count == 0)
        {
            pages.Add([]);
        }

        var contents = new List<string>();
        for (var pageIndex = 0; pageIndex < pages.Count; pageIndex++)
        {
            contents.Add(BuildPage(
                model,
                pages[pageIndex],
                width,
                height,
                compact,
                reprint,
                pageIndex + 1,
                pages.Count,
                pageIndex == 0,
                pageIndex == pages.Count - 1));
        }

        return BuildPdf(width, height, contents);
    }

    private static string BuildPage(
        NonGstPrintDto model,
        IReadOnlyList<NonGstPrintItemDto> items,
        double width,
        double height,
        bool compact,
        bool reprint,
        int page,
        int pageCount,
        bool firstPage,
        bool lastPage)
    {
        var canvas = new PdfCanvas(height);
        var left = compact ? 18d : 28d;
        var top = compact ? 16d : 24d;
        var bodyWidth = width - left * 2;
        var rowHeight = compact ? 24d : 25d;

        canvas.StrokeRect(left, top, bodyWidth, height - top * 2, 0.8, 0.30, 0.36, 0.42);
        canvas.FillRect(left, top, bodyWidth, compact ? 54 : 62, 0.02, 0.09, 0.16);
        canvas.FillRect(left, top + (compact ? 54 : 62), bodyWidth, 3, 0.02, 0.70, 0.64);
        canvas.Text(model.CompanyName, left + 14, top + 10, compact ? 14 : 17, true, 1, 1, 1);
        canvas.Text(model.StoreName, left + 14, top + 30, compact ? 8 : 9, true, 0.84, 0.90, 0.95);
        canvas.WrappedText(model.StoreAddress, left + 14, top + 41, bodyWidth * 0.52, compact ? 6.2 : 7, 2, false, 0.78, 0.84, 0.90);
        canvas.Text($"OFF BOOK {model.DocumentType.ToUpperInvariant()}", left + bodyWidth - (compact ? 180 : 230), top + 11, compact ? 11 : 14, true, 1, 1, 1);
        canvas.Text(model.DocumentNumber, left + bodyWidth - (compact ? 180 : 230), top + 31, compact ? 7.5 : 9, true, 0.84, 0.90, 0.95);
        if (reprint)
        {
            canvas.Text("REPRINT", left + bodyWidth - (compact ? 180 : 230), top + 44, 7.5, true, 0.98, 0.45, 0.45);
        }
        if (firstPage)
        {
            canvas.Qr(DocumentCodeService.Create(DocumentCodeService.NonGstGoods, model.Id), left + bodyWidth - 50, top + 5, 44);
        }

        var infoTop = top + (compact ? 68 : 78);
        var infoWidth = (bodyWidth - 12) / 4;
        Info(canvas, left + 6, infoTop, infoWidth, "Date", model.OnDate.ToString("dd MMM yyyy", CultureInfo.InvariantCulture));
        Info(canvas, left + 6 + infoWidth, infoTop, infoWidth, "Party", model.PartyName);
        Info(canvas, left + 6 + infoWidth * 2, infoTop, infoWidth, "Payment", model.PaymentMode);
        Info(canvas, left + 6 + infoWidth * 3, infoTop, infoWidth, "Status", model.PaymentStatus);

        var noteTop = infoTop + 42;
        canvas.FillRect(left + 6, noteTop, bodyWidth - 12, compact ? 32 : 38, 0.96, 0.93, 0.82);
        canvas.StrokeRect(left + 6, noteTop, bodyWidth - 12, compact ? 32 : 38, 0.35, 0.86, 0.66, 0.18);
        canvas.WrappedText(model.TaxNote, left + 12, noteTop + 7, bodyWidth - 24, compact ? 6.2 : 7, compact ? 3 : 2, true, 0.40, 0.25, 0.04);

        var tableTop = noteTop + (compact ? 42 : 50);
        var columns = new[] { 0d, 0.43, 0.55, 0.68, 0.81, 1d };
        Header(canvas, left + 6, tableTop, bodyWidth - 12, rowHeight, ["Item", "Qty", "Rate", "Discount", "Amount"], columns);
        tableTop += rowHeight;

        foreach (var item in items)
        {
            canvas.StrokeRect(left + 6, tableTop, bodyWidth - 12, rowHeight, 0.25, 0.82, 0.85, 0.88);
            canvas.WrappedText($"{item.ProductName} | {item.Barcode}", left + 10, tableTop + 5, (bodyWidth - 12) * 0.40, compact ? 6.4 : 7, 2);
            canvas.RightText(item.Quantity.ToString("N2", CultureInfo.InvariantCulture), left + 6 + (bodyWidth - 12) * columns[2] - 4, tableTop + 6, 7, false);
            canvas.RightText(item.Rate.ToString("N2", CultureInfo.InvariantCulture), left + 6 + (bodyWidth - 12) * columns[3] - 4, tableTop + 6, 7, false);
            canvas.RightText(item.DiscountAmount.ToString("N2", CultureInfo.InvariantCulture), left + 6 + (bodyWidth - 12) * columns[4] - 4, tableTop + 6, 7, false);
            canvas.RightText(item.Amount.ToString("N2", CultureInfo.InvariantCulture), left + bodyWidth - 10, tableTop + 6, 7, true);
            tableTop += rowHeight;
        }

        if (lastPage)
        {
            var totalsTop = Math.Max(tableTop + 12, height - (compact ? 154 : 178));
            var totalsLeft = left + bodyWidth - (compact ? 176 : 210);
            var totalsWidth = compact ? 170 : 202;
            var totals = new[]
            {
                ("Gross", model.GrossAmount, false),
                ("Discount", model.DiscountAmount, false),
                ("Net amount", model.NetAmount, true),
                ("Paid", model.PaidAmount, false),
                ("Balance", model.BalanceAmount, true)
            };
            canvas.StrokeRect(totalsLeft, totalsTop, totalsWidth, totals.Length * 18 + 8, 0.45, 0.35, 0.42, 0.48);
            var y = totalsTop + 7;
            foreach (var (label, amount, bold) in totals)
            {
                canvas.Text(label, totalsLeft + 7, y, 7.5, bold, 0.08, 0.12, 0.18);
                canvas.RightText($"INR {amount:N2}", totalsLeft + totalsWidth - 7, y, 7.5, bold);
                y += 18;
            }

            var detailsWidth = Math.Max(100, totalsLeft - left - 24);
            canvas.WrappedText($"Reference: {Empty(model.ReferenceNumber)}", left + 12, totalsTop + 8, detailsWidth, 7, 2, true);
            canvas.WrappedText($"Remarks: {Empty(model.Remarks)}", left + 12, totalsTop + 32, detailsWidth, 7, 4);
        }

        canvas.Text($"Page {page} of {pageCount}", left + 10, height - 34, 6.8, false, 0.36, 0.40, 0.45);
        canvas.CenteredText($"Scan code: {model.DocumentNumber}", left + 8, height - 34, bodyWidth - 16, 7, true, 0.08, 0.12, 0.18);
        canvas.RightText("Generated by Garmetix", left + bodyWidth - 10, height - 34, 6.8, false);
        return canvas.Content;
    }

    private static void Info(PdfCanvas canvas, double x, double top, double width, string label, string value)
    {
        canvas.StrokeRect(x, top, width, 33, 0.35, 0.74, 0.78, 0.82);
        canvas.Text(label, x + 5, top + 5, 6.4, false, 0.38, 0.43, 0.49);
        canvas.Text(Trim(value, 22), x + 5, top + 17, 7.4, true, 0.08, 0.12, 0.18);
    }

    private static void Header(PdfCanvas canvas, double x, double top, double width, double height, string[] headers, double[] columns)
    {
        canvas.FillRect(x, top, width, height, 0.91, 0.98, 0.97);
        canvas.StrokeRect(x, top, width, height, 0.35, 0.74, 0.78, 0.82);
        for (var index = 0; index < headers.Length; index++)
        {
            canvas.Text(headers[index], x + width * columns[index] + 4, top + 7, 7, true, 0.09, 0.43, 0.40);
        }
    }

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

    private static string Empty(string? value) => string.IsNullOrWhiteSpace(value) ? "-" : value.Trim();
    private static string Trim(string value, int length) => value.Length <= length ? value : $"{value[..Math.Max(0, length - 3)]}...";
    private static string F(double value) => value.ToString("0.##", CultureInfo.InvariantCulture);

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

        public void Qr(string payload, double left, double top, double size)
            => DocumentCodeService.AppendPdfCommands(builder, pageHeight, left, top, size, payload);

        private static string Escape(string value) => Sanitize(value).Replace("\\", "\\\\").Replace("(", "\\(").Replace(")", "\\)");
        private static string Sanitize(string value) => new((value ?? string.Empty).Select(character => character is >= ' ' and <= '~' ? character : '?').ToArray());
    }
}
