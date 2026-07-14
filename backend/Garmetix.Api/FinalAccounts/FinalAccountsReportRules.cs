using Garmetix.Core.Models.FinalAccounts;

namespace Garmetix.Api.FinalAccounts;

public static class FinalAccountsReportRules
{
    public const int DefaultPageSize = 50;
    public const int MaxPageSize = 250;

    public static int NormalizePage(int? page)
        => Math.Max(1, page ?? 1);

    public static int NormalizePageSize(int? pageSize)
        => Math.Clamp(pageSize ?? DefaultPageSize, 1, MaxPageSize);

    public static decimal RoundAmount(decimal value)
        => Math.Round(value, 2, MidpointRounding.AwayFromZero);

    public static decimal OpeningSignedBalance(decimal openingBalance, FinalAccountsNaturalBalance naturalBalance)
    {
        var amount = RoundAmount(openingBalance);
        return naturalBalance == FinalAccountsNaturalBalance.Credit ? -amount : amount;
    }

    public static (decimal Debit, decimal Credit) SplitSignedBalance(decimal signedBalance)
    {
        var rounded = RoundAmount(signedBalance);
        if (rounded > 0)
        {
            return (rounded, 0m);
        }

        if (rounded < 0)
        {
            return (0m, Math.Abs(rounded));
        }

        return (0m, 0m);
    }

    public static string BalanceType(decimal signedBalance)
    {
        var rounded = RoundAmount(signedBalance);
        if (rounded > 0)
        {
            return "Debit";
        }

        if (rounded < 0)
        {
            return "Credit";
        }

        return "Zero";
    }

    public static bool IsZeroBalance(decimal openingDebit, decimal openingCredit, decimal periodDebit, decimal periodCredit, decimal closingDebit, decimal closingCredit)
        => RoundAmount(openingDebit + openingCredit + periodDebit + periodCredit + closingDebit + closingCredit) == 0m;

    public static string NormalizeTrialBalanceView(string? view)
        => string.Equals(view, "Group", StringComparison.OrdinalIgnoreCase) ? "Group" : "Ledger";

    public static string NormalizeComparison(string? comparison)
        => string.Equals(comparison, "Quarterly", StringComparison.OrdinalIgnoreCase) ? "Quarterly" : "Monthly";

    public static string BalanceStatus(decimal difference)
        => Math.Abs(RoundAmount(difference)) <= 0.01m ? "Balanced" : "Difference";

    public static string PeriodKey(DateTime date, string comparison)
    {
        var normalized = NormalizeComparison(comparison);
        if (normalized == "Quarterly")
        {
            var quarter = ((date.Month - 1) / 3) + 1;
            return $"{date:yyyy}-Q{quarter}";
        }

        return date.ToString("yyyy-MM");
    }

    public static DateTime PeriodStart(DateTime date, string comparison)
    {
        var normalized = NormalizeComparison(comparison);
        if (normalized == "Quarterly")
        {
            var month = (((date.Month - 1) / 3) * 3) + 1;
            return new DateTime(date.Year, month, 1);
        }

        return new DateTime(date.Year, date.Month, 1);
    }

    public static DateTime PeriodEnd(DateTime date, string comparison)
    {
        var start = PeriodStart(date, comparison);
        return NormalizeComparison(comparison) == "Quarterly"
            ? start.AddMonths(3).AddDays(-1)
            : start.AddMonths(1).AddDays(-1);
    }
}
