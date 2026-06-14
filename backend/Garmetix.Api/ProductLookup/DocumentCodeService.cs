using System.Globalization;
using System.Text;
using QRCoder;

namespace Garmetix.Api.ProductLookup;

public static class DocumentCodeService
{
    public const string SaleInvoice = "SALE";
    public const string PurchaseInvoice = "PURCHASE";
    public const string Voucher = "VOUCHER";
    public const string CashVoucher = "CASH";
    public const string CommercialNote = "NOTE";
    public const string PettyCash = "PETTY";
    public const string Payslip = "PAYSLIP";
    public const string SalaryPayment = "SALARYPAY";
    public const string NonGstGoods = "OFFBOOKGOODS";

    public static string Create(string documentType, Guid id)
        => $"GMX:{NormalizeType(documentType)}:{id:N}";

    public static bool TryParse(string? value, out string documentType, out Guid id)
    {
        documentType = string.Empty;
        id = Guid.Empty;
        var parts = (value ?? string.Empty).Trim().Split(':', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length != 3 || !string.Equals(parts[0], "GMX", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        documentType = NormalizeType(parts[1]);
        return Guid.TryParse(parts[2], out id);
    }

    public static string BuildSvg(string payload, int moduleSize = 5, int quietZone = 4)
    {
        using var generator = new QRCodeGenerator();
        using var data = generator.CreateQrCode(payload, QRCodeGenerator.ECCLevel.Q);
        var modules = data.ModuleMatrix;
        var totalModules = modules.Count + quietZone * 2;
        var size = totalModules * moduleSize;
        var builder = new StringBuilder();
        builder.Append(CultureInfo.InvariantCulture,
            $"<svg xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"0 0 {size} {size}\" width=\"{size}\" height=\"{size}\" shape-rendering=\"crispEdges\">");
        builder.Append($"<rect width=\"{size}\" height=\"{size}\" fill=\"#fff\"/>");
        for (var row = 0; row < modules.Count; row++)
        {
            for (var column = 0; column < modules[row].Length; column++)
            {
                if (!modules[row][column])
                {
                    continue;
                }

                builder.Append(CultureInfo.InvariantCulture,
                    $"<rect x=\"{(column + quietZone) * moduleSize}\" y=\"{(row + quietZone) * moduleSize}\" width=\"{moduleSize}\" height=\"{moduleSize}\" fill=\"#020817\"/>");
            }
        }

        builder.Append("</svg>");
        return builder.ToString();
    }

    public static void AppendPdfCommands(
        StringBuilder builder,
        double pageHeight,
        double left,
        double top,
        double size,
        string payload,
        int quietZone = 3)
    {
        using var generator = new QRCodeGenerator();
        using var data = generator.CreateQrCode(payload, QRCodeGenerator.ECCLevel.Q);
        var modules = data.ModuleMatrix;
        var totalModules = modules.Count + quietZone * 2;
        var moduleSize = size / totalModules;

        builder.Append(CultureInfo.InvariantCulture,
            $"1 1 1 rg {F(left)} {F(pageHeight - top - size)} {F(size)} {F(size)} re f\n");
        builder.Append("0.01 0.03 0.08 rg\n");
        for (var row = 0; row < modules.Count; row++)
        {
            for (var column = 0; column < modules[row].Length; column++)
            {
                if (!modules[row][column])
                {
                    continue;
                }

                var x = left + (column + quietZone) * moduleSize;
                var y = pageHeight - top - (row + quietZone + 1) * moduleSize;
                builder.Append(CultureInfo.InvariantCulture,
                    $"{F(x)} {F(y)} {F(moduleSize + 0.02)} {F(moduleSize + 0.02)} re f\n");
            }
        }
    }

    private static string NormalizeType(string value)
        => new(value.Trim().ToUpperInvariant().Where(char.IsLetterOrDigit).ToArray());

    private static string F(double value) => value.ToString("0.###", CultureInfo.InvariantCulture);
}
