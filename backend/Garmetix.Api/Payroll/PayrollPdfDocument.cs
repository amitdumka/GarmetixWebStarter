using System.Globalization;
using System.Text;
using Garmetix.Api.ProductLookup;

namespace Garmetix.Api.Payroll;

public sealed record SalaryPaymentPdfModel(
    Guid Id,
    string CompanyName,
    string CompanyAddress,
    string StoreName,
    string EmployeeName,
    string VoucherNumber,
    int SalaryMonth,
    DateTime OnDate,
    decimal GrossSalary,
    decimal TotalDeductions,
    decimal NetSalary,
    decimal Amount,
    string PaymentMode,
    string Remarks);

public static class PayrollPdfDocument
{
    private const double A4Width = 595.28;
    private const double A4Height = 841.89;
    private const double A5Width = 419.53;
    private const double A5Height = 595.28;

    public static byte[] BuildPayslip(PayslipPrintDto model)
    {
        var canvas = new PdfCanvas(A4Height);
        const double left = 30;
        const double top = 24;
        const double width = A4Width - 60;
        canvas.Border(left, top, width, A4Height - 48);
        Header(canvas, left, top, width, model.CompanyName, model.CompanyAddress, "SALARY PAYSLIP",
            DocumentCodeService.Create(DocumentCodeService.Payslip, model.Summary.Id));

        var infoTop = top + 72;
        Info(canvas, left + 8, infoTop, width / 2 - 10, "Employee", model.Summary.EmployeeName);
        Info(canvas, left + width / 2, infoTop, width / 2 - 8, "Salary month", model.Summary.MonthYear);
        Info(canvas, left + 8, infoTop + 35, width / 2 - 10, "Pay period", $"{model.Summary.PayPeriodStart:dd MMM yyyy} - {model.Summary.PayPeriodEnd:dd MMM yyyy}");
        Info(canvas, left + width / 2, infoTop + 35, width / 2 - 8, "Attendance", $"{model.Summary.BillableDays:0.##} / {model.Summary.WorkingDays:0.##} days");

        var tableTop = infoTop + 84;
        var columnWidth = (width - 22) / 2;
        DrawSection(canvas, left + 8, tableTop, columnWidth, "EARNINGS",
        [
            ("Basic salary", model.BasicSalary),
            ("House rent allowance", model.Hra),
            ("Special allowance", model.SpecialAllowance),
            ("Conveyance allowance", model.ConveyanceAllowance),
            ("Incentives", model.Incentives),
            ("Other earnings", model.OtherEarnings)
        ], model.Summary.TotalEarnings);
        DrawSection(canvas, left + 14 + columnWidth, tableTop, columnWidth, "DEDUCTIONS",
        [
            ("Provident fund", model.ProvidentFund),
            ("Gratuity", model.Gratuity),
            ("Professional tax", model.ProfessionalTax),
            ("Income tax", model.IncomeTax),
            ("Other deductions", model.OtherDeductions),
            ("Deductions", model.Deductions)
        ], model.Summary.TotalDeductions);

        var settlementTop = tableTop + 205;
        canvas.Fill(left + 8, settlementTop, width - 16, 104, 0.94, 0.98, 0.97);
        canvas.Stroke(left + 8, settlementTop, width - 16, 104, 0.4, 0.57, 0.66, 0.65);
        var settlementRows = new (string, decimal)[]
        {
            ("Net salary", model.Summary.NetSalary),
            ("Previous due", model.Summary.CarryForwardDue),
            ("Salary advance adjusted", model.Summary.SalaryAdvance),
            ("Paid amount", model.Summary.PaidAmount),
            ("Balance due", model.Summary.DueAmount)
        };
        for (var index = 0; index < settlementRows.Length; index++)
        {
            var y = settlementTop + 10 + index * 18;
            canvas.Text(settlementRows[index].Item1, left + 18, y, 8, index is 0 or 4);
            canvas.Right(settlementRows[index].Item2.ToString("N2", CultureInfo.InvariantCulture), left + width - 18, y, 8, index is 0 or 4);
        }

        canvas.Text($"Status: {model.Summary.Status}", left + 12, settlementTop + 113, 8, true);
        canvas.Wrapped(model.Remarks ?? "Computer-generated salary payslip.", left + 12, settlementTop + 130, width - 24, 8, 3);
        Signatures(canvas, left + 8, A4Height - 94, width - 16, ["Prepared by", "Employee", "Authorized signatory"]);
        canvas.Centered("This is a system-generated payroll document from Garmetix.", left, A4Height - 39, width, 7);
        return BuildPdf(A4Width, A4Height, canvas.Content);
    }

    public static byte[] BuildSalaryPayment(SalaryPaymentPdfModel model)
    {
        var canvas = new PdfCanvas(A5Height);
        const double left = 20;
        const double top = 18;
        const double width = A5Width - 40;
        canvas.Border(left, top, width, A5Height - 36);
        Header(canvas, left, top, width, model.CompanyName, model.CompanyAddress, "SALARY PAYMENT SLIP",
            DocumentCodeService.Create(DocumentCodeService.SalaryPayment, model.Id));

        var y = top + 76;
        var rows = new (string, string)[]
        {
            ("Voucher number", model.VoucherNumber),
            ("Employee", model.EmployeeName),
            ("Salary month", model.SalaryMonth.ToString(CultureInfo.InvariantCulture)),
            ("Payment date", model.OnDate.ToString("dd MMM yyyy", CultureInfo.InvariantCulture)),
            ("Payment mode", model.PaymentMode),
            ("Gross salary", $"INR {model.GrossSalary:N2}"),
            ("Deductions", $"INR {model.TotalDeductions:N2}"),
            ("Net salary", $"INR {model.NetSalary:N2}"),
            ("Amount paid", $"INR {model.Amount:N2}"),
            ("Remarks", string.IsNullOrWhiteSpace(model.Remarks) ? "-" : model.Remarks)
        };
        foreach (var row in rows)
        {
            canvas.Fill(left + 8, y, 105, 27, 0.95, 0.97, 0.98);
            canvas.Stroke(left + 8, y, width - 16, 27, 0.3, 0.75, 0.79, 0.82);
            canvas.Text(row.Item1, left + 14, y + 8, 7.5, true);
            canvas.Wrapped(row.Item2, left + 122, y + 7, width - 140, 8, 2);
            y += 27;
        }

        Signatures(canvas, left + 8, A5Height - 78, width - 16, ["Prepared by", "Employee", "Authorized"]);
        canvas.Centered($"{model.StoreName} | Generated by Garmetix", left, A5Height - 34, width, 7);
        return BuildPdf(A5Width, A5Height, canvas.Content);
    }

    private static void Header(PdfCanvas canvas, double left, double top, double width, string company, string address, string title, string code)
    {
        canvas.Fill(left, top, width, 56, 0.02, 0.09, 0.16);
        canvas.Fill(left, top + 56, width, 3, 0.02, 0.70, 0.64);
        canvas.Text(company, left + 12, top + 10, 16, true, 1, 1, 1);
        canvas.Wrapped(address, left + 12, top + 31, width - 170, 7, 2, false, 0.82, 0.88, 0.94);
        canvas.Text(title, left + width - 177, top + 11, 11, true, 1, 1, 1);
        canvas.Qr(code, left + width - 48, top + 6, 42);
    }

    private static void Info(PdfCanvas canvas, double left, double top, double width, string label, string value)
    {
        canvas.Stroke(left, top, width, 30, 0.35, 0.74, 0.78, 0.82);
        canvas.Text(label, left + 6, top + 5, 6.5, false, 0.4, 0.44, 0.5);
        canvas.Text(value, left + 6, top + 16, 8, true);
    }

    private static void DrawSection(PdfCanvas canvas, double left, double top, double width, string title, IReadOnlyList<(string, decimal)> rows, decimal total)
    {
        canvas.Fill(left, top, width, 25, 0.02, 0.09, 0.16);
        canvas.Text(title, left + 8, top + 8, 8, true, 1, 1, 1);
        var y = top + 25;
        foreach (var row in rows)
        {
            canvas.Stroke(left, y, width, 25, 0.25, 0.82, 0.85, 0.88);
            canvas.Text(row.Item1, left + 8, y + 8, 7.5);
            canvas.Right(row.Item2.ToString("N2", CultureInfo.InvariantCulture), left + width - 8, y + 8, 7.5);
            y += 25;
        }
        canvas.Fill(left, y, width, 27, 0.91, 0.98, 0.97);
        canvas.Text("TOTAL", left + 8, y + 8, 8, true);
        canvas.Right(total.ToString("N2", CultureInfo.InvariantCulture), left + width - 8, y + 8, 8, true);
    }

    private static void Signatures(PdfCanvas canvas, double left, double top, double width, IReadOnlyList<string> labels)
    {
        var itemWidth = width / labels.Count;
        for (var index = 0; index < labels.Count; index++)
        {
            var x = left + itemWidth * index;
            canvas.Line(x + 8, top, x + itemWidth - 8, top);
            canvas.Centered(labels[index], x, top + 7, itemWidth, 7);
        }
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
        var xref = output.Position;
        Write(output, $"xref\n0 {objects.Length + 1}\n0000000000 65535 f \n");
        foreach (var offset in offsets.Skip(1)) Write(output, $"{offset:D10} 00000 n \n");
        Write(output, $"trailer\n<< /Size {objects.Length + 1} /Root 1 0 R >>\nstartxref\n{xref}\n%%EOF");
        return output.ToArray();
    }

    private static void Write(Stream stream, string value)
    {
        var bytes = Encoding.ASCII.GetBytes(value);
        stream.Write(bytes);
    }

    private static string F(double value) => value.ToString("0.##", CultureInfo.InvariantCulture);

    private sealed class PdfCanvas(double pageHeight)
    {
        private readonly StringBuilder builder = new();
        public string Content => builder.ToString();

        public void Text(string value, double x, double top, double size, bool bold = false, double r = 0.08, double g = 0.12, double b = 0.18)
            => builder.Append(CultureInfo.InvariantCulture, $"BT /{(bold ? "F2" : "F1")} {F(size)} Tf {F(r)} {F(g)} {F(b)} rg 1 0 0 1 {F(x)} {F(pageHeight - top - size)} Tm ({Escape(value)}) Tj ET\n");
        public void Right(string value, double right, double top, double size, bool bold = false)
            => Text(value, right - Sanitize(value).Length * size * (bold ? 0.55 : 0.5), top, size, bold);
        public void Centered(string value, double left, double top, double width, double size)
            => Text(value, left + Math.Max(0, (width - Sanitize(value).Length * size * 0.5) / 2), top, size);
        public void Wrapped(string value, double left, double top, double width, double size, int maxLines, bool bold = false, double r = 0.08, double g = 0.12, double b = 0.18)
        {
            var words = Sanitize(value).Split(' ', StringSplitOptions.RemoveEmptyEntries);
            var line = new StringBuilder();
            var lines = new List<string>();
            var max = Math.Max(8, (int)(width / (size * 0.52)));
            foreach (var word in words)
            {
                if (line.Length > 0 && line.Length + word.Length + 1 > max)
                {
                    lines.Add(line.ToString());
                    line.Clear();
                    if (lines.Count == maxLines) break;
                }
                if (line.Length > 0) line.Append(' ');
                line.Append(word.Length > max ? word[..max] : word);
            }
            if (line.Length > 0 && lines.Count < maxLines) lines.Add(line.ToString());
            for (var index = 0; index < lines.Count; index++) Text(lines[index], left, top + index * (size + 2), size, bold, r, g, b);
        }
        public void Fill(double left, double top, double width, double height, double r, double g, double b)
            => builder.Append(CultureInfo.InvariantCulture, $"{F(r)} {F(g)} {F(b)} rg {F(left)} {F(pageHeight - top - height)} {F(width)} {F(height)} re f\n");
        public void Stroke(double left, double top, double width, double height, double lineWidth, double r, double g, double b)
            => builder.Append(CultureInfo.InvariantCulture, $"{F(r)} {F(g)} {F(b)} RG {F(lineWidth)} w {F(left)} {F(pageHeight - top - height)} {F(width)} {F(height)} re S\n");
        public void Border(double left, double top, double width, double height) => Stroke(left, top, width, height, 0.8, 0.32, 0.38, 0.44);
        public void Line(double x1, double top1, double x2, double top2)
            => builder.Append(CultureInfo.InvariantCulture, $"0.45 0.49 0.54 RG 0.5 w {F(x1)} {F(pageHeight - top1)} m {F(x2)} {F(pageHeight - top2)} l S\n");
        public void Qr(string payload, double left, double top, double size)
            => DocumentCodeService.AppendPdfCommands(builder, pageHeight, left, top, size, payload);
        private static string Escape(string value) => Sanitize(value).Replace("\\", "\\\\").Replace("(", "\\(").Replace(")", "\\)");
        private static string Sanitize(string? value) => new((value ?? string.Empty).Select(character => character is >= ' ' and <= '~' ? character : '?').ToArray());
    }
}
