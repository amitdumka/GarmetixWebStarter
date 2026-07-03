using System.Globalization;
using System.Text;

namespace Garmetix.Api.DotMatrix;

public static class DotMatrixTextFormatter
{
    public static string Rule(int width, char ch = '=') => new(ch, Math.Clamp(width, 40, 180));

    public static string Center(int width, string value)
    {
        value = Clean(value);
        if (value.Length >= width) return value[..width];
        var left = (width - value.Length) / 2;
        return new string(' ', left) + value;
    }

    public static string Fit(string? value, int width, bool right = false)
    {
        width = Math.Max(1, width);
        var clean = Clean(value);
        if (clean.Length > width)
        {
            clean = clean[..Math.Max(1, width - 1)] + "…";
        }
        return right ? clean.PadLeft(width) : clean.PadRight(width);
    }

    public static string Money(decimal value, int width = 14) => value.ToString("N2", CultureInfo.InvariantCulture).PadLeft(width);

    public static string Pair(string label, decimal amount, int width)
    {
        var value = Money(amount, 14);
        return Fit(label, Math.Max(1, width - value.Length)) + value;
    }

    public static string Pair(string label, string value, int width)
    {
        var rightWidth = Math.Min(28, Math.Max(10, width / 3));
        return Fit(label, width - rightWidth) + Fit(value, rightWidth, right: true);
    }

    public static string ThreeBlockLine((string Label, string Value) left, (string Label, string Value) middle, (string Label, string Value) right, int width)
    {
        var gap = "  ";
        var block = (width - gap.Length * 2) / 3;
        var rem = width - (block * 3 + gap.Length * 2);
        return Pair(left.Label, left.Value, block)
            + gap
            + Pair(middle.Label, middle.Value, block)
            + gap
            + Pair(right.Label, right.Value, block + rem);
    }

    public static string ThreeBlockMoneyLine((string Label, decimal Value) left, (string Label, decimal Value) middle, (string Label, decimal Value) right, int width)
    {
        var gap = "  ";
        var block = (width - gap.Length * 2) / 3;
        var rem = width - (block * 3 + gap.Length * 2);
        return Pair(left.Label, left.Value, block)
            + gap
            + Pair(middle.Label, middle.Value, block)
            + gap
            + Pair(right.Label, right.Value, block + rem);
    }

    public static IEnumerable<string> Wrap(string? value, int width)
    {
        var clean = Clean(value);
        if (string.IsNullOrEmpty(clean))
        {
            yield break;
        }

        while (clean.Length > width)
        {
            var take = clean.LastIndexOf(' ', Math.Min(width, clean.Length - 1));
            if (take < width / 2) take = width;
            yield return clean[..take].TrimEnd();
            clean = clean[take..].TrimStart();
        }
        if (clean.Length > 0) yield return clean;
    }

    public static string CashDetailsTable(int width, string title, CashDetailPrintModel? cash)
    {
        var sb = new StringBuilder();
        sb.AppendLine(title);
        sb.AppendLine(Rule(width, '-'));
        sb.AppendLine(Fit("Denomination", 18) + Fit("Count", 10, true) + Fit("Amount", 16, true));
        sb.AppendLine(Rule(width, '-'));
        if (cash is not null)
        {
            AddCash(sb, "Rs 2000", cash.N2000, 2000);
            AddCash(sb, "Rs 500", cash.N500, 500);
            AddCash(sb, "Rs 200", cash.N200, 200);
            AddCash(sb, "Rs 100", cash.N100, 100);
            AddCash(sb, "Rs 50", cash.N50, 50);
            AddCash(sb, "Rs 20", cash.NC20, 20);
            AddCash(sb, "Rs 10", cash.NC10, 10);
            AddCash(sb, "Rs 5", cash.NC5, 5);
            AddCash(sb, "Rs 2", cash.NC2, 2);
            AddCash(sb, "Rs 1", cash.NC1, 1);
        }
        sb.AppendLine(Rule(width, '-'));
        sb.AppendLine(Pair("Cash Total", cash?.Amount ?? 0m, Math.Min(width, 50)));
        return sb.ToString().TrimEnd();

        static void AddCash(StringBuilder sb, string label, int count, int denomination)
        {
            if (count <= 0) return;
            sb.AppendLine(Fit(label, 18) + Fit(count.ToString(CultureInfo.InvariantCulture), 10, true) + Money(count * denomination, 16));
        }
    }

    public static string Clean(string? value) => string.IsNullOrWhiteSpace(value)
        ? string.Empty
        : value.Replace("\r", " ").Replace("\n", " ").Trim();
}

public sealed record CashDetailPrintModel(decimal Amount, int N2000, int N500, int N200, int N100, int N50, int NC20, int NC10, int NC5, int NC2, int NC1);
