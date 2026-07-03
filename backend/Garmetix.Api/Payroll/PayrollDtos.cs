using Garmetix.Core.Enums;

namespace Garmetix.Api.Payroll;

public sealed record GeneratePayslipsRequest(
    int Year,
    int Month,
    Guid? CompanyId,
    Guid? StoreGroupId,
    Guid? StoreId);

public sealed record GeneratePayslipsResponse(
    int Year,
    int Month,
    int EmployeesProcessed,
    int PayslipsCreated,
    int PayslipsUpdated,
    decimal TotalGross,
    decimal TotalDeductions,
    decimal TotalNet,
    decimal TotalAdvance,
    decimal TotalCarryForwardDue,
    decimal TotalPaid,
    decimal TotalDue);

public sealed record PayslipSummaryDto(
    Guid Id,
    Guid EmployeeId,
    string EmployeeName,
    string? EmployeeEmail,
    string EmployeeMobile,
    string MonthYear,
    DateTime PayPeriodStart,
    DateTime? PayPeriodEnd,
    decimal TotalEarnings,
    decimal TotalDeductions,
    decimal NetSalary,
    decimal SalaryAdvance,
    decimal CarryForwardDue,
    decimal PaidAmount,
    decimal DueAmount,
    decimal PayableAmount,
    decimal BillableDays,
    decimal WorkingDays,
    string Status);

public sealed record PayslipPrintDto(
    PayslipSummaryDto Summary,
    string CompanyName,
    string CompanyAddress,
    string CompanyEmail,
    string CompanyPhone,
    string? StoreName,
    string? StoreAddress,
    decimal BasicSalary,
    decimal Hra,
    decimal SpecialAllowance,
    decimal ConveyanceAllowance,
    decimal Incentives,
    decimal OtherEarnings,
    decimal ProvidentFund,
    decimal Gratuity,
    decimal ProfessionalTax,
    decimal IncomeTax,
    decimal Deductions,
    decimal OtherDeductions,
    string? Remarks);

public sealed record SalaryPaymentPreviewRequest(
    Guid EmployeeId,
    int SalaryMonth,
    Guid? SalaryPaySlipId,
    Guid? PaymentId);

public sealed record SalaryPaymentPreviewDto(
    Guid? SalaryPaySlipId,
    decimal GrossSalary,
    decimal BaseDeductions,
    decimal SalaryAdvance,
    decimal TotalDeductions,
    decimal PreviousDue,
    decimal NetPayable,
    decimal AlreadyPaid,
    decimal OutstandingAmount,
    decimal RoundedPaidAmount,
    decimal RoundOff);

public sealed record SalaryPaymentUpsertRequest(
    Guid EmployeeId,
    int SalaryMonth,
    DateTime OnDate,
    SalaryComponent SalaryComponent,
    decimal GrossSalary,
    decimal TotalDeductions,
    decimal NetSalary,
    decimal Amount,
    PaymentMode PaymentMode,
    string? Remarks,
    Guid? SalaryPaySlipId,
    Guid CompanyId,
    Guid StoreGroupId,
    Guid StoreId);

public sealed record PayrollFinalizeMonthRequest(
    int Year,
    int Month,
    Guid? EmployeeId,
    Guid? CompanyId,
    Guid? StoreGroupId,
    Guid? StoreId,
    bool Confirm,
    bool PostSalaryPayments,
    bool LockMonth,
    PaymentMode PaymentMode,
    DateTime? PaymentDate,
    decimal OvertimeRateMultiplier,
    decimal LatePenaltyPerDay,
    string? Notes);

public sealed record PayrollFinalizeMonthResponse(
    int Year,
    int Month,
    int EmployeesFinalized,
    int ReviewRowsApproved,
    int DraftRowsReady,
    int PayslipsCreated,
    int PayslipsUpdated,
    int SalaryPaymentsCreated,
    int LockedRows,
    decimal TotalGross,
    decimal TotalDeductions,
    decimal TotalNet,
    decimal TotalPaid,
    bool PaymentPostingEnabled,
    bool MonthLockEnabled,
    IReadOnlyList<string> StepLogs);

public sealed record PayrollRealMonthValidationDto(
    int Year,
    int Month,
    string MonthYear,
    bool Complete,
    string Status,
    string GeneratedAtUtc,
    PayrollRealMonthCountsDto Counts,
    PayrollRealMonthMoneyDto Money,
    IReadOnlyList<PayrollRealMonthCheckDto> Checks,
    IReadOnlyList<PayrollRealMonthIssueDto> Issues,
    IReadOnlyList<PayrollRealMonthEmployeeDto> Employees,
    IReadOnlyList<string> CloseoutChecklist,
    IReadOnlyList<string> KnownLimitations,
    IReadOnlyList<string> NextModuleCandidates);

public sealed record PayrollRealMonthCountsDto(
    int ActiveEmployees,
    int MonthlySummaryRows,
    int EmployeesWithoutSummary,
    int PayrollReviewRows,
    int ApprovedReviewRows,
    int SalaryDraftRows,
    int ReadySalaryDraftRows,
    int Payslips,
    int SalaryPayments,
    int LockedMonthlyRows,
    int PendingRegularizations,
    int PendingPhotoProofs,
    int UnpaidPayslips,
    int DuplicatePayslipEmployees);

public sealed record PayrollRealMonthMoneyDto(
    decimal TotalGross,
    decimal TotalDeductions,
    decimal TotalNet,
    decimal TotalPaid,
    decimal Outstanding);

public sealed record PayrollRealMonthCheckDto(
    string Key,
    string Title,
    bool Ok,
    string Status,
    string Detail);

public sealed record PayrollRealMonthIssueDto(
    string Severity,
    string Code,
    string Message,
    int Count,
    string Action);

public sealed record PayrollRealMonthEmployeeDto(
    Guid EmployeeId,
    string EmployeeName,
    string? EmployeeCode,
    decimal PresentDays,
    decimal AbsentDays,
    decimal HalfDays,
    decimal LateDays,
    decimal PayableDays,
    decimal DeductionDays,
    string ReviewStatus,
    string DraftStatus,
    string PayrollPostStatus,
    bool HasPayslip,
    bool HasSalaryPayment,
    bool Locked,
    decimal NetSalary,
    decimal PaidAmount,
    decimal OutstandingAmount,
    string Status,
    IReadOnlyList<string> Issues);

