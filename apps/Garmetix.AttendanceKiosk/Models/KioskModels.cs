namespace Garmetix.AttendanceKiosk.Models;

public sealed record KioskSettings(
    string ApiBaseUrl,
    string DeviceId,
    string DeviceToken);

public sealed record KioskBootstrapRequest(
    Guid? DeviceId,
    string? DeviceCode,
    string? DeviceToken);

public sealed record KioskReadinessRequest(
    Guid DeviceId,
    string DeviceToken);

public sealed record KioskReadinessResponse(
    bool DeviceValid,
    string DeviceCode,
    string DeviceName,
    string StoreScope,
    bool PhotoProofEnabled,
    long PhotoProofMaxBytes,
    bool OfflineSyncEnabled,
    int DuplicateWindowMinutes,
    IReadOnlyList<string> NextStageItems);

public sealed record EmployeeLookupRequest(
    Guid DeviceId,
    string DeviceToken,
    string Search);

public sealed record EmployeeLookupResponse(
    Guid Id,
    string EmployeeCode,
    string FullName,
    string Mobile,
    string Department,
    string Designation,
    Guid CompanyId,
    Guid StoreGroupId,
    Guid StoreId,
    string PhotoDataUrl);

public sealed record KioskPunchRequest(
    Guid EmployeeId,
    string PunchType,
    DateTime PunchTimeUtc,
    DateTime LocalPunchTime,
    string Source,
    Guid DeviceId,
    string DeviceToken,
    string ClientPunchId,
    Guid CompanyId,
    Guid StoreGroupId,
    Guid StoreId,
    decimal? Latitude,
    decimal? Longitude,
    string Remarks);

public sealed record SyncPendingRequest(
    Guid DeviceId,
    string DeviceToken,
    IReadOnlyList<KioskPunchRequest> Punches);

public sealed record SyncPendingResponse(
    int Accepted,
    int Duplicate,
    int Failed);

public sealed record PendingPunchRow(
    string Id,
    string ClientPunchId,
    string PayloadJson,
    DateTime CreatedAtUtc,
    DateTime? LastAttemptAtUtc,
    int AttemptCount,
    string Status,
    string? LastError);
