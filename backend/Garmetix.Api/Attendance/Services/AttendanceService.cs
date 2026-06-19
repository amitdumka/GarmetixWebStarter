using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Garmetix.Api.AppInfo;
using Garmetix.Api.Attendance.Dtos;
using Garmetix.Api.Workspace;
using Garmetix.Core.Models.Attendance;
using Garmetix.Core.Models.HRM;
using Garmetix.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

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
    Task<EmployeeBiometricEnrollment> SavePlaceholderAsync(EmployeeBiometricEnrollment request, HttpContext context, CancellationToken cancellationToken);
}

public sealed class AttendanceService(GarmetixDbContext db, IAttendanceRuleEngine ruleEngine) : IAttendanceService
{
    private static readonly string[] AutoPunchTypes = ["CheckIn", "CheckOut", "BreakIn", "BreakOut"];

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

        var punchUtc = NormalizeUtc(request.PunchTimeUtc ?? DateTime.UtcNow);
        var localPunch = request.LocalPunchTime ?? punchUtc.ToLocalTime();
        var punchType = NormalizePunchType(request.PunchType);
        if (punchType == "Auto")
        {
            punchType = await DetectNextPunchTypeAsync(employee.Id, localPunch.Date, context, cancellationToken);
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
            VerificationStatus = requireDevice ? "DeviceAccepted" : "ManualApproved",
            PhotoProofPath = Clean(request.PhotoProofPath, 300),
            ClientPunchId = Clean(request.ClientPunchId, 120),
            Latitude = request.Latitude,
            Longitude = request.Longitude,
            ConfidenceScore = request.ConfidenceScore,
            Reason = Clean(request.Reason, 300),
            Remarks = Clean(request.Remarks, 300),
            IsManual = !requireDevice,
            IsSynced = true,
            CreatedBy = context.User.Identity?.Name ?? context.User.FindFirstValue(ClaimTypes.Name) ?? context.User.FindFirstValue("userName")
        };

        if (!WorkspaceScope.CanWrite(entity, context, out var message))
        {
            return new(false, message ?? "Selected workspace is not allowed.", null, null, false);
        }

        db.AttendancePunches.Add(entity);
        await db.SaveChangesAsync(cancellationToken);
        var dayStatus = await BuildEmployeeDayStatusAsync(employee, localPunch.Date, context, cancellationToken);
        return new(true, $"{punchType} saved for {employee.StaffName}.", entity, dayStatus, false);
    }

    public async Task<AttendanceTodayDto> BuildTodayAsync(HttpContext context, DateTime? onDate, CancellationToken cancellationToken)
    {
        var date = (onDate ?? DateTime.Today).Date;
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
            RegisteredByUserName = context.User.Identity?.Name ?? context.User.FindFirstValue(ClaimTypes.Name) ?? context.User.FindFirstValue("userName")
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
        var shift = await WorkspaceScope.ApplyTo(db.AttendanceShifts.AsNoTracking(), context)
            .Where(item => item.Active && item.StoreId == employee.StoreId)
            .OrderByDescending(item => item.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);
        var policy = await WorkspaceScope.ApplyTo(db.AttendancePolicies.AsNoTracking(), context)
            .Where(item => item.Active && item.StoreId == employee.StoreId)
            .OrderByDescending(item => item.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);
        return ruleEngine.CalculateDay(employee, date, punches, shift, policy);
    }

    private async Task<string> DetectNextPunchTypeAsync(Guid employeeId, DateTime date, HttpContext context, CancellationToken cancellationToken)
    {
        var start = date.Date;
        var end = start.AddDays(1);
        var last = await WorkspaceScope.ApplyTo(db.AttendancePunches.AsNoTracking(), context)
            .Where(item => item.EmployeeId == employeeId && item.LocalPunchTime >= start && item.LocalPunchTime < end && !item.Deleted)
            .OrderByDescending(item => item.LocalPunchTime)
            .FirstOrDefaultAsync(cancellationToken);
        return last is null || last.PunchType.Equals("CheckOut", StringComparison.OrdinalIgnoreCase) ? "CheckIn" : "CheckOut";
    }

    private async Task<int> DuplicateWindowMinutesAsync(Employee employee, HttpContext context, CancellationToken cancellationToken)
    {
        var policy = await WorkspaceScope.ApplyTo(db.AttendancePolicies.AsNoTracking(), context)
            .Where(item => item.Active && item.StoreId == employee.StoreId)
            .OrderByDescending(item => item.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);
        return Math.Clamp(policy?.DuplicateWindowMinutes ?? 5, 1, 60);
    }

    internal static string HashToken(string token)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(token));
        return Convert.ToHexString(bytes);
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

public sealed class BiometricEnrollmentService(GarmetixDbContext db) : IBiometricEnrollmentService
{
    public async Task<EmployeeBiometricEnrollment> SavePlaceholderAsync(EmployeeBiometricEnrollment request, HttpContext context, CancellationToken cancellationToken)
    {
        request.Id = request.Id == Guid.Empty ? Guid.NewGuid() : request.Id;
        request.EnrollmentStatus = request.ConsentGiven ? "ConsentOnly" : "NotEnrolled";
        request.ConsentAtUtc = request.ConsentGiven ? request.ConsentAtUtc ?? DateTime.UtcNow : null;
        request.FaceTemplateRef = string.IsNullOrWhiteSpace(request.FaceTemplateRef) ? null : request.FaceTemplateRef.Trim();
        request.FingerprintTemplateRef = string.IsNullOrWhiteSpace(request.FingerprintTemplateRef) ? null : request.FingerprintTemplateRef.Trim();
        request.WebAuthnCredentialId = string.IsNullOrWhiteSpace(request.WebAuthnCredentialId) ? null : request.WebAuthnCredentialId.Trim();
        if (!WorkspaceScope.CanWrite(request, context, out var message))
        {
            throw new InvalidOperationException(message ?? "Selected biometric workspace is not allowed.");
        }
        db.EmployeeBiometricEnrollments.Add(request);
        await db.SaveChangesAsync(cancellationToken);
        return request;
    }
}
