using Garmetix.Core.Models.FinalAccounts;

namespace Garmetix.Api.FinalAccounts;

public static class FinalAccountsPeriodCloseRules
{
    public const string Pass = "Pass";
    public const string Block = "Block";
    public const string Warning = "Warning";
    public const string Prepared = "Prepared";
    public const string NotRequired = "NotRequired";

    public static FinalAccountsCloseType ParseCloseType(string? value)
        => Enum.TryParse<FinalAccountsCloseType>(value, ignoreCase: true, out var parsed)
            ? parsed
            : throw new ArgumentException($"Close type '{value}' is not supported.");

    public static FinalAccountsCloseRunStatus ParseRunStatus(string? value)
        => Enum.TryParse<FinalAccountsCloseRunStatus>(value, ignoreCase: true, out var parsed)
            ? parsed
            : throw new ArgumentException($"Close status '{value}' is not supported.");

    public static bool CanClose(IReadOnlyList<FinalAccountsCloseChecklistItemDto> checklist)
        => checklist.Where(item => item.Required).All(item => string.Equals(item.Status, Pass, StringComparison.OrdinalIgnoreCase));

    public static bool CanReopen(FinalAccountsCloseRunStatus status)
        => status == FinalAccountsCloseRunStatus.Closed;

    public static string OverallStatus(IReadOnlyList<FinalAccountsCloseChecklistItemDto> checklist)
    {
        if (checklist.Any(item => item.Required && string.Equals(item.Status, Block, StringComparison.OrdinalIgnoreCase)))
        {
            return Block;
        }

        return checklist.Any(item => string.Equals(item.Status, Warning, StringComparison.OrdinalIgnoreCase)) ? Warning : Pass;
    }

    public static string BuildRunNumberPrefix(FinalAccountsCloseType closeType, DateTime periodEnd)
        => $"{(closeType == FinalAccountsCloseType.Year ? "FYC" : "FPC")}-{periodEnd:yyyyMMdd}";

    public static FinalAccountsCloseChecklistItemDto Gate(
        string key,
        string label,
        bool required,
        bool passed,
        string passDetail,
        string blockDetail,
        decimal? amount,
        int sortOrder)
        => new(key, label, required, passed ? Pass : Block, passed ? passDetail : blockDetail, amount, sortOrder);

    public static FinalAccountsCloseChecklistItemDto Info(
        string key,
        string label,
        string status,
        string detail,
        decimal? amount,
        int sortOrder)
        => new(key, label, false, status, detail, amount, sortOrder);

    public static string NormalizeText(string? value, string fieldName, int maxLength)
    {
        var trimmed = (value ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(trimmed))
        {
            throw new ArgumentException($"{fieldName} is required.");
        }

        return trimmed.Length <= maxLength ? trimmed : trimmed[..maxLength];
    }

    public static string? OptionalText(string? value, int maxLength)
    {
        var trimmed = (value ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(trimmed))
        {
            return null;
        }

        return trimmed.Length <= maxLength ? trimmed : trimmed[..maxLength];
    }

    public static decimal RoundAmount(decimal value) => Math.Round(value, 2, MidpointRounding.AwayFromZero);
}
