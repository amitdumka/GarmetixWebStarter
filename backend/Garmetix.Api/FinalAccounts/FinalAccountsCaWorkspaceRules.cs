using Garmetix.Core.Models.FinalAccounts;

namespace Garmetix.Api.FinalAccounts;

public static class FinalAccountsCaWorkspaceRules
{
    public const string CaAdjustmentSourceType = "CAAdjustment";
    public const string CaAdjustmentReversalSourceType = "CAAdjustmentReversal";
    public const string AuditStatusUnaudited = "Unaudited";

    private static readonly HashSet<FinalAccountsAdjustmentStatus> EditableStatuses =
    [
        FinalAccountsAdjustmentStatus.Draft,
        FinalAccountsAdjustmentStatus.Rejected
    ];

    public static bool CanEdit(FinalAccountsAdjustmentStatus status) => EditableStatuses.Contains(status);

    public static bool CanTransition(FinalAccountsAdjustmentStatus from, FinalAccountsAdjustmentStatus to)
        => (from, to) switch
        {
            (FinalAccountsAdjustmentStatus.Draft, FinalAccountsAdjustmentStatus.Submitted) => true,
            (FinalAccountsAdjustmentStatus.Submitted, FinalAccountsAdjustmentStatus.Review) => true,
            (FinalAccountsAdjustmentStatus.Review, FinalAccountsAdjustmentStatus.Approved) => true,
            (FinalAccountsAdjustmentStatus.Submitted, FinalAccountsAdjustmentStatus.Rejected) => true,
            (FinalAccountsAdjustmentStatus.Review, FinalAccountsAdjustmentStatus.Rejected) => true,
            (FinalAccountsAdjustmentStatus.Approved, FinalAccountsAdjustmentStatus.Posted) => true,
            (FinalAccountsAdjustmentStatus.Posted, FinalAccountsAdjustmentStatus.Reversed) => true,
            _ => false
        };

    public static void EnsureTransition(FinalAccountsAdjustmentStatus from, FinalAccountsAdjustmentStatus to)
    {
        if (!CanTransition(from, to))
        {
            throw new InvalidOperationException($"CA adjustment cannot move from {from} to {to}.");
        }
    }

    public static string ReportVersionFor(FinalAccountsAdjustmentStatus status)
        => status switch
        {
            FinalAccountsAdjustmentStatus.Draft => FinalAccountsReportVersionKind.Provisional.ToString(),
            FinalAccountsAdjustmentStatus.Submitted => FinalAccountsReportVersionKind.Provisional.ToString(),
            FinalAccountsAdjustmentStatus.Review => FinalAccountsReportVersionKind.Provisional.ToString(),
            FinalAccountsAdjustmentStatus.Rejected => FinalAccountsReportVersionKind.Provisional.ToString(),
            FinalAccountsAdjustmentStatus.Approved => FinalAccountsReportVersionKind.Adjusted.ToString(),
            FinalAccountsAdjustmentStatus.Posted => FinalAccountsReportVersionKind.Final.ToString(),
            FinalAccountsAdjustmentStatus.Reversed => FinalAccountsReportVersionKind.Final.ToString(),
            _ => FinalAccountsReportVersionKind.Provisional.ToString()
        };

    public static FinalAccountsReportVersionKind ParseReportVersion(string? value)
        => Enum.TryParse<FinalAccountsReportVersionKind>(value, ignoreCase: true, out var parsed)
            ? parsed
            : throw new ArgumentException($"Report version '{value}' is not supported.");

    public static FinalAccountsAdjustmentStatus ParseStatus(string? value)
        => Enum.TryParse<FinalAccountsAdjustmentStatus>(value, ignoreCase: true, out var parsed)
            ? parsed
            : throw new ArgumentException($"Adjustment status '{value}' is not supported.");

    public static string NormalizeBatchNumberPrefix(DateTime onDate)
        => $"CA-{onDate:yyyyMMdd}";

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
