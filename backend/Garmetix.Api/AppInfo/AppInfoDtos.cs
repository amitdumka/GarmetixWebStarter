namespace Garmetix.Api.AppInfo;

public sealed record AppVersionDto(
    string ProductName,
    string Version,
    string Stage,
    string ReleaseName,
    string BuildDate,
    string BuildCode,
    string Environment,
    IReadOnlyList<string> Highlights,
    IReadOnlyList<AppContactDto> Contacts,
    IReadOnlyList<AppFaqDto> Faqs);

public sealed record AppContactDto(
    string Label,
    string Value,
    string Type,
    string Note);

public sealed record AppFaqDto(
    string Question,
    string Answer,
    string Category);


public sealed record AppSystemInfoDto(
    string ProductName,
    string Version,
    string Stage,
    string ReleaseName,
    string BuildDate,
    string BuildCode,
    string Environment,
    string ServerTimeUtc,
    string ProcessStartedAtUtc,
    long UptimeSeconds,
    string AssemblyVersion);
