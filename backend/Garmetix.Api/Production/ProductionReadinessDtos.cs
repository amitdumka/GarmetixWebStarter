namespace Garmetix.Api.Production;

public sealed record ProductionReadinessSummaryDto(
    string OverallStatus,
    string Environment,
    DateTimeOffset CheckedAtUtc,
    int Passed,
    int Warnings,
    int Critical,
    IReadOnlyList<ProductionReadinessCheckDto> Checks);

public sealed record ProductionReadinessCheckDto(
    string Code,
    string Title,
    string Status,
    string Severity,
    string Message,
    string FixHint);
