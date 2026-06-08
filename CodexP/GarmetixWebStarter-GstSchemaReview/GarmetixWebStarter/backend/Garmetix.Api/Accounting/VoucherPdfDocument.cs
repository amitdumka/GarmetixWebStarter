using System.Globalization;
using System.Text;

namespace Garmetix.Api.Accounting;

public sealed record VoucherPdfModel(
    string CompanyName,
    string CompanyAddress,
    string CompanyPhone,
    string Gstin,
    string StoreName,
    string VoucherNumber,
    DateTime OnDate,
    string VoucherType,
    string PartyName,
    string Particulars,
    decimal Amount,
    string? Remarks,
    string? SlipNumber,
    string PaymentMode,
    string? PaymentDetails,
    string LedgerName,
    string EmployeeName,
    string BankAccount);

public static class VoucherPdfDocument
{
    private const double A4Width = 595.28;
    private const double A4Height = 841.89;
    private const double A5Width = 419.53;
    private const double A5Height = 595.28;

    public static byte[] Build(VoucherPdfModel model, bool a5SingleCopy, bool reprint, bool signatures)
    {
        var width = a5SingleCopy ? A5Width : A4Width;
        var height = a5SingleCopy ? A5Height : A4Height;
        var canvas = new PdfCanvas(height);

        if (a5SingleCopy)
        {
            DrawVoucher(canvas, model, 20, 18, width - 40, height - 36, "Voucher Copy", reprint, signatures);
        }
        else
        {
            var copyHeight = (height - 44) / 2;
            DrawVoucher(canvas, model, 22, 16, width - 44, copyHeight, "Office Copy", reprint, signatures);
            canvas.DashedLine(22, 421, width - 22, 421, 0.7, "6 4");
            DrawVoucher(canvas, model, 22, 429, width - 44, copyHeight, "Recipient Copy", reprint, signatures);
        }

        return BuildPdf(width, height, canvas.Content);
    }

    private static void DrawVoucher(
        PdfCanvas canvas,
        VoucherPdfModel model,
        double left,
        double top,
        double width,
        double height,
        string copyLabel,
        bool reprint,
        bool signatures)
    {
        const double navyR = 0.02;
        const double navyG = 0.09;
        const double navyB = 0.16;
        const double tealR = 0.02;
        const double tealG = 0.70;
        const double tealB = 0.64;

        canvas.StrokeRect(left, top, width, height, 0.8, 0.32, 0.38, 0.44);
        canvas.FillRect(left, top, width, 46, navyR, navyG, navyB);
        canvas.FillRect(left, top + 46, width, 3, tealR, tealG, tealB);
        canvas.Text(model.CompanyName, left + 12, top + 10, 15, true, 1, 1, 1);
        canvas.Text($"{model.StoreName} | {model.VoucherType} Voucher", left + 12, top + 29, 8.5, false, 0.8, 0.86, 0.91);
        canvas.Text(copyLabel, left + width - 84, top + 12, 9, true, 1, 1, 1);
        if (reprint)
        {
            canvas.Text("REPRINT", left + width - 85, top + 30, 8, true, 0.98, 0.45, 0.45);
        }

        var infoTop = top + 58;
        var columnWidth = (width - 12) / 4;
        DrawInfoBox(canvas, left + 6, infoTop, columnWidth, "Voucher No.", model.VoucherNumber);
        DrawInfoBox(canvas, left + 6 + columnWidth, infoTop, columnWidth, "Date", model.OnDate.ToString("dd MMM yyyy", CultureInfo.InvariantCulture));
        DrawInfoBox(canvas, left + 6 + columnWidth * 2, infoTop, columnWidth, "Mode", model.PaymentMode);
        DrawInfoBox(canvas, left + 6 + columnWidth * 3, infoTop, columnWidth, "Amount", $"INR {model.Amount:N2}");

        var tableTop = infoTop + 40;
        var labelWidth = Math.Min(116, width * 0.27);
        var rowHeight = aRowHeight(height);
        var details = new List<(string Label, string Value)>
        {
            ("Party", model.PartyName),
            ("Ledger", model.LedgerName),
            ("Issued by", model.EmployeeName),
            ("Bank account", model.BankAccount),
            ("Payment details", EmptyAsDash(model.PaymentDetails))
        };
        if (string.Equals(model.PaymentMode, "Cash", StringComparison.OrdinalIgnoreCase))
        {
            details.Add(("Slip number", EmptyAsDash(model.SlipNumber)));
        }
        details.Add(("Particulars", model.Particulars));
        details.Add(("Remarks", EmptyAsDash(model.Remarks)));

        foreach (var (label, value) in details)
        {
            canvas.FillRect(left + 6, tableTop, labelWidth, rowHeight, 0.95, 0.97, 0.98);
            canvas.StrokeRect(left + 6, tableTop, width - 12, rowHeight, 0.35, 0.72, 0.75, 0.79);
            canvas.Text(label, left + 11, tableTop + 7, 7.5, true, 0.25, 0.30, 0.36);
            canvas.WrappedText(value, left + labelWidth + 12, tableTop + 6, width - labelWidth - 24, 7.5, 2);
            tableTop += rowHeight;
        }

        var amountTop = tableTop + 7;
        canvas.FillRect(left + 6, amountTop, width - 12, 35, 0.91, 0.98, 0.97);
        canvas.Text("Amount in words", left + 12, amountTop + 6, 7, true, 0.09, 0.43, 0.40);
        canvas.WrappedText($"{AmountInWords(model.Amount)} only", left + 12, amountTop + 17, width - 135, 8, 2, true);
        canvas.Text($"INR {model.Amount:N2}", left + width - 116, amountTop + 16, 11, true, 0.02, 0.31, 0.29);

        var signatureTop = top + height - 46;
        if (signatures)
        {
            var signatureWidth = (width - 24) / 4;
            var labels = new[] { "Prepared by", "Checked by", "Approved by", "Received by" };
            for (var index = 0; index < labels.Length; index++)
            {
                var x = left + 6 + index * signatureWidth;
                canvas.Line(x + 4, signatureTop, x + signatureWidth - 4, signatureTop, 0.5, 0.45, 0.49, 0.54);
                canvas.CenteredText(labels[index], x, signatureTop + 6, signatureWidth, 7, false, 0.36, 0.40, 0.45);
            }
        }

        var footer = string.Join(" | ", new[]
        {
            model.CompanyAddress,
            string.IsNullOrWhiteSpace(model.CompanyPhone) ? null : $"Phone: {model.CompanyPhone}",
            string.IsNullOrWhiteSpace(model.Gstin) ? null : $"GSTIN: {model.Gstin}"
        }.Where(value => !string.IsNullOrWhiteSpace(value)));
        canvas.CenteredText(footer.Length == 0 ? "Generated by Garmetix" : footer, left + 8, top + height - 15, width - 16, 6.5, false, 0.36, 0.40, 0.45);

        static double aRowHeight(double copyHeight) => copyHeight < 430 ? 22 : 28;
    }

    private static void DrawInfoBox(PdfCanvas canvas, double x, double top, double width, string label, string value)
    {
        canvas.StrokeRect(x, top, width, 33, 0.35, 0.74, 0.78, 0.82);
        canvas.Text(label, x + 5, top + 5, 6.5, false, 0.38, 0.43, 0.49);
        canvas.Text(TrimTo(value, 22), x + 5, top + 16, 8, true, 0.08, 0.12, 0.18);
    }

    private static string EmptyAsDash(string? value) => string.IsNullOrWhiteSpace(value) ? "-" : value.Trim();

    private static string TrimTo(string value, int length)
    {
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
    {
        var streamBytes = Encoding.ASCII.GetBytes(content);
        var objects = new[]
        {
            "<< /Type /Catalog /Pages 2 0 R >>",
            "<< /Type /Pages /Kids [3 0 R] /Count 1 >>",
            $"<< /Type /Page /Parent 2 0 R /MediaBox [0 0 {F(width)} {F(height)}] /Resources << /Font << /F1 5 0 R /F2 6 0 R >> >> /Contents 4 0 R >>",
            $"<< /Length {streamBytes.Length} >>\nstream\n{content}endstream",
            "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica /Encoding /WinAnsiEncoding >>",
            "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica-Bold /Encoding /WinAnsiEncoding >>"
        };

        using var output = new MemoryStream();
        Write(output, "%PDF-1.4\n%Garmetix\n");
        var offsets = new List<long> { 0 };
        for (var index = 0; index < objects.Length; index++)
        {
            offsets.Add(output.Position);
            Write(output, $"{index + 1} 0 obj\n{objects[index]}\nendobj\n");
        }

        var xrefOffset = output.Position;
        Write(output, $"xref\n0 {objects.Length + 1}\n0000000000 65535 f \n");
        for (var index = 1; index < offsets.Count; index++)
        {
            Write(output, $"{offsets[index]:D10} 00000 n \n");
        }

        Write(output, $"trailer\n<< /Size {objects.Length + 1} /Root 1 0 R >>\nstartxref\n{xrefOffset}\n%%EOF");
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

        public void CenteredText(string value, double x, double top, double width, double size, bool bold, double r, double g, double b)
        {
            var estimatedWidth = Sanitize(value).Length * size * (bold ? 0.55 : 0.5);
            Text(value, x + Math.Max(0, (width - estimatedWidth) / 2), top, size, bold, r, g, b);
        }

        public void WrappedText(string value, double x, double top, double width, double size, int maxLines, bool bold = false)
        {
            var charsPerLine = Math.Max(8, (int)(width / (size * 0.52)));
            var lines = Wrap(Sanitize(value), charsPerLine, maxLines);
            for (var index = 0; index < lines.Count; index++)
            {
                Text(lines[index], x, top + index * (size + 2), size, bold, 0.08, 0.12, 0.18);
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

        public void DashedLine(double x1, double top1, double x2, double top2, double lineWidth, string dash)
        {
            builder.Append(CultureInfo.InvariantCulture, $"0.45 0.49 0.54 RG {F(lineWidth)} w [{dash}] 0 d {F(x1)} {F(pageHeight - top1)} m {F(x2)} {F(pageHeight - top2)} l S [] 0 d\n");
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
            var builder = new StringBuilder(value.Length);
            foreach (var character in value)
            {
                builder.Append(character is >= ' ' and <= '~' ? character : '?');
            }

            return builder.ToString();
        }
    }
}
