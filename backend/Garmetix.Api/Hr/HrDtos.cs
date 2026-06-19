using Garmetix.Core.Models.HRM;

namespace Garmetix.Api.Hr;

public sealed record GenerateMonthlyAttendanceRequest(
    int Year,
    int Month,
    Guid? CompanyId,
    Guid? StoreGroupId,
    Guid? StoreId);

public sealed record GenerateMonthlyAttendanceResponse(
    int Year,
    int Month,
    int EmployeesProcessed,
    int RecordsCreated,
    int RecordsUpdated);

public sealed record EmployeeMasterSummaryDto(
    int TotalEmployees,
    int ActiveEmployees,
    int MissingPhoto,
    int MissingAadhaar,
    int MissingPan,
    int MissingBank,
    int MissingEmergencyContact,
    int ExitedThisMonth,
    int WithSalaryStructure,
    int OpenAdvanceCount,
    decimal OpenAdvanceAmount,
    IReadOnlyList<string> ReadinessMessages);

public sealed record EmployeeLifecycleRequest(
    string Status,
    DateTime? ExitDate,
    string? ExitReason);

public sealed record EmployeeIdCardDto(
    Guid EmployeeId,
    string EmployeeCode,
    string FullName,
    string Department,
    string Designation,
    string StoreName,
    string CompanyName,
    string Mobile,
    string EmergencyContact,
    string BloodGroup,
    string PhotoDataUrl,
    DateTime JoiningDate);

public sealed record EmployeePayrollAdjustmentRequest(
    Guid Id,
    Guid EmployeeId,
    string AdjustmentType,
    DateTime OnDate,
    int? SalaryMonth,
    decimal Amount,
    decimal LeaveDays,
    bool RecoverFromSalary,
    decimal RecoveredAmount,
    decimal PfEmployee,
    decimal PfEmployer,
    decimal GratuityAmount,
    string Status,
    string? Remarks,
    Guid CompanyId,
    Guid StoreGroupId,
    Guid StoreId);

public sealed record EmployeePayrollAdjustmentRowDto(
    Guid Id,
    Guid EmployeeId,
    string EmployeeCode,
    string EmployeeName,
    string AdjustmentType,
    DateTime OnDate,
    int? SalaryMonth,
    decimal Amount,
    decimal LeaveDays,
    bool RecoverFromSalary,
    decimal RecoveredAmount,
    decimal PfEmployee,
    decimal PfEmployer,
    decimal GratuityAmount,
    string Status,
    string? Remarks,
    Guid CompanyId,
    Guid StoreGroupId,
    Guid StoreId);

public sealed record EmployeeBenefitSummaryDto(
    int OpenAdvances,
    decimal OpenAdvanceAmount,
    decimal RecoveredAmount,
    decimal LeaveDays,
    decimal BonusAmount,
    decimal PfEmployee,
    decimal PfEmployer,
    decimal GratuityAmount,
    IReadOnlyList<string> ReadinessMessages);
