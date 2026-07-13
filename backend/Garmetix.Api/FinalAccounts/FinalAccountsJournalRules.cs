using Garmetix.Core.Models.FinalAccounts;

namespace Garmetix.Api.FinalAccounts;

public static class FinalAccountsJournalRules
{
    public const string ManualAdjustmentSourceType = "ManualAdjustment";
    public const string ReversalSourceType = "JournalReversal";
    public const int MaxIdempotencyKeyLength = 160;

    public static FinalAccountsJournalValidationResponse ValidateJournal(
        IReadOnlyList<FinalAccountsJournalLineRequest> lines,
        FinalAccountsPeriodStatus? periodStatus,
        bool requireOpenPeriod = true,
        bool requireBalanced = true)
    {
        var issues = new List<FinalAccountsValidationIssueDto>();
        if (lines.Count == 0)
        {
            issues.Add(new FinalAccountsValidationIssueDto("Error", "LinesRequired", "At least two journal lines are required.", null));
        }

        var totalDebit = 0m;
        var totalCredit = 0m;
        for (var i = 0; i < lines.Count; i++)
        {
            var line = lines[i];
            var lineNumber = i + 1;
            var debit = RoundAmount(line.Debit);
            var credit = RoundAmount(line.Credit);
            totalDebit += Math.Max(0m, debit);
            totalCredit += Math.Max(0m, credit);

            if (line.AccountId == Guid.Empty)
            {
                issues.Add(new FinalAccountsValidationIssueDto("Error", "AccountRequired", $"Line {lineNumber} must select an account.", null));
            }

            if (debit < 0m || credit < 0m)
            {
                issues.Add(new FinalAccountsValidationIssueDto("Error", "NegativeAmount", $"Line {lineNumber} cannot have a negative debit or credit.", line.AccountId == Guid.Empty ? null : line.AccountId));
            }

            if (debit > 0m && credit > 0m)
            {
                issues.Add(new FinalAccountsValidationIssueDto("Error", "BothDebitAndCredit", $"Line {lineNumber} cannot contain both debit and credit.", line.AccountId == Guid.Empty ? null : line.AccountId));
            }

            if (debit == 0m && credit == 0m)
            {
                issues.Add(new FinalAccountsValidationIssueDto("Error", "AmountRequired", $"Line {lineNumber} must contain either debit or credit.", line.AccountId == Guid.Empty ? null : line.AccountId));
            }
        }

        if (lines.Count == 1)
        {
            issues.Add(new FinalAccountsValidationIssueDto("Error", "MinimumLines", "A journal entry must contain at least two lines.", null));
        }

        var difference = RoundAmount(totalDebit - totalCredit);
        if (requireBalanced && difference != 0m)
        {
            issues.Add(new FinalAccountsValidationIssueDto("Error", "UnbalancedJournal", $"Debit and credit totals must match. Difference: {Math.Abs(difference):0.00}.", null));
        }

        if (requireOpenPeriod && !CanPostPeriodStatus(periodStatus))
        {
            issues.Add(new FinalAccountsValidationIssueDto("Error", "PeriodNotOpen", "Journal date must fall inside an open fiscal period.", null));
        }

        return new FinalAccountsJournalValidationResponse(
            issues.All(item => !string.Equals(item.Severity, "Error", StringComparison.OrdinalIgnoreCase)),
            RoundAmount(totalDebit),
            RoundAmount(totalCredit),
            difference,
            issues);
    }

    public static bool CanPostStatus(FinalAccountsJournalStatus status)
        => status == FinalAccountsJournalStatus.Draft;

    public static bool CanEditStatus(FinalAccountsJournalStatus status)
        => status == FinalAccountsJournalStatus.Draft;

    public static bool CanDeleteStatus(FinalAccountsJournalStatus status)
        => status == FinalAccountsJournalStatus.Draft;

    public static bool CanReverseStatus(FinalAccountsJournalStatus status)
        => status == FinalAccountsJournalStatus.Posted;

    public static bool CanPostPeriodStatus(FinalAccountsPeriodStatus? status)
        => status == FinalAccountsPeriodStatus.Open;

    public static IReadOnlyList<FinalAccountsJournalLineRequest> BuildReversalLines(IEnumerable<FinalAccountsJournalLineRequest> lines)
        => lines.Select(line => new FinalAccountsJournalLineRequest(
                line.AccountId,
                RoundAmount(line.Credit),
                RoundAmount(line.Debit),
                line.Narration))
            .ToList();

    public static decimal RoundAmount(decimal value, int scale = FinalAccountsSettingsDefaults.RoundingScale)
        => decimal.Round(value, scale, MidpointRounding.AwayFromZero);

    public static string NormalizeSourceType(string? value)
    {
        var trimmed = (value ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(trimmed))
        {
            return ManualAdjustmentSourceType;
        }

        return trimmed.Length <= 80 ? trimmed : trimmed[..80];
    }

    public static string? NormalizeIdempotencyKey(string? value)
    {
        var trimmed = (value ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(trimmed))
        {
            return null;
        }

        return trimmed.Length <= MaxIdempotencyKeyLength ? trimmed : trimmed[..MaxIdempotencyKeyLength];
    }

    public static string? FindDuplicateIdempotencyKey(string? incomingKey, IEnumerable<string?> existingKeys)
    {
        var normalized = NormalizeIdempotencyKey(incomingKey);
        if (normalized is null)
        {
            return null;
        }

        return existingKeys
            .Select(NormalizeIdempotencyKey)
            .FirstOrDefault(item => string.Equals(item, normalized, StringComparison.OrdinalIgnoreCase));
    }

    public static string BuildJournalNumberPrefix(DateTime onDate)
        => $"FAJ-{onDate:yyyyMMdd}";

    public static string BuildDraftNumberPrefix(DateTime onDate)
        => $"FAD-{onDate:yyyyMMdd}";
}
