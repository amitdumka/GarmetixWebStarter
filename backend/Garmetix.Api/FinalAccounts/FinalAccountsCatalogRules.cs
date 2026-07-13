using Garmetix.Core.Models.FinalAccounts;

namespace Garmetix.Api.FinalAccounts;

public static class FinalAccountsCatalogRules
{
    public static bool TryParseAccountShape(string accountType, string naturalBalance, out ParsedAccountShape shape, out string message)
    {
        shape = default!;
        message = string.Empty;
        if (!Enum.TryParse(accountType, ignoreCase: true, out FinalAccountsAccountType type))
        {
            message = $"Unknown account type '{accountType}'.";
            return false;
        }

        if (!Enum.TryParse(naturalBalance, ignoreCase: true, out FinalAccountsNaturalBalance balance))
        {
            message = $"Unknown natural balance '{naturalBalance}'.";
            return false;
        }

        if (!IsExpectedNaturalBalance(type, balance))
        {
            message = $"{type} accounts must normally carry a {ExpectedNaturalBalance(type)} natural balance.";
            return false;
        }

        shape = new ParsedAccountShape(type, balance);
        return true;
    }

    public static FinalAccountsNaturalBalance ExpectedNaturalBalance(FinalAccountsAccountType type)
        => type switch
        {
            FinalAccountsAccountType.Asset or FinalAccountsAccountType.Expense or FinalAccountsAccountType.ContraLiability => FinalAccountsNaturalBalance.Debit,
            _ => FinalAccountsNaturalBalance.Credit
        };

    public static bool IsExpectedNaturalBalance(FinalAccountsAccountType type, FinalAccountsNaturalBalance balance)
        => ExpectedNaturalBalance(type) == balance;

    public static bool CreatesCycle(Guid currentId, Guid? parentId, IReadOnlyDictionary<Guid, Guid?> parentLookup)
    {
        var seen = new HashSet<Guid> { currentId };
        var next = parentId;
        while (next.HasValue)
        {
            if (!seen.Add(next.Value))
            {
                return true;
            }

            next = parentLookup.TryGetValue(next.Value, out var parent) ? parent : null;
        }

        return false;
    }

    public static bool RangesOverlap(DateTime firstStart, DateTime firstEnd, DateTime secondStart, DateTime secondEnd)
        => firstStart.Date <= secondEnd.Date && secondStart.Date <= firstEnd.Date;

    public static bool IsAllowedStatusTransition(FinalAccountsPeriodStatus current, FinalAccountsPeriodStatus next)
        => current == next
            || current == FinalAccountsPeriodStatus.Draft && next == FinalAccountsPeriodStatus.Open
            || current == FinalAccountsPeriodStatus.Open && (next == FinalAccountsPeriodStatus.Closed || next == FinalAccountsPeriodStatus.Locked)
            || current == FinalAccountsPeriodStatus.Closed && next == FinalAccountsPeriodStatus.Locked;

    public static string NormalizeCode(string value)
        => string.Join("-", (value ?? string.Empty).Trim().ToUpperInvariant().Split([' ', '_', '-'], StringSplitOptions.RemoveEmptyEntries));

    public static string NormalizeMappingKey(string value)
        => string.Join(".", (value ?? string.Empty).Trim().ToUpperInvariant().Split([' ', '_', '-', '.'], StringSplitOptions.RemoveEmptyEntries));
}
