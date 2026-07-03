using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Garmetix.Api.AppInfo;
using Garmetix.Api.Attendance.Dtos;
using Garmetix.Api.Messages;
using Garmetix.Api.Workspace;
using Garmetix.Core.Enums;
using Garmetix.Core.Models.Attendance;
using Garmetix.Core.Models.HRM;
using Garmetix.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Garmetix.Api.Attendance.Services;

public interface IAttendanceService
{
    Task<AttendancePunchResultDto> RecordPunchAsync(AttendancePunchRequest request, HttpContext context, bool requireDevice, CancellationToken cancellationToken);
    Task<AttendanceTodayDto> BuildTodayAsync(HttpContext context, DateTime? onDate, CancellationToken cancellationToken);
    Task<AttendanceMonthlyDto> BuildMonthlyAsync(HttpContext context, Guid? employeeId, int year, int month, CancellationToken cancellationToken);
    Task<AttendanceDeviceRegistrationDto> RegisterDeviceAsync(AttendanceDeviceRegisterRequest request, HttpContext context, CancellationToken cancellationToken);
    Task<AttendanceDevice?> ValidateDeviceAsync(Guid deviceId, string? deviceToken, CancellationToken cancellationToken);
}

public interface IAttendanceSyncService
{
    Task<AttendanceSyncPendingResult> SyncPendingAsync(AttendanceSyncPendingRequest request, HttpContext context, CancellationToken cancellationToken);
}

public interface IBiometricEnrollmentService
{
    Task<EmployeeBiometricEnrollment> SaveAsync(BiometricEnrollmentSaveRequest request, HttpContext context, CancellationToken cancellationToken);
}

public sealed class AttendanceService(
    GarmetixDbContext db,
    IAttendanceRuleEngine ruleEngine,
    IOptions<AttendanceFingerprintOptions> fingerprintOptions,
    ApplicationMessageLogService logs) : IAttendanceService
{
    private static readonly string[] AutoPunchTypes = ["CheckIn", "CheckOut", "BreakIn", "BreakOut"];
    private static readonly string[] FingerprintAcceptedStatuses = ["Matched", "Identified", "Accepted"];

    public async Task<AttendancePunchResultDto> RecordPunchAsync(AttendancePunchRequest request, HttpContext context, bool requireDevice, CancellationToken cancellationToken)
    {
        if (request.EmployeeId == Guid.Empty)
        {
            return new(false, "Employee is required.", null, null, false);
        }

        AttendanceDevice? device = null;
        if (request.DeviceId.HasValue)
        {
            device = await ValidateDeviceAsync(request.DeviceId.Value, request.DeviceToken, cancellationToken);
            if (device is null)
            {
                return new(false, "Kiosk device is invalid, revoked, or token does not match.", null, null, false);
            }
        }
        else if (requireDevice)
        {
            return new(false, "Registered kiosk device is required.", null, null, false);
        }

        var employeeQuery = db.Employees.AsNoTracking().Where(item => item.Id == request.EmployeeId && item.Working && !item.Deleted);
        employeeQuery = device is not null
            ? employeeQuery.Where(item => item.CompanyId == device.CompanyId && item.StoreId == device.StoreId)
            : WorkspaceScope.ApplyTo(employeeQuery, context);
        var employee = await employeeQuery.FirstOrDefaultAsync(cancellationToken);
        if (employee is null)
        {
            return new(false, "Active employee was not found in this workspace.", null, null, false);
        }

        var punchUtc = ResolvePunchUtc(request);
        var localPunch = ResolveIndiaLocalPunchTime(request, punchUtc);
        var punchType = NormalizePunchType(request.PunchType);
        if (punchType == "Auto")
        {
            punchType = await DetectNextPunchTypeAsync(employee, localPunch.Date, context, cancellationToken);
        }

        var fingerprintPolicy = fingerprintOptions.Value;
        var fingerprintRequired = requireDevice && fingerprintPolicy.IsRequiredForStore(device?.StoreId ?? employee.StoreId);
        var fingerprintValidation = ValidateFingerprintProof(request.FingerprintProof, employee.Id, fingerprintPolicy, fingerprintRequired);
        if (!fingerprintValidation.Success)
        {
            await TryLogFingerprintFailureAsync(fingerprintValidation.Message, request, employee, device, context, cancellationToken);
            return new(false, fingerprintValidation.Message, null, null, false);
        }

        var duplicateWindow = await DuplicateWindowMinutesAsync(employee, context, cancellationToken);
        var fromUtc = punchUtc.AddMinutes(-duplicateWindow);
        var toUtc = punchUtc.AddMinutes(duplicateWindow);
        var duplicate = await WorkspaceScope.ApplyTo(db.AttendancePunches.AsNoTracking(), context)
            .Where(item => item.EmployeeId == employee.Id && item.PunchTimeUtc >= fromUtc && item.PunchTimeUtc <= toUtc && !item.Deleted)
            .OrderByDescending(item => item.PunchTimeUtc)
            .FirstOrDefaultAsync(cancellationToken);
        if (duplicate is not null)
        {
            var status = await BuildEmployeeDayStatusAsync(employee, localPunch.Date, context, cancellationToken);
            return new(false, $"Duplicate punch blocked within {duplicateWindow} minutes.", duplicate, status, true);
        }

        var entity = new AttendancePunch
        {
            Id = Guid.NewGuid(),
            CompanyId = device?.CompanyId ?? request.CompanyId,
            StoreGroupId = device?.StoreGroupId ?? request.StoreGroupId,
            StoreId = device?.StoreId ?? request.StoreId,
            EmployeeId = employee.Id,
            PunchType = punchType,
            PunchTimeUtc = punchUtc,
            LocalPunchTime = localPunch,
            Source = NormalizeSource(request.Source, requireDevice),
            DeviceId = device?.Id ?? request.DeviceId,
            DeviceCode = device?.DeviceCode ?? request.DeviceCode,
            VerificationStatus = fingerprintValidation.ProofAccepted ? "FingerprintMatched" : requireDevice ? "DeviceAccepted" : "ManualApproved",
            PhotoProofPath = Clean(request.PhotoProofPath, 300),
            ClientPunchId = Clean(request.ClientPunchId, 120),
            Latitude = request.Latitude,
            Longitude = request.Longitude,
            ConfidenceScore = request.ConfidenceScore ?? request.FingerprintProof?.QualityScore,
            Reason = Clean(request.Reason, 300),
            Remarks = Clean(MergeFingerprintRemarks(request.Remarks, request.FingerprintProof), 300),
            IsManual = !requireDevice,
            IsSynced = true,
            CreatedBy = context.User.Identity?.Name ?? context.User.FindFirst(ClaimTypes.Name)?.Value ?? context.User.FindFirst("userName")?.Value
        };

        if (!WorkspaceScope.CanWrite(entity, context, out var message))
        {
            return new(false, message ?? "Selected workspace is not allowed.", null, null, false);
        }

        db.AttendancePunches.Add(entity);
        await UpsertDailyAttendanceFromPunchAsync(employee, entity, context, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        var dayStatus = await BuildEmployeeDayStatusAsync(employee, localPunch.Date, context, cancellationToken);
        return new(true, $"{punchType} saved for {employee.StaffName}.", entity, dayStatus, false);
    }

    private async Task UpsertDailyAttendanceFromPunchAsync(Employee employee, AttendancePunch punch, HttpContext context, CancellationToken cancellationToken)
    {
        if (!IsDailyAttendancePunch(punch.PunchType))
        {
            return;
        }

        var onDate = punch.LocalPunchTime.Date;
        var punchTime = punch.LocalPunchTime.TimeOfDay;
        var daily = await db.Attendance
            .FirstOrDefaultAsync(item => item.EmployeeId == employee.Id && item.OnDate == onDate && !item.Deleted, cancellationToken);

        if (daily is null)
        {
            daily = new Garmetix.Core.Models.HRM.Attendance
            {
                Id = Guid.NewGuid(),
                CompanyId = employee.CompanyId,
                StoreGroupId = employee.StoreGroupId,
                StoreId = employee.StoreId,
                EmployeeId = employee.Id,
                OnDate = onDate,
                Status = AttendanceStatus.Present,
                CheckInTime = null,
                CheckOutTime = null,
                EntryTime = FormatTime(punchTime),
                Remarks = $"{punch.Source} {punch.PunchType} punch"
            };
            db.Attendance.Add(daily);
        }
        else
        {
            daily.CompanyId = employee.CompanyId;
            daily.StoreGroupId = employee.StoreGroupId;
            daily.StoreId = employee.StoreId;
            daily.UpdatedAt = DateTime.UtcNow;
            daily.Remarks = MergeRemarks(daily.Remarks, $"{punch.Source} {punch.PunchType} sync");
        }

        if (punch.PunchType.Equals("CheckIn", StringComparison.OrdinalIgnoreCase))
        {
            if (!daily.CheckInTime.HasValue || punchTime < daily.CheckInTime.Value)
            {
                daily.CheckInTime = punchTime;
                daily.EntryTime = FormatTime(punchTime);
            }
            daily.Status = AttendanceStatus.Present;
        }
        else if (punch.PunchType.Equals("BreakOut", StringComparison.OrdinalIgnoreCase))
        {
            if (!daily.BreakOutTime.HasValue || punchTime < daily.BreakOutTime.Value)
            {
                daily.BreakOutTime = punchTime;
            }
            daily.Status = AttendanceStatus.Present;
        }
        else if (punch.PunchType.Equals("BreakIn", StringComparison.OrdinalIgnoreCase))
        {
            if (!daily.BreakInTime.HasValue || punchTime > daily.BreakInTime.Value)
            {
                daily.BreakInTime = punchTime;
            }
            daily.Status = AttendanceStatus.Present;
        }
        else if (punch.PunchType.Equals("CheckOut", StringComparison.OrdinalIgnoreCase))
        {
            if (!daily.CheckOutTime.HasValue || punchTime > daily.CheckOutTime.Value)
            {
                daily.CheckOutTime = punchTime;
            }
            daily.Status = AttendanceStatus.Present;
        }

        var start = onDate;
        var end = start.AddDays(1);
        var existingPunches = await WorkspaceScope.ApplyTo(db.AttendancePunches.AsNoTracking(), context)
            .Where(item => item.EmployeeId == employee.Id && item.LocalPunchTime >= start && item.LocalPunchTime < end && !item.Deleted)
            .ToListAsync(cancellationToken);
        var calculationPunches = existingPunches.Any(item => item.Id == punch.Id)
            ? existingPunches
            : existingPunches.Concat(new[] { punch }).ToList();
        var shift = await ResolveEmployeeShiftAsync(employee, onDate, context, cancellationToken);
        var policy = await ResolveStorePolicyAsync(employee, context, cancellationToken);
        var dayStatus = ruleEngine.CalculateDay(employee, onDate, calculationPunches, shift, policy);
        daily.Status = MapDailyAttendanceStatus(dayStatus.Status);
        daily.Remarks = MergeRemarks(daily.Remarks, $"Shift: {dayStatus.ShiftName ?? "Default"}; sessions {dayStatus.CompletedSessions}/{dayStatus.RequiredSessionsForFullDay}; {dayStatus.Status}");
    }

    private static bool IsDailyAttendancePunch(string? punchType)
        => punchType is not null
            && (punchType.Equals("CheckIn", StringComparison.OrdinalIgnoreCase)
                || punchType.Equals("CheckOut", StringComparison.OrdinalIgnoreCase)
                || punchType.Equals("BreakOut", StringComparison.OrdinalIgnoreCase)
                || punchType.Equals("BreakIn", StringComparison.OrdinalIgnoreCase));

    private static string FormatTime(TimeSpan value)
        => value.ToString(@"hh\:mm");

    private static string? MergeRemarks(string? existing, string next)
    {
        if (string.IsNullOrWhiteSpace(existing)) return next;
        if (existing.Contains(next, StringComparison.OrdinalIgnoreCase)) return existing;
        return existing.Length + next.Length + 3 > 100 ? existing : $"{existing} | {next}";
    }

    public async Task<AttendanceTodayDto> BuildTodayAsync(HttpContext context, DateTime? onDate, CancellationToken cancellationToken)
    {
        var date = (onDate ?? IndiaNow()).Date;
        var employees = await WorkspaceScope.ApplyTo(db.Employees.AsNoTracking(), context)
            .Where(item => item.Working && !item.Deleted)
            .OrderBy(item => item.FirstName).ThenBy(item => item.LastName)
            .ToListAsync(cancellationToken);

        var rows = new List<AttendanceDayStatusDto>();
        foreach (var employee in employees)
        {
            rows.Add(await BuildEmployeeDayStatusAsync(employee, date, context, cancellationToken));
        }

        return new AttendanceTodayDto(date, employees.Count,
            rows.Count(item => item.Status == "Present"),
            rows.Count(item => item.Status == "Late"),
            rows.Count(item => item.Status == "HalfDay"),
            rows.Count(item => item.Status == "Absent"),
            rows.Count(item => item.NeedsReview),
            rows);
    }

    public async Task<AttendanceMonthlyDto> BuildMonthlyAsync(HttpContext context, Guid? employeeId, int year, int month, CancellationToken cancellationToken)
    {
        var start = new DateTime(year, month, 1);
        var days = DateTime.DaysInMonth(year, month);
        var employeesQuery = WorkspaceScope.ApplyTo(db.Employees.AsNoTracking(), context).Where(item => item.Working && !item.Deleted);
        if (employeeId.HasValue && employeeId.Value != Guid.Empty)
        {
            employeesQuery = employeesQuery.Where(item => item.Id == employeeId.Value);
        }

        var employees = await employeesQuery.OrderBy(item => item.FirstName).ThenBy(item => item.LastName).ToListAsync(cancellationToken);
        var rows = new List<AttendanceDayStatusDto>();
        foreach (var employee in employees)
        {
            for (var index = 0; index < days; index++)
            {
                rows.Add(await BuildEmployeeDayStatusAsync(employee, start.AddDays(index), context, cancellationToken));
            }
        }

        var locked = await WorkspaceScope.ApplyTo(db.AttendanceMonthlySummaries.AsNoTracking(), context)
            .AnyAsync(item => item.Year == year && item.Month == month && item.Locked && (!employeeId.HasValue || item.EmployeeId == employeeId.Value), cancellationToken);

        return new AttendanceMonthlyDto(year, month, employeeId, employees.Count,
            rows.Count(item => item.Status == "Present"),
            rows.Count(item => item.Status == "Late"),
            rows.Count(item => item.Status == "HalfDay"),
            rows.Count(item => item.Status == "Absent"),
            rows.Sum(item => item.OvertimeMinutes),
            locked,
            rows);
    }

    public async Task<AttendanceDeviceRegistrationDto> RegisterDeviceAsync(AttendanceDeviceRegisterRequest request, HttpContext context, CancellationToken cancellationToken)
    {
        var token = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)).Replace("+", "").Replace("/", "").Replace("=", "");
        var code = $"KIOSK-{DateTime.UtcNow:yyMMdd}-{RandomNumberGenerator.GetInt32(1000, 9999)}";
        var device = new AttendanceDevice
        {
            Id = Guid.NewGuid(),
            CompanyId = request.CompanyId,
            StoreGroupId = request.StoreGroupId,
            StoreId = request.StoreId,
            DeviceCode = code,
            DeviceName = string.IsNullOrWhiteSpace(request.DeviceName) ? code : request.DeviceName.Trim(),
            DeviceType = string.IsNullOrWhiteSpace(request.DeviceType) ? "WebKiosk" : request.DeviceType.Trim(),
            DeviceTokenHash = HashToken(token),
            AppVersion = Clean(request.AppVersion, 80),
            Notes = Clean(request.Notes, 200),
            Status = "Active",
            RegisteredAtUtc = DateTime.UtcNow,
            RegisteredByUserName = context.User.Identity?.Name ?? context.User.FindFirst(ClaimTypes.Name)?.Value ?? context.User.FindFirst("userName")?.Value
        };
        if (!WorkspaceScope.CanWrite(device, context, out var message))
        {
            throw new InvalidOperationException(message ?? "Selected device workspace is not allowed.");
        }
        db.AttendanceDevices.Add(device);
        await db.SaveChangesAsync(cancellationToken);
        return new AttendanceDeviceRegistrationDto(device.Id, device.DeviceCode, token, device.DeviceName, device.DeviceType, device.Status);
    }

    public async Task<AttendanceDevice?> ValidateDeviceAsync(Guid deviceId, string? deviceToken, CancellationToken cancellationToken)
    {
        if (deviceId == Guid.Empty || string.IsNullOrWhiteSpace(deviceToken))
        {
            return null;
        }

        var tokenHash = HashToken(deviceToken);
        return await db.AttendanceDevices
            .FirstOrDefaultAsync(item => item.Id == deviceId && item.DeviceTokenHash == tokenHash && item.Status == "Active" && item.RevokedAtUtc == null && !item.Deleted, cancellationToken);
    }

    private async Task<AttendanceDayStatusDto> BuildEmployeeDayStatusAsync(Employee employee, DateTime date, HttpContext context, CancellationToken cancellationToken)
    {
        var start = date.Date;
        var end = start.AddDays(1);
        var punches = await WorkspaceScope.ApplyTo(db.AttendancePunches.AsNoTracking(), context)
            .Where(item => item.EmployeeId == employee.Id && item.LocalPunchTime >= start && item.LocalPunchTime < end && !item.Deleted)
            .OrderBy(item => item.LocalPunchTime)
            .ToListAsync(cancellationToken);
        var shift = await ResolveEmployeeShiftAsync(employee, date, context, cancellationToken);
        var policy = await ResolveStorePolicyAsync(employee, context, cancellationToken);
        return ruleEngine.CalculateDay(employee, date, punches, shift, policy);
    }

    private async Task<string> DetectNextPunchTypeAsync(Employee employee, DateTime date, HttpContext context, CancellationToken cancellationToken)
    {
        var start = date.Date;
        var end = start.AddDays(1);
        var last = await WorkspaceScope.ApplyTo(db.AttendancePunches.AsNoTracking(), context)
            .Where(item => item.EmployeeId == employee.Id && item.LocalPunchTime >= start && item.LocalPunchTime < end && !item.Deleted)
            .OrderByDescending(item => item.LocalPunchTime)
            .FirstOrDefaultAsync(cancellationToken);
        var shift = await ResolveEmployeeShiftAsync(employee, date, context, cancellationToken);
        var needsBreak = shift?.HasBreak == true;

        if (last is null || last.PunchType.Equals("CheckOut", StringComparison.OrdinalIgnoreCase)) return "CheckIn";
        if (!needsBreak) return last.PunchType.Equals("CheckIn", StringComparison.OrdinalIgnoreCase) ? "CheckOut" : "CheckIn";
        if (last.PunchType.Equals("CheckIn", StringComparison.OrdinalIgnoreCase)) return "BreakOut";
        if (last.PunchType.Equals("BreakOut", StringComparison.OrdinalIgnoreCase)) return "BreakIn";
        if (last.PunchType.Equals("BreakIn", StringComparison.OrdinalIgnoreCase)) return "CheckOut";
        return "CheckOut";
    }

    private async Task<int> DuplicateWindowMinutesAsync(Employee employee, HttpContext context, CancellationToken cancellationToken)
    {
        var policy = await ResolveStorePolicyAsync(employee, context, cancellationToken);
        return Math.Clamp(policy?.DuplicateWindowMinutes ?? 5, 1, 60);
    }

    private async Task<AttendancePolicy?> ResolveStorePolicyAsync(Employee employee, HttpContext context, CancellationToken cancellationToken)
        => await WorkspaceScope.ApplyTo(db.AttendancePolicies.AsNoTracking(), context)
            .Where(item => item.Active && item.StoreId == employee.StoreId)
            .OrderByDescending(item => item.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);

    private async Task<AttendanceShift?> ResolveEmployeeShiftAsync(Employee employee, DateTime onDate, HttpContext context, CancellationToken cancellationToken)
    {
        var date = onDate.Date;
        var rules = await WorkspaceScope.ApplyTo(db.EmployeeAttendanceShiftRules.AsNoTracking(), context)
            .Where(item => item.Active && item.StoreId == employee.StoreId && item.EffectiveFrom.Date <= date && (!item.EffectiveTo.HasValue || item.EffectiveTo.Value.Date >= date) && !item.Deleted)
            .OrderBy(item => item.Priority)
            .ThenByDescending(item => item.CreatedAt)
            .ToListAsync(cancellationToken);

        var match = rules.FirstOrDefault(rule => MatchesShiftRule(rule, employee));
        if (match is not null)
        {
            var assigned = await WorkspaceScope.ApplyTo(db.AttendanceShifts.AsNoTracking(), context)
                .FirstOrDefaultAsync(item => item.Id == match.AttendanceShiftId && item.Active && !item.Deleted, cancellationToken);
            if (assigned is not null)
            {
                return assigned;
            }
        }

        return await WorkspaceScope.ApplyTo(db.AttendanceShifts.AsNoTracking(), context)
            .Where(item => item.Active && item.StoreId == employee.StoreId && !item.Deleted)
            .OrderBy(item => item.Name.Contains("Default") ? 0 : 1)
            .ThenByDescending(item => item.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);
    }

    private static bool MatchesShiftRule(EmployeeAttendanceShiftRule rule, Employee employee)
    {
        var type = (rule.RuleType ?? string.Empty).Trim();
        var value = (rule.MatchValue ?? string.Empty).Trim();

        if (type.Equals("Employee", StringComparison.OrdinalIgnoreCase))
        {
            return rule.EmployeeId == employee.Id;
        }

        if (type.Equals("Gender", StringComparison.OrdinalIgnoreCase))
        {
            return value.Equals(employee.Gender.ToString(), StringComparison.OrdinalIgnoreCase);
        }

        if (type.Equals("Category", StringComparison.OrdinalIgnoreCase))
        {
            return value.Equals(employee.Category.ToString(), StringComparison.OrdinalIgnoreCase);
        }

        if (type.Equals("Department", StringComparison.OrdinalIgnoreCase))
        {
            return !string.IsNullOrWhiteSpace(employee.Department) && value.Equals(employee.Department.Trim(), StringComparison.OrdinalIgnoreCase);
        }

        if (type.Equals("Designation", StringComparison.OrdinalIgnoreCase))
        {
            return !string.IsNullOrWhiteSpace(employee.Designation) && value.Equals(employee.Designation.Trim(), StringComparison.OrdinalIgnoreCase);
        }

        return type.Equals("StoreDefault", StringComparison.OrdinalIgnoreCase);
    }

    private static AttendanceStatus MapDailyAttendanceStatus(string? status)
    {
        if (status is not null && status.Equals("Absent", StringComparison.OrdinalIgnoreCase)) return AttendanceStatus.Absent;
        if (status is not null && status.Equals("HalfDay", StringComparison.OrdinalIgnoreCase)) return AttendanceStatus.HalfDay;
        return AttendanceStatus.Present;
    }

    internal static string HashToken(string token)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(token));
        return Convert.ToHexString(bytes);
    }

    private static readonly TimeZoneInfo IndiaTimeZone = ResolveIndiaTimeZone();

    private static TimeZoneInfo ResolveIndiaTimeZone()
    {
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById("Asia/Kolkata");
        }
        catch (TimeZoneNotFoundException)
        {
            return TimeZoneInfo.FindSystemTimeZoneById("India Standard Time");
        }
    }

    private static DateTime IndiaNow()
        => DateTime.SpecifyKind(TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, IndiaTimeZone), DateTimeKind.Unspecified);

    private static DateTime ResolvePunchUtc(AttendancePunchRequest request)
    {
        if (request.PunchTimeUtc.HasValue)
        {
            return NormalizeUtc(request.PunchTimeUtc.Value);
        }

        if (request.LocalPunchTime.HasValue)
        {
            return IndiaLocalToUtc(request.LocalPunchTime.Value);
        }

        return DateTime.UtcNow;
    }

    private static DateTime ResolveIndiaLocalPunchTime(AttendancePunchRequest request, DateTime punchUtc)
    {
        if (!request.LocalPunchTime.HasValue)
        {
            return IndiaLocalFromUtc(punchUtc);
        }

        var supplied = request.LocalPunchTime.Value;

        // Nuxt/kiosk pages were sending localPunchTime as new Date().toISOString().
        // That is UTC (Z), so storing it directly made attendance show UTC instead of India time.
        if (supplied.Kind == DateTimeKind.Utc || supplied.Kind == DateTimeKind.Local)
        {
            return IndiaLocalFromUtc(NormalizeUtc(supplied));
        }

        // Unspecified means the caller intentionally sent a local wall-clock time, e.g. 2026-06-24T18:05:00.
        return DateTime.SpecifyKind(supplied, DateTimeKind.Unspecified);
    }

    private static DateTime IndiaLocalFromUtc(DateTime utcValue)
        => DateTime.SpecifyKind(TimeZoneInfo.ConvertTimeFromUtc(NormalizeUtc(utcValue), IndiaTimeZone), DateTimeKind.Unspecified);

    private static DateTime IndiaLocalToUtc(DateTime localValue)
    {
        if (localValue.Kind == DateTimeKind.Utc)
        {
            return localValue;
        }

        if (localValue.Kind == DateTimeKind.Local)
        {
            return localValue.ToUniversalTime();
        }

        return TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(localValue, DateTimeKind.Unspecified), IndiaTimeZone);
    }

    private static DateTime NormalizeUtc(DateTime value)
        => value.Kind == DateTimeKind.Utc ? value : DateTime.SpecifyKind(value, DateTimeKind.Local).ToUniversalTime();

    private static string NormalizePunchType(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return "Auto";
        var normalized = value.Trim();
        return normalized.Equals("Auto", StringComparison.OrdinalIgnoreCase) ? "Auto" : AutoPunchTypes.FirstOrDefault(item => item.Equals(normalized, StringComparison.OrdinalIgnoreCase)) ?? "Auto";
    }

    private static string NormalizeSource(string? value, bool requireDevice)
    {
        if (string.IsNullOrWhiteSpace(value)) return requireDevice ? "Kiosk" : "Manual";
        var source = value.Trim();
        return source.Length > 40 ? source[..40] : source;
    }

    private static string? Clean(string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var trimmed = value.Trim();
        return trimmed.Length <= maxLength ? trimmed : trimmed[..maxLength];
    }

    private static FingerprintValidationResult ValidateFingerprintProof(
        AttendanceFingerprintProofDto? proof,
        Guid employeeId,
        AttendanceFingerprintOptions options,
        bool required)
    {
        if (!required && proof is null)
        {
            return FingerprintValidationResult.Accepted(false, "Fingerprint proof not required.");
        }

        if (proof is null)
        {
            return FingerprintValidationResult.Rejected("Fingerprint verification is required before kiosk punch.");
        }

        if (!proof.Success)
        {
            return FingerprintValidationResult.Rejected("Fingerprint verification failed. Please scan again.");
        }

        if (proof.RawPayloadStored)
        {
            return FingerprintValidationResult.Rejected("Fingerprint proof was rejected because raw biometric payload storage was reported.");
        }

        if (proof.EmployeeId.HasValue && proof.EmployeeId.Value != Guid.Empty && proof.EmployeeId.Value != employeeId)
        {
            return FingerprintValidationResult.Rejected("Fingerprint proof employee does not match selected employee.");
        }

        if (!FingerprintAcceptedStatuses.Any(item => item.Equals(proof.MatchStatus ?? string.Empty, StringComparison.OrdinalIgnoreCase)))
        {
            return FingerprintValidationResult.Rejected("Fingerprint proof does not show an accepted match.");
        }

        if ((proof.QualityScore ?? 0) < options.SafeMinQualityScore)
        {
            return FingerprintValidationResult.Rejected($"Fingerprint quality must be at least {options.SafeMinQualityScore}.");
        }

        if (!proof.AuditRef.HasValue || proof.AuditRef.Value == Guid.Empty)
        {
            return FingerprintValidationResult.Rejected("Fingerprint proof audit reference is missing.");
        }

        if (!proof.CapturedAtUtc.HasValue)
        {
            return FingerprintValidationResult.Rejected("Fingerprint proof capture time is missing.");
        }

        var age = DateTimeOffset.UtcNow - proof.CapturedAtUtc.Value.ToUniversalTime();
        if (age < TimeSpan.FromMinutes(-2) || age > TimeSpan.FromMinutes(options.SafeProofMaxAgeMinutes))
        {
            return FingerprintValidationResult.Rejected($"Fingerprint proof is older than {options.SafeProofMaxAgeMinutes} minutes.");
        }

        return FingerprintValidationResult.Accepted(true, "Fingerprint proof accepted.");
    }

    private async Task TryLogFingerprintFailureAsync(
        string message,
        AttendancePunchRequest request,
        Employee employee,
        AttendanceDevice? device,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        try
        {
            await logs.ErrorAsync(
                "Attendance Fingerprint Guard",
                "KioskPunchBlocked",
                message,
                new
                {
                    request.EmployeeId,
                    employee.EmployeeCode,
                    device?.DeviceCode,
                    request.ClientPunchId,
                    request.FingerprintProof?.MatchStatus,
                    request.FingerprintProof?.QualityScore,
                    request.FingerprintProof?.AuditRef,
                    request.FingerprintProof?.RawPayloadStored
                },
                device?.CompanyId ?? request.CompanyId,
                device?.StoreGroupId ?? request.StoreGroupId,
                device?.StoreId ?? request.StoreId,
                null,
                context.User.Identity?.Name ?? "KioskDevice",
                "/attendance/kiosk",
                request.FingerprintProof?.AuditRef,
                cancellationToken);
        }
        catch
        {
            // Punch validation must return the clean business error even if log storage is unavailable.
        }
    }

    private static string? MergeFingerprintRemarks(string? remarks, AttendanceFingerprintProofDto? proof)
    {
        if (proof?.AuditRef is null || proof.AuditRef == Guid.Empty)
        {
            return remarks;
        }

        var fingerprintText = $"Fingerprint audit {proof.AuditRef}; match {proof.MatchStatus}; quality {proof.QualityScore ?? 0}; template {Clean(proof.TemplateRef, 80) ?? "-"}";
        return string.IsNullOrWhiteSpace(remarks) ? fingerprintText : $"{remarks.Trim()} | {fingerprintText}";
    }

    private sealed record FingerprintValidationResult(bool Success, bool ProofAccepted, string Message)
    {
        public static FingerprintValidationResult Accepted(bool proofAccepted, string message) => new(true, proofAccepted, message);
        public static FingerprintValidationResult Rejected(string message) => new(false, false, message);
    }
}

public sealed class AttendanceFingerprintOptions
{
    public string KioskPunchMode { get; init; } = "Off";
    public string? RequiredStoreIdsCsv { get; init; }
    public string BridgeBaseUrl { get; init; } = "http://127.0.0.1:8787/garmetix-fingerprint/";
    public int MinQualityScore { get; init; } = 60;
    public int ProofMaxAgeMinutes { get; init; } = 10;
    public bool OfflineQueueAllowed { get; init; } = true;

    public int SafeMinQualityScore => Math.Clamp(MinQualityScore, 0, 100);
    public int SafeProofMaxAgeMinutes => Math.Clamp(ProofMaxAgeMinutes, 1, 120);
    public string SafeBridgeBaseUrl => string.IsNullOrWhiteSpace(BridgeBaseUrl) ? "http://127.0.0.1:8787/garmetix-fingerprint/" : BridgeBaseUrl.Trim();

    public bool IsRequiredForStore(Guid storeId)
    {
        var mode = (KioskPunchMode ?? "Off").Trim();
        if (mode.Equals("Off", StringComparison.OrdinalIgnoreCase) || mode.Equals("Disabled", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (mode.Equals("RequiredAll", StringComparison.OrdinalIgnoreCase) || mode.Equals("Required", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (!mode.Equals("RequiredForConfiguredStores", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return RequiredStoreIds().Contains(storeId);
    }

    private HashSet<Guid> RequiredStoreIds()
        => (RequiredStoreIdsCsv ?? string.Empty)
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(value => Guid.TryParse(value, out var parsed) ? parsed : Guid.Empty)
            .Where(value => value != Guid.Empty)
            .ToHashSet();
}

public sealed class AttendanceSyncService(IAttendanceService attendanceService, GarmetixDbContext db) : IAttendanceSyncService
{
    public async Task<AttendanceSyncPendingResult> SyncPendingAsync(AttendanceSyncPendingRequest request, HttpContext context, CancellationToken cancellationToken)
    {
        var device = await attendanceService.ValidateDeviceAsync(request.DeviceId, request.DeviceToken, cancellationToken);
        if (device is null)
        {
            return new AttendanceSyncPendingResult(0, 0, request.Punches.Count, [new AttendancePunchResultDto(false, "Kiosk device is invalid, revoked, or token does not match.", null, null, false)]);
        }

        var batch = new AttendanceKioskSyncBatch
        {
            Id = Guid.NewGuid(),
            CompanyId = device.CompanyId,
            StoreGroupId = device.StoreGroupId,
            StoreId = device.StoreId,
            DeviceId = device.Id,
            DeviceCode = device.DeviceCode,
            BatchClientId = request.Punches.FirstOrDefault()?.ClientPunchId,
            TotalCount = request.Punches.Count,
            Status = "Processing",
            ReceivedAtUtc = DateTime.UtcNow,
            CreatedBy = "KioskDevice"
        };
        db.AttendanceKioskSyncBatches.Add(batch);
        await db.SaveChangesAsync(cancellationToken);

        var results = new List<AttendancePunchResultDto>();
        foreach (var punch in request.Punches)
        {
            var enriched = punch with { DeviceId = request.DeviceId, DeviceToken = request.DeviceToken, Source = string.IsNullOrWhiteSpace(punch.Source) ? "DeviceSync" : punch.Source };
            results.Add(await attendanceService.RecordPunchAsync(enriched, context, requireDevice: true, cancellationToken));
        }

        var response = new AttendanceSyncPendingResult(
            results.Count(item => item.Success),
            results.Count(item => item.Duplicate),
            results.Count(item => !item.Success && !item.Duplicate),
            results);

        batch.AcceptedCount = response.Accepted;
        batch.DuplicateCount = response.Duplicate;
        batch.FailedCount = response.Failed;
        batch.Status = response.Failed == 0 ? "Completed" : response.Accepted > 0 ? "CompletedWithErrors" : "Failed";
        batch.CompletedAtUtc = DateTime.UtcNow;
        batch.ResultJson = JsonSerializer.Serialize(new { response.Accepted, response.Duplicate, response.Failed });
        await db.SaveChangesAsync(cancellationToken);
        return response;
    }
}

public sealed class BiometricEnrollmentService(GarmetixDbContext db, ApplicationMessageLogService logs) : IBiometricEnrollmentService
{
    private static readonly string[] BlockedTemplateTokens =
    [
        "rawImage",
        "fingerprintImage",
        "wsq",
        "minutiae",
        "isoTemplate",
        "templateBase64",
        "biometricPayload"
    ];

    public async Task<EmployeeBiometricEnrollment> SaveAsync(BiometricEnrollmentSaveRequest request, HttpContext context, CancellationToken cancellationToken)
    {
        if (request.EmployeeId == Guid.Empty)
        {
            throw new InvalidOperationException("Select employee before saving biometric enrollment.");
        }

        var employee = await WorkspaceScope.ApplyTo(db.Employees.AsNoTracking(), context)
            .FirstOrDefaultAsync(item => item.Id == request.EmployeeId, cancellationToken);
        if (employee is null)
        {
            throw new InvalidOperationException("Selected employee is outside your workspace or does not exist.");
        }

        var fingerprintRef = CleanReference(request.FingerprintTemplateRef, nameof(request.FingerprintTemplateRef));
        var faceRef = CleanReference(request.FaceTemplateRef, nameof(request.FaceTemplateRef));
        var webAuthnRef = CleanReference(request.WebAuthnCredentialId, nameof(request.WebAuthnCredentialId));
        var hasTemplateReference = fingerprintRef is not null || faceRef is not null || webAuthnRef is not null;
        if (hasTemplateReference && !request.ConsentGiven)
        {
            throw new InvalidOperationException("Employee consent is required before saving biometric template references.");
        }

        EmployeeBiometricEnrollment? entity = null;
        if (request.Id.HasValue && request.Id.Value != Guid.Empty)
        {
            entity = await WorkspaceScope.ApplyTo(db.EmployeeBiometricEnrollments, context)
                .FirstOrDefaultAsync(item => item.Id == request.Id.Value, cancellationToken);
        }

        entity ??= await WorkspaceScope.ApplyTo(db.EmployeeBiometricEnrollments, context)
            .Where(item => item.EmployeeId == request.EmployeeId && item.RevokedAtUtc == null)
            .OrderByDescending(item => item.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);

        var isNew = entity is null;
        entity ??= new EmployeeBiometricEnrollment { Id = Guid.NewGuid(), EmployeeId = request.EmployeeId };

        entity.EmployeeId = request.EmployeeId;
        entity.CompanyId = employee.CompanyId;
        entity.StoreGroupId = employee.StoreGroupId;
        entity.StoreId = employee.StoreId;
        entity.ConsentGiven = request.ConsentGiven;
        entity.ConsentAtUtc = request.ConsentGiven ? entity.ConsentAtUtc ?? DateTime.UtcNow : null;
        entity.FaceTemplateRef = faceRef;
        entity.FingerprintTemplateRef = fingerprintRef;
        entity.WebAuthnCredentialId = webAuthnRef;
        entity.EnrollmentStatus = request.ConsentGiven
            ? hasTemplateReference ? "Enrolled" : "ConsentOnly"
            : "NotEnrolled";
        entity.EnrolledAtUtc = hasTemplateReference ? entity.EnrolledAtUtc ?? DateTime.UtcNow : null;
        entity.RevokedAtUtc = null;
        entity.RevokedReason = null;
        entity.Notes = BuildNotes(request);

        if (!WorkspaceScope.CanWrite(entity, context, out var message))
        {
            throw new InvalidOperationException(message ?? "Selected biometric workspace is not allowed.");
        }

        if (isNew)
        {
            db.EmployeeBiometricEnrollments.Add(entity);
        }

        await db.SaveChangesAsync(cancellationToken);
        await logs.SuccessAsync(
            "Attendance Biometric Enrollment",
            isNew ? "EnrollmentCreated" : "EnrollmentUpdated",
            hasTemplateReference
                ? "Biometric enrollment reference saved after consent validation."
                : "Biometric enrollment consent status saved.",
            new
            {
                entity.EmployeeId,
                employee.EmployeeCode,
                EmployeeName = $"{employee.FirstName} {employee.LastName}".Trim(),
                entity.EnrollmentStatus,
                HasFingerprintTemplateRef = entity.FingerprintTemplateRef is not null,
                HasFaceTemplateRef = entity.FaceTemplateRef is not null,
                HasWebAuthnCredentialId = entity.WebAuthnCredentialId is not null,
                entity.ConsentGiven,
                RawBiometricPayloadStored = false
            },
            entity.CompanyId,
            entity.StoreGroupId,
            entity.StoreId,
            userName: context.User.Identity?.Name ?? context.User.FindFirst(ClaimTypes.Name)?.Value ?? context.User.FindFirst("userName")?.Value,
            resource: "/attendance/biometric-enrollment",
            operationId: entity.Id,
            cancellationToken: cancellationToken);

        return entity;
    }

    private static string? CleanReference(string? value, string fieldName)
    {
        var cleaned = Clean(value, 300);
        if (cleaned is null)
        {
            return null;
        }

        if (cleaned.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"{fieldName} must be a reference, not raw biometric data.");
        }

        foreach (var token in BlockedTemplateTokens)
        {
            if (cleaned.Contains(token, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException($"{fieldName} contains a blocked raw biometric field marker: {token}.");
            }
        }

        return cleaned;
    }

    private static string? BuildNotes(BiometricEnrollmentSaveRequest request)
    {
        var parts = new List<string>();
        AddPart(parts, "Consent", request.ConsentMethod);
        AddPart(parts, "ConsentRef", request.ConsentReference);
        AddPart(parts, "Provider", request.TemplateProvider);
        AddPart(parts, "Device", request.DeviceSerial);
        AddPart(parts, "Note", request.Notes);
        return Clean(string.Join("; ", parts), 300);
    }

    private static void AddPart(List<string> parts, string label, string? value)
    {
        var cleaned = Clean(value, 80);
        if (cleaned is not null)
        {
            parts.Add($"{label}: {cleaned}");
        }
    }

    private static string? Clean(string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var trimmed = value.Trim();
        return trimmed.Length <= maxLength ? trimmed : trimmed[..maxLength];
    }
}
