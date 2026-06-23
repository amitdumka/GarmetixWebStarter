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
