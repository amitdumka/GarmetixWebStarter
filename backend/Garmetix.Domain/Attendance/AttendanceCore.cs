using Garmetix.Core.Models.Base;
using Garmetix.Core.Models.HRM;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace Garmetix.Core.Models.Attendance;

public class AttendanceDevice : StoreBase
{
    [MaxLength(40)] public string DeviceCode { get; set; } = string.Empty;
    [MaxLength(120)] public string DeviceName { get; set; } = string.Empty;
    [MaxLength(40)] public string DeviceType { get; set; } = "WebKiosk";
    [MaxLength(120)] public string DeviceTokenHash { get; set; } = string.Empty;
    [MaxLength(40)] public string Status { get; set; } = "Active";
    [MaxLength(80)] public string? AppVersion { get; set; }
    [MaxLength(200)] public string? Notes { get; set; }
    public DateTime RegisteredAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? LastSeenAtUtc { get; set; }
    public DateTime? RevokedAtUtc { get; set; }
    public Guid? RegisteredByUserId { get; set; }
    [MaxLength(120)] public string? RegisteredByUserName { get; set; }
}

public class AttendancePunch : StoreBase
{
    public Guid EmployeeId { get; set; }
    [MaxLength(20)] public string PunchType { get; set; } = "CheckIn";
    public DateTime PunchTimeUtc { get; set; } = DateTime.UtcNow;
    public DateTime LocalPunchTime { get; set; } = DateTime.Now;
    [MaxLength(40)] public string Source { get; set; } = "Manual";
    public Guid? DeviceId { get; set; }
    [MaxLength(40)] public string? DeviceCode { get; set; }
    [MaxLength(40)] public string VerificationStatus { get; set; } = "ManualApproved";
    [MaxLength(300)] public string? PhotoProofPath { get; set; }
    [MaxLength(120)] public string? ClientPunchId { get; set; }
    public decimal? Latitude { get; set; }
    public decimal? Longitude { get; set; }
    public decimal? ConfidenceScore { get; set; }
    public bool IsManual { get; set; }
    public bool IsSynced { get; set; } = true;
    public Guid? DuplicateOfPunchId { get; set; }
    [MaxLength(300)] public string? Reason { get; set; }
    [MaxLength(300)] public string? Remarks { get; set; }

    [JsonIgnore] public virtual Employee? Employee { get; set; }
    [JsonIgnore] public virtual AttendanceDevice? Device { get; set; }
}

public class AttendanceShift : StoreBase
{
    [MaxLength(120)] public string Name { get; set; } = "Default Shift";
    public int StartTimeMinutes { get; set; } = 600;
    public int EndTimeMinutes { get; set; } = 1200;
    public int GraceMinutes { get; set; } = 10;
    public int LateAfterMinutes { get; set; } = 10;
    public int HalfDayAfterMinutes { get; set; } = 750;
    public int MinimumFullDayMinutes { get; set; } = 480;
    public int MinimumHalfDayMinutes { get; set; } = 240;
    public int OvertimeAfterMinutes { get; set; } = 540;
    public bool AutoCheckoutEnabled { get; set; }
    public int? AutoCheckoutTimeMinutes { get; set; }
    [MaxLength(80)] public string WeeklyOffDays { get; set; } = "Sunday";
    public bool Active { get; set; } = true;
}

public class AttendancePolicy : StoreBase
{
    [MaxLength(120)] public string Name { get; set; } = "Default Attendance Policy";
    public int GraceMinutes { get; set; } = 10;
    public int LateAfterMinutes { get; set; } = 10;
    public int HalfDayAfterMinutes { get; set; } = 750;
    public int MinimumFullDayMinutes { get; set; } = 480;
    public int MinimumHalfDayMinutes { get; set; } = 240;
    public int OvertimeAfterMinutes { get; set; } = 540;
    public bool AutoCheckoutEnabled { get; set; }
    public int? AutoCheckoutAfterMinutes { get; set; }
    public int DuplicateWindowMinutes { get; set; } = 5;
    public bool Active { get; set; } = true;
}

public class EmployeeBiometricEnrollment : StoreBase
{
    public Guid EmployeeId { get; set; }
    public bool ConsentGiven { get; set; }
    public DateTime? ConsentAtUtc { get; set; }
    [MaxLength(300)] public string? FacePhotoPath { get; set; }
    [MaxLength(300)] public string? FaceTemplateRef { get; set; }
    [MaxLength(300)] public string? FingerprintTemplateRef { get; set; }
    [MaxLength(300)] public string? WebAuthnCredentialId { get; set; }
    [MaxLength(40)] public string EnrollmentStatus { get; set; } = "NotEnrolled";
    public DateTime? EnrolledAtUtc { get; set; }
    public DateTime? RevokedAtUtc { get; set; }
    [MaxLength(300)] public string? RevokedReason { get; set; }
    [MaxLength(300)] public string? Notes { get; set; }

    [JsonIgnore] public virtual Employee? Employee { get; set; }
}

public class AttendanceRegularizationRequest : StoreBase
{
    public Guid EmployeeId { get; set; }
    public Guid? AttendancePunchId { get; set; }
    [MaxLength(40)] public string RequestType { get; set; } = "MissedPunch";
    [MaxLength(20)] public string RequestedPunchType { get; set; } = "CheckIn";
    public DateTime? RequestedPunchTimeUtc { get; set; }
    public DateTime? RequestedLocalPunchTime { get; set; }
    [MaxLength(300)] public string Reason { get; set; } = string.Empty;
    [MaxLength(40)] public string Status { get; set; } = "Pending";
    [MaxLength(120)] public string? RequestedBy { get; set; }
    [MaxLength(120)] public string? ApprovedBy { get; set; }
    public DateTime? ApprovedAtUtc { get; set; }
    [MaxLength(300)] public string? RejectionReason { get; set; }

    [JsonIgnore] public virtual Employee? Employee { get; set; }
}

public class AttendanceApproval : StoreBase
{
    public Guid RequestId { get; set; }
    public bool Approved { get; set; }
    [MaxLength(40)] public string Decision { get; set; } = "Approved";
    [MaxLength(300)] public string? Remarks { get; set; }
    [MaxLength(120)] public string? ApprovedBy { get; set; }
    public DateTime ApprovedAtUtc { get; set; } = DateTime.UtcNow;
}

public class AttendanceMonthlySummary : StoreBase
{
    public Guid EmployeeId { get; set; }
    public int Year { get; set; }
    public int Month { get; set; }
    public decimal PresentDays { get; set; }
    public decimal AbsentDays { get; set; }
    public decimal LateDays { get; set; }
    public decimal HalfDays { get; set; }
    public decimal LeaveDays { get; set; }
    public int WorkingMinutes { get; set; }
    public int OvertimeMinutes { get; set; }
    public bool Locked { get; set; }
    public DateTime? LockedAtUtc { get; set; }
    [MaxLength(120)] public string? LockedBy { get; set; }
    public string? SummaryJson { get; set; }

    [JsonIgnore] public virtual Employee? Employee { get; set; }
}


public class AttendancePayrollReview : StoreBase
{
    public Guid EmployeeId { get; set; }
    public int Year { get; set; }
    public int Month { get; set; }
    public decimal PresentDays { get; set; }
    public decimal AbsentDays { get; set; }
    public decimal LateDays { get; set; }
    public decimal HalfDays { get; set; }
    public decimal LeaveDays { get; set; }
    public decimal PayableDays { get; set; }
    public decimal DeductionDays { get; set; }
    public int WorkingMinutes { get; set; }
    public int OvertimeMinutes { get; set; }
    public decimal EstimatedDailyRate { get; set; }
    public decimal EstimatedGrossPay { get; set; }
    [MaxLength(40)] public string ReviewStatus { get; set; } = "Draft";
    [MaxLength(40)] public string PayrollActionStatus { get; set; } = "NotPosted";
    public bool Locked { get; set; }
    public DateTime? LockedAtUtc { get; set; }
    [MaxLength(120)] public string? ReviewedBy { get; set; }
    public DateTime? ReviewedAtUtc { get; set; }
    [MaxLength(300)] public string? Notes { get; set; }
    public string? SourceSummaryJson { get; set; }

    [JsonIgnore] public virtual Employee? Employee { get; set; }
}

public class AttendancePhotoProof : StoreBase
{
    public Guid EmployeeId { get; set; }
    public Guid? DeviceId { get; set; }
    [MaxLength(40)] public string? DeviceCode { get; set; }
    [MaxLength(120)] public string? ClientPunchId { get; set; }
    [MaxLength(500)] public string ProofPath { get; set; } = string.Empty;
    [MaxLength(80)] public string ContentType { get; set; } = "image/jpeg";
    public long SizeBytes { get; set; }
    public DateTime CapturedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime UploadedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? RetentionUntilUtc { get; set; }
    [MaxLength(40)] public string VerificationStatus { get; set; } = "PhotoProofOnly";
    [MaxLength(40)] public string ReviewStatus { get; set; } = "PendingReview";
    public DateTime? ReviewedAtUtc { get; set; }
    [MaxLength(120)] public string? ReviewedBy { get; set; }
    [MaxLength(300)] public string? ReviewRemarks { get; set; }
    [MaxLength(80)] public string? ReviewReason { get; set; }
    public Guid? RegularizationRequestId { get; set; }
    [MaxLength(300)] public string? Remarks { get; set; }

    [JsonIgnore] public virtual Employee? Employee { get; set; }
    [JsonIgnore] public virtual AttendanceDevice? Device { get; set; }
}

public class AttendanceKioskSyncBatch : StoreBase
{
    public Guid DeviceId { get; set; }
    [MaxLength(40)] public string DeviceCode { get; set; } = string.Empty;
    [MaxLength(120)] public string? BatchClientId { get; set; }
    public int TotalCount { get; set; }
    public int AcceptedCount { get; set; }
    public int DuplicateCount { get; set; }
    public int FailedCount { get; set; }
    [MaxLength(40)] public string Status { get; set; } = "Received";
    public DateTime ReceivedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? CompletedAtUtc { get; set; }
    public string? ResultJson { get; set; }
}
