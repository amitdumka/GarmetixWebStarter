using Garmetix.Core.Models.Attendance;

namespace Garmetix.Api.Attendance.Dtos;

public sealed record AttendanceEmployeeLookupDto(
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

public sealed record AttendancePunchRequest(
    Guid EmployeeId,
    string? PunchType,
    DateTime? PunchTimeUtc,
    DateTime? LocalPunchTime,
    string? Source,
    Guid? DeviceId,
    string? DeviceCode,
    string? DeviceToken,
    string? PhotoProofPath,
    string? ClientPunchId,
    decimal? Latitude,
    decimal? Longitude,
    decimal? ConfidenceScore,
    string? Reason,
    string? Remarks,
    Guid CompanyId,
    Guid StoreGroupId,
    Guid StoreId);

public sealed record AttendancePunchResultDto(
    bool Success,
    string Message,
    AttendancePunch? Punch,
    AttendanceDayStatusDto? DayStatus,
    bool Duplicate);

public sealed record AttendanceDayStatusDto(
    Guid EmployeeId,
    DateTime OnDate,
    string EmployeeName,
    string EmployeeCode,
    string Status,
    DateTime? CheckIn,
    DateTime? CheckOut,
    int WorkingMinutes,
    int OvertimeMinutes,
    int LateMinutes,
    bool NeedsReview);

public sealed record AttendanceTodayDto(
    DateTime OnDate,
    int EmployeeCount,
    int Present,
    int Late,
    int HalfDay,
    int Absent,
    int NeedsReview,
    IReadOnlyList<AttendanceDayStatusDto> Rows);

public sealed record AttendanceMonthlyDto(
    int Year,
    int Month,
    Guid? EmployeeId,
    int EmployeeCount,
    decimal PresentDays,
    decimal LateDays,
    decimal HalfDays,
    decimal AbsentDays,
    int OvertimeMinutes,
    bool Locked,
    IReadOnlyList<AttendanceDayStatusDto> Days);

public sealed record AttendanceDeviceRegisterRequest(
    string DeviceName,
    string DeviceType,
    string? AppVersion,
    string? Notes,
    Guid CompanyId,
    Guid StoreGroupId,
    Guid StoreId);

public sealed record AttendanceDeviceRegistrationDto(
    Guid DeviceId,
    string DeviceCode,
    string DeviceToken,
    string DeviceName,
    string DeviceType,
    string Status);

public sealed record AttendanceDeviceHeartbeatRequest(
    Guid DeviceId,
    string DeviceToken,
    string? AppVersion);

public sealed record AttendanceKioskBootstrapRequest(
    Guid? DeviceId,
    string? DeviceCode,
    string? DeviceToken);

public sealed record AttendanceKioskBootstrapDto(
    bool DeviceValid,
    string Message,
    Guid? DeviceId,
    string? DeviceCode,
    string Version,
    string Stage,
    IReadOnlyList<string> SupportedSources,
    IReadOnlyList<string> SupportedPunchTypes);

public sealed record AttendanceEmployeeLookupRequest(
    Guid DeviceId,
    string DeviceToken,
    string Search);

public sealed record AttendanceSyncPendingRequest(
    Guid DeviceId,
    string DeviceToken,
    IReadOnlyList<AttendancePunchRequest> Punches);

public sealed record AttendanceSyncPendingResult(
    int Accepted,
    int Duplicate,
    int Failed,
    IReadOnlyList<AttendancePunchResultDto> Results);

public sealed record AttendanceRegularizationRequestDto(
    Guid EmployeeId,
    Guid? AttendancePunchId,
    string RequestType,
    string RequestedPunchType,
    DateTime? RequestedPunchTimeUtc,
    DateTime? RequestedLocalPunchTime,
    string Reason,
    Guid CompanyId,
    Guid StoreGroupId,
    Guid StoreId);

public sealed record AttendanceApprovalRequestDto(
    string? Remarks);

public sealed record AttendanceRecalculateRequest(
    int Year,
    int Month,
    Guid? EmployeeId,
    Guid? CompanyId,
    Guid? StoreGroupId,
    Guid? StoreId);

public sealed record AttendanceLockMonthRequest(
    int Year,
    int Month,
    Guid? CompanyId,
    Guid? StoreGroupId,
    Guid? StoreId,
    bool Locked);

public sealed record AttendancePayrollSummaryDto(
    int Year,
    int Month,
    int Employees,
    decimal PresentDays,
    decimal AbsentDays,
    decimal LateDays,
    decimal HalfDays,
    int OvertimeMinutes,
    bool HasLockedRows,
    IReadOnlyList<AttendanceMonthlySummary> Rows);


public sealed record AttendancePayrollReviewRowDto(
    Guid Id,
    Guid EmployeeId,
    string EmployeeCode,
    string EmployeeName,
    int Year,
    int Month,
    decimal PresentDays,
    decimal AbsentDays,
    decimal LateDays,
    decimal HalfDays,
    decimal LeaveDays,
    decimal PayableDays,
    decimal DeductionDays,
    int WorkingMinutes,
    int OvertimeMinutes,
    decimal EstimatedDailyRate,
    decimal EstimatedGrossPay,
    string ReviewStatus,
    string PayrollActionStatus,
    bool Locked,
    DateTime? ReviewedAtUtc,
    string? ReviewedBy,
    string? Notes);

public sealed record AttendancePayrollReviewDto(
    int Year,
    int Month,
    int Employees,
    decimal PresentDays,
    decimal AbsentDays,
    decimal LateDays,
    decimal HalfDays,
    decimal LeaveDays,
    decimal PayableDays,
    decimal DeductionDays,
    int OvertimeMinutes,
    bool HasLockedRows,
    int DraftRows,
    int ReviewedRows,
    IReadOnlyList<AttendancePayrollReviewRowDto> Rows);

public sealed record AttendancePayrollReviewBuildRequest(
    int Year,
    int Month,
    Guid? EmployeeId,
    Guid? CompanyId,
    Guid? StoreGroupId,
    Guid? StoreId);

public sealed record AttendancePayrollReviewMarkRequest(
    string ReviewStatus,
    string? Notes);

public sealed record AttendancePhotoProofRequest(
    Guid DeviceId,
    string DeviceToken,
    Guid EmployeeId,
    string DataUrl,
    string? ClientPunchId,
    DateTime? CapturedAtUtc,
    string? Remarks);

public sealed record AttendancePhotoProofDto(
    bool Success,
    string Message,
    Guid? PhotoProofId,
    string? PhotoProofPath,
    long SizeBytes,
    string VerificationStatus);

public sealed record AttendanceKioskReadinessRequest(
    Guid DeviceId,
    string DeviceToken);

public sealed record AttendanceKioskReadinessDto(
    bool DeviceValid,
    string DeviceCode,
    string DeviceName,
    string StoreScope,
    bool PhotoProofEnabled,
    long PhotoProofMaxBytes,
    bool OfflineSyncEnabled,
    int DuplicateWindowMinutes,
    IReadOnlyList<string> NextStageItems);


public sealed record AttendancePhotoProofReviewRequest(
    string Decision,
    string? Reason,
    string? Remarks,
    bool CreateRegularizationRequest);

public sealed record AttendancePhotoProofReviewSummaryDto(
    int Pending,
    int Approved,
    int Rejected,
    int Flagged,
    int NeedsRegularization,
    int ExpiringSoon);
