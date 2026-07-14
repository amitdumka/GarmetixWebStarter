using Garmetix.Api.Attendance.Dtos;
using Garmetix.Core.Models.Attendance;
using Garmetix.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Garmetix.Api.Attendance.Services;

public interface IAttendancePhotoProofService
{
    Task<AttendancePhotoProofDto> SavePhotoProofAsync(AttendancePhotoProofRequest request, CancellationToken cancellationToken);
}

public sealed class AttendancePhotoProofService(
    GarmetixDbContext db,
    IAttendanceService attendanceService,
    IConfiguration configuration,
    ILogger<AttendancePhotoProofService> logger) : IAttendancePhotoProofService
{
    private static readonly Dictionary<string, string> SupportedImageTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        ["image/jpeg"] = ".jpg",
        ["image/jpg"] = ".jpg",
        ["image/png"] = ".png",
        ["image/webp"] = ".webp"
    };

    public async Task<AttendancePhotoProofDto> SavePhotoProofAsync(AttendancePhotoProofRequest request, CancellationToken cancellationToken)
    {
        var maxBytes = configuration.GetValue<long?>("AttendancePhotoProof:MaxBytes") ?? 1_500_000;
        if (request.EmployeeId == Guid.Empty)
        {
            return new(false, "Employee is required for photo proof.", null, null, 0, "Rejected");
        }

        var device = await attendanceService.ValidateDeviceAsync(request.DeviceId, request.DeviceToken, cancellationToken);
        if (device is null)
        {
            return new(false, "Kiosk device is invalid, revoked, or token does not match.", null, null, 0, "Rejected");
        }

        var employee = await db.Employees.AsNoTracking()
            .Where(item => item.Id == request.EmployeeId && item.CompanyId == device.CompanyId && item.StoreId == device.StoreId && item.Working && !item.Deleted)
            .FirstOrDefaultAsync(cancellationToken);
        if (employee is null)
        {
            return new(false, "Active employee was not found for this kiosk store.", null, null, 0, "Rejected");
        }

        if (!TryDecodeDataUrl(request.DataUrl, out var contentType, out var bytes, out var extension, out var error))
        {
            return new(false, error ?? "Photo proof image is invalid.", null, null, 0, "Rejected");
        }

        if (bytes.LongLength > maxBytes)
        {
            return new(false, $"Photo proof exceeds limit of {maxBytes} bytes.", null, null, bytes.LongLength, "Rejected");
        }

        var captureUtc = NormalizeUtc(request.CapturedAtUtc ?? DateTime.UtcNow);
        var root = configuration["AttendancePhotoProof:StoragePath"];
        if (string.IsNullOrWhiteSpace(root))
        {
            root = Path.Combine(AppContext.BaseDirectory, "attendance-photo-proof");
        }

        var relativeDir = Path.Combine(captureUtc.Year.ToString("0000"), captureUtc.Month.ToString("00"), device.StoreId.ToString("N"));
        var absoluteDir = Path.Combine(root, relativeDir);
        Directory.CreateDirectory(absoluteDir);

        var safeClientId = SanitizeFilePart(request.ClientPunchId, 32);
        var fileName = $"{employee.Id:N}_{captureUtc:yyyyMMddHHmmss}_{safeClientId}_{Guid.NewGuid():N}{extension}";
        var absolutePath = Path.Combine(absoluteDir, fileName);
        await File.WriteAllBytesAsync(absolutePath, bytes, cancellationToken);

        var proofPath = Path.Combine("attendance-photo-proof", relativeDir, fileName).Replace('\\', '/');
        var retentionDays = configuration.GetValue<int?>("AttendancePhotoProof:RetentionDays") ?? 180;
        var entity = new AttendancePhotoProof
        {
            Id = Guid.NewGuid(),
            CompanyId = device.CompanyId,
            StoreGroupId = device.StoreGroupId,
            StoreId = device.StoreId,
            EmployeeId = employee.Id,
            DeviceId = device.Id,
            DeviceCode = device.DeviceCode,
            ClientPunchId = Clean(request.ClientPunchId, 120),
            ProofPath = proofPath,
            ContentType = contentType,
            SizeBytes = bytes.LongLength,
            CapturedAtUtc = captureUtc,
            UploadedAtUtc = DateTime.UtcNow,
            RetentionUntilUtc = captureUtc.AddDays(retentionDays),
            VerificationStatus = "PhotoProofOnly",
            Remarks = Clean(request.Remarks, 300),
            CreatedBy = "KioskDevice"
        };

        db.AttendancePhotoProofs.Add(entity);
        await db.SaveChangesAsync(cancellationToken);
        logger.LogInformation("Attendance photo proof saved for employee {EmployeeId} from device {DeviceCode}.", employee.Id, device.DeviceCode);
        return new(true, "Photo proof saved. Use the returned path in kiosk punch.", entity.Id, entity.ProofPath, entity.SizeBytes, entity.VerificationStatus);
    }

    private static bool TryDecodeDataUrl(string? dataUrl, out string contentType, out byte[] bytes, out string extension, out string? error)
    {
        contentType = string.Empty;
        bytes = Array.Empty<byte>();
        extension = ".jpg";
        error = null;

        if (string.IsNullOrWhiteSpace(dataUrl) || !dataUrl.StartsWith("data:image/", StringComparison.OrdinalIgnoreCase))
        {
            error = "Photo proof must be a data:image/* base64 URL.";
            return false;
        }

        var commaIndex = dataUrl.IndexOf(',');
        if (commaIndex <= 5)
        {
            error = "Photo proof data URL is malformed.";
            return false;
        }

        var header = dataUrl[..commaIndex];
        var payload = dataUrl[(commaIndex + 1)..];
        var semicolon = header.IndexOf(';');
        contentType = semicolon > 5 ? header[5..semicolon] : header[5..];
        if (!SupportedImageTypes.TryGetValue(contentType, out extension!))
        {
            error = "Only JPEG, PNG, and WEBP photo proof images are accepted.";
            return false;
        }

        try
        {
            bytes = Convert.FromBase64String(payload);
            return bytes.Length > 0;
        }
        catch (FormatException)
        {
            error = "Photo proof image payload is not valid base64.";
            return false;
        }
    }

    private static DateTime NormalizeUtc(DateTime value)
        => value.Kind == DateTimeKind.Utc ? value : DateTime.SpecifyKind(value, DateTimeKind.Unspecified).ToUniversalTime();

    private static string? Clean(string? value, int max)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        value = value.Trim();
        return value.Length <= max ? value : value[..max];
    }

    private static string SanitizeFilePart(string? value, int max)
    {
        if (string.IsNullOrWhiteSpace(value)) return "nopunch";
        var chars = value.Where(char.IsLetterOrDigit).Take(max).ToArray();
        return chars.Length == 0 ? "nopunch" : new string(chars);
    }
}
