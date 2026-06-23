namespace Garmetix.Api.Testing;

public sealed record TestAutomationCheckDefinitionDto(
    string Code,
    string Scope,
    string Title,
    string Command,
    string ExpectedResult,
    string Severity,
    bool RequiresDocker,
    bool RequiresLiveServer);

public sealed record TestAutomationRuntimeCheckDto(
    string Code,
    string Title,
    string Status,
    string Severity,
    string Message,
    string FixHint);

public sealed record TestAutomationRuntimeSummaryDto(
    string OverallStatus,
    DateTimeOffset CheckedAtUtc,
    string Version,
    string BuildCode,
    int Passed,
    int Warnings,
    int Critical,
    IReadOnlyList<TestAutomationRuntimeCheckDto> Checks);
