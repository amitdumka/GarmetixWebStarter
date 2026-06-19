using Garmetix.Core.Enums;
using Garmetix.Core.Models.HRM;
using Garmetix.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Garmetix.Api.Payroll;

public sealed class PayrollService(GarmetixDbContext db)
{
    private static readonly SalaryComponent[] AdvanceComponents =
    [
        SalaryComponent.Advance,
        SalaryComponent.SalaryAdvance
    ];

    public async Task<GeneratePayslipsResponse> GeneratePayslipsAsync(
        GeneratePayslipsRequest request,
        CancellationToken cancellationToken)
    {
        if (request.Year < 2000 || request.Month is < 1 or > 12)
        {
            throw new ArgumentException("Valid year and month are required.");
        }

        var monthStart = new DateTime(request.Year, request.Month, 1);
        var nextMonth = monthStart.AddMonths(1);
        var monthEnd = nextMonth.AddDays(-1);
        var salaryMonth = ToSalaryMonth(monthStart);
        var monthYear = monthStart.ToString("MMMM yyyy");

        var employeesQuery = db.Employees.AsQueryable()
            .Where(employee =>
                employee.Working &&
                employee.JoiningDate <= monthEnd &&
                (employee.LeavingDate == null || employee.LeavingDate >= monthStart));

        if (request.CompanyId.HasValue)
        {
            employeesQuery = employeesQuery.Where(employee => employee.CompanyId == request.CompanyId.Value);
        }

        if (request.StoreGroupId.HasValue)
        {
            employeesQuery = employeesQuery.Where(employee => employee.StoreGroupId == request.StoreGroupId.Value);
        }

        if (request.StoreId.HasValue)
        {
            employeesQuery = employeesQuery.Where(employee => employee.StoreId == request.StoreId.Value);
        }

        var employees = await employeesQuery
            .OrderBy(employee => employee.FirstName)
            .ThenBy(employee => employee.LastName)
            .ToListAsync(cancellationToken);

        var employeeIds = employees.Select(employee => employee.Id).ToList();
        var structures = await db.SalaryStructures
            .Where(structure =>
                employeeIds.Contains(structure.EmployeeId) &&
                structure.FromDate <= monthEnd &&
                (structure.ToDate == null || structure.ToDate >= monthStart))
            .OrderByDescending(structure => structure.FromDate)
            .ToListAsync(cancellationToken);

        var attendanceRows = await db.MonthlyAttendance
            .Where(attendance => employeeIds.Contains(attendance.EmployeeId) && attendance.OnDate == monthStart)
            .ToDictionaryAsync(attendance => attendance.EmployeeId, cancellationToken);

        var existingPayslips = await db.SalaryPaySlips
            .Where(payslip => employeeIds.Contains(payslip.EmployeeId) && payslip.PayPeriodStart == monthStart)
            .ToDictionaryAsync(payslip => payslip.EmployeeId, cancellationToken);

        var benefitAdjustments = await db.EmployeePayrollAdjustments.AsNoTracking()
            .Where(item =>
                employeeIds.Contains(item.EmployeeId) &&
                !item.Deleted &&
                ((item.SalaryMonth.HasValue && item.SalaryMonth.Value == salaryMonth) || (item.OnDate >= monthStart && item.OnDate < nextMonth)))
            .ToListAsync(cancellationToken);

        var recordsCreated = 0;
        var recordsUpdated = 0;
        decimal totalGross = 0;
        decimal totalDeductions = 0;
        decimal totalNet = 0;
        decimal totalAdvance = 0;
        decimal totalCarryForwardDue = 0;
        decimal totalPaid = 0;
        decimal totalDue = 0;

        foreach (var employee in employees)
        {
            var structure = structures.FirstOrDefault(item => item.EmployeeId == employee.Id);
            if (structure is null)
            {
                continue;
            }

            attendanceRows.TryGetValue(employee.Id, out var attendance);
            var factor = AttendanceFactor(attendance, monthStart);

            if (!existingPayslips.TryGetValue(employee.Id, out var payslip))
            {
                payslip = new SalaryPaySlip
                {
                    EmployeeId = employee.Id,
                    PayPeriodStart = monthStart,
                    CompanyId = employee.CompanyId
                };
                db.SalaryPaySlips.Add(payslip);
                recordsCreated++;
            }
            else
            {
                recordsUpdated++;
            }

            payslip.CompanyId = employee.CompanyId;
            payslip.MonthYear = monthYear;
            payslip.PayPeriodStart = monthStart;
            payslip.PayPeriodEnd = monthEnd;
            payslip.BasicSalary = RoundMoney(structure.BasicSalary * factor);
            payslip.HRA = RoundMoney(structure.HRA * factor);
            payslip.SpecialAllowance = RoundMoney(structure.SpecialAllowance * factor);
            payslip.ConveyanceAllowance = RoundMoney(structure.ConveyanceAllowance * factor);
            payslip.Incentives = RoundMoney(structure.Incentives * factor);
            var employeeAdjustments = benefitAdjustments.Where(item => item.EmployeeId == employee.Id).ToList();
            var bonusAndEncashment = employeeAdjustments
                .Where(item => item.AdjustmentType == "Bonus" || item.AdjustmentType == "LeaveEncashment")
                .Sum(item => item.Amount);
            var pfEmployee = employeeAdjustments.Sum(item => item.PfEmployee);
            var gratuityProvision = employeeAdjustments.Sum(item => item.GratuityAmount);

            payslip.OtherEarnings = RoundMoney(bonusAndEncashment);
            payslip.ProvidentFund = RoundMoney((structure.ProvidentFund * factor) + pfEmployee);
            payslip.Gratuity = RoundMoney((structure.Gratuity * factor) + gratuityProvision);
            payslip.ProfessionalTax = RoundMoney(structure.ProfessionalTax * factor);
            payslip.Deductions = RoundMoney(structure.Deductions * factor);
            payslip.IncomeTax = 0;
            payslip.OtherDeductions = 0;
            payslip.Remarks = $"Generated for {monthYear}. Billable days {BillableDays(attendance, monthStart):0.##}. Salary advance, leave, bonus, PF and gratuity adjustments are calculated from HR Benefits register.";
        }

        await db.SaveChangesAsync(cancellationToken);

        foreach (var payslip in await db.SalaryPaySlips
            .Where(item => employeeIds.Contains(item.EmployeeId) && item.PayPeriodStart == monthStart)
            .ToListAsync(cancellationToken))
        {
            var amounts = await CalculateAmountsAsync(payslip, salaryMonth, monthStart, cancellationToken);
            totalGross += payslip.TotalEarnings;
            totalDeductions += payslip.TotalDeductions;
            totalNet += payslip.NetSalary;
            totalAdvance += amounts.SalaryAdvance;
            totalCarryForwardDue += amounts.CarryForwardDue;
            totalPaid += amounts.PaidAmount;
            totalDue += amounts.DueAmount;
        }

        return new GeneratePayslipsResponse(
            request.Year,
            request.Month,
            employees.Count,
            recordsCreated,
            recordsUpdated,
            RoundMoney(totalGross),
            RoundMoney(totalDeductions),
            RoundMoney(totalNet),
            RoundMoney(totalAdvance),
            RoundMoney(totalCarryForwardDue),
            RoundMoney(totalPaid),
            RoundMoney(totalDue));
    }

    public async Task<List<PayslipSummaryDto>> GetRecentPayslipsAsync(
        int? take,
        CancellationToken cancellationToken)
    {
        var limit = Math.Clamp(take ?? 100, 1, 500);
        var payslips = await db.SalaryPaySlips.AsNoTracking()
            .OrderByDescending(payslip => payslip.PayPeriodStart)
            .ThenByDescending(payslip => payslip.CreatedAt)
            .Take(limit)
            .ToListAsync(cancellationToken);

        return await ToSummariesAsync(payslips, cancellationToken);
    }

    public async Task<PayslipPrintDto?> GetPrintablePayslipAsync(
        Guid id,
        CancellationToken cancellationToken)
    {
        var payslip = await db.SalaryPaySlips.AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == id, cancellationToken);

        if (payslip is null)
        {
            return null;
        }

        var summary = (await ToSummariesAsync([payslip], cancellationToken)).Single();
        var company = await db.Companies.AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == payslip.CompanyId, cancellationToken);
        var employee = await db.Employees.AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == payslip.EmployeeId, cancellationToken);
        var store = employee is null
            ? null
            : await db.Stores.AsNoTracking().FirstOrDefaultAsync(item => item.Id == employee.StoreId, cancellationToken);

        return new PayslipPrintDto(
            summary,
            company?.Name ?? "Garmetix",
            company is null ? string.Empty : $"{company.Address}, {company.City}, {company.State} {company.ZipCode}".Trim(' ', ','),
            company?.Email ?? string.Empty,
            company?.ContactNumber ?? string.Empty,
            store?.Name,
            store is null ? null : $"{store.Address}, {store.City}, {store.State} {store.ZipCode}".Trim(' ', ','),
            payslip.BasicSalary,
            payslip.HRA,
            payslip.SpecialAllowance,
            payslip.ConveyanceAllowance,
            payslip.Incentives,
            payslip.OtherEarnings,
            payslip.ProvidentFund,
            payslip.Gratuity,
            payslip.ProfessionalTax,
            payslip.IncomeTax,
            payslip.Deductions,
            payslip.OtherDeductions,
            payslip.Remarks);
    }

    public async Task<SalaryPaymentPreviewDto> PreviewSalaryPaymentAsync(
        SalaryPaymentPreviewRequest request,
        CancellationToken cancellationToken)
    {
        if (request.EmployeeId == Guid.Empty)
        {
            throw new ArgumentException("Employee is required.");
        }

        var monthStart = FromSalaryMonth(request.SalaryMonth);
        var monthEnd = monthStart.AddMonths(1).AddDays(-1);
        var employeeExists = await db.Employees.AsNoTracking()
            .AnyAsync(employee => employee.Id == request.EmployeeId, cancellationToken);
        if (!employeeExists)
        {
            throw new ArgumentException("Select a valid employee.");
        }

        SalaryPaySlip? payslip;
        if (request.SalaryPaySlipId.HasValue)
        {
            payslip = await db.SalaryPaySlips.AsNoTracking()
                .FirstOrDefaultAsync(
                    item => item.Id == request.SalaryPaySlipId.Value &&
                        item.EmployeeId == request.EmployeeId &&
                        !item.Deleted,
                    cancellationToken);
            if (payslip is null)
            {
                throw new ArgumentException("The selected payslip was not found for this employee.");
            }
        }
        else
        {
            payslip = await db.SalaryPaySlips.AsNoTracking()
                .Where(item =>
                    item.EmployeeId == request.EmployeeId &&
                    item.PayPeriodStart == monthStart &&
                    !item.Deleted)
                .OrderByDescending(item => item.CreatedAt)
                .FirstOrDefaultAsync(cancellationToken);
        }

        decimal grossSalary;
        decimal baseDeductions;
        if (payslip is not null)
        {
            grossSalary = payslip.TotalEarnings;
            baseDeductions = payslip.TotalDeductions;
        }
        else
        {
            var structure = await db.SalaryStructures.AsNoTracking()
                .Where(item =>
                    item.EmployeeId == request.EmployeeId &&
                    item.FromDate <= monthEnd &&
                    (item.ToDate == null || item.ToDate >= monthStart) &&
                    !item.Deleted)
                .OrderByDescending(item => item.FromDate)
                .FirstOrDefaultAsync(cancellationToken);
            if (structure is null)
            {
                throw new ArgumentException("No payslip or salary structure is available for this employee and month.");
            }

            grossSalary = structure.BasicSalary +
                structure.HRA +
                structure.SpecialAllowance +
                structure.ConveyanceAllowance +
                structure.Incentives;
            baseDeductions = structure.ProvidentFund +
                structure.Gratuity +
                structure.ProfessionalTax +
                structure.Deductions;
        }

        var currentPayments = await db.SalaryPayments.AsNoTracking()
            .Where(payment =>
                payment.EmployeeId == request.EmployeeId &&
                payment.SalaryMonth == request.SalaryMonth &&
                !payment.Deleted &&
                (!request.PaymentId.HasValue || payment.Id != request.PaymentId.Value))
            .ToListAsync(cancellationToken);
        var benefitAdvance = await CalculateBenefitAdvanceAsync(request.EmployeeId, request.SalaryMonth, monthStart, cancellationToken);
        var salaryAdvance = currentPayments
            .Where(payment => AdvanceComponents.Contains(payment.SalaryComponent))
            .Sum(payment => payment.Amount) + benefitAdvance;
        var alreadyPaid = currentPayments
            .Where(payment =>
                payment.SalaryComponent == SalaryComponent.NetSalary ||
                (payslip != null && payment.SalaryPaySlipId == payslip.Id))
            .Sum(payment => payment.Amount);
        var previousDue = await CalculateCarryForwardDueAsync(
            request.EmployeeId,
            request.SalaryMonth,
            monthStart,
            cancellationToken);
        var totalDeductions = baseDeductions + salaryAdvance;
        var netPayable = Math.Max(0, grossSalary - totalDeductions + previousDue);
        var roundedPayable = RoundRupee(netPayable);
        var outstanding = Math.Max(0, roundedPayable - alreadyPaid);
        var roundedAmount = RoundRupee(outstanding);

        return new SalaryPaymentPreviewDto(
            payslip?.Id,
            RoundMoney(grossSalary),
            RoundMoney(baseDeductions),
            RoundMoney(salaryAdvance),
            RoundMoney(totalDeductions),
            RoundMoney(previousDue),
            RoundMoney(netPayable),
            RoundMoney(alreadyPaid),
            RoundMoney(outstanding),
            roundedAmount,
            RoundMoney(roundedPayable - netPayable));
    }

    private async Task<List<PayslipSummaryDto>> ToSummariesAsync(
        IReadOnlyList<SalaryPaySlip> payslips,
        CancellationToken cancellationToken)
    {
        if (payslips.Count == 0)
        {
            return [];
        }

        var employeeIds = payslips.Select(payslip => payslip.EmployeeId).Distinct().ToList();
        var employees = await db.Employees.AsNoTracking()
            .Where(employee => employeeIds.Contains(employee.Id))
            .ToDictionaryAsync(employee => employee.Id, cancellationToken);

        var monthStarts = payslips.Select(payslip => payslip.PayPeriodStart).Distinct().ToList();
        var attendanceRows = await db.MonthlyAttendance.AsNoTracking()
            .Where(attendance => employeeIds.Contains(attendance.EmployeeId) && monthStarts.Contains(attendance.OnDate))
            .ToListAsync(cancellationToken);

        var rows = new List<PayslipSummaryDto>();
        foreach (var payslip in payslips)
        {
            employees.TryGetValue(payslip.EmployeeId, out var employee);
            var salaryMonth = ToSalaryMonth(payslip.PayPeriodStart);
            var attendance = attendanceRows.FirstOrDefault(item => item.EmployeeId == payslip.EmployeeId && item.OnDate == payslip.PayPeriodStart);
            var amounts = await CalculateAmountsAsync(payslip, salaryMonth, payslip.PayPeriodStart, cancellationToken);
            var status = amounts.DueAmount <= 0 ? "Paid" : amounts.PaidAmount > 0 ? "Partial" : "Due";

            rows.Add(new PayslipSummaryDto(
                payslip.Id,
                payslip.EmployeeId,
                employee?.StaffName ?? "Employee",
                employee?.Email,
                employee?.Mobile ?? string.Empty,
                payslip.MonthYear,
                payslip.PayPeriodStart,
                payslip.PayPeriodEnd,
                RoundMoney(payslip.TotalEarnings),
                RoundMoney(payslip.TotalDeductions),
                RoundMoney(payslip.NetSalary),
                amounts.SalaryAdvance,
                amounts.CarryForwardDue,
                amounts.PaidAmount,
                amounts.DueAmount,
                amounts.PayableAmount,
                BillableDays(attendance, payslip.PayPeriodStart),
                WorkingDays(attendance, payslip.PayPeriodStart),
                status));
        }

        return rows;
    }

    private async Task<PayslipAmounts> CalculateAmountsAsync(
        SalaryPaySlip payslip,
        int salaryMonth,
        DateTime monthStart,
        CancellationToken cancellationToken)
    {
        var payments = await db.SalaryPayments.AsNoTracking()
            .Where(payment =>
                payment.EmployeeId == payslip.EmployeeId &&
                payment.SalaryMonth == salaryMonth &&
                !payment.Deleted)
            .ToListAsync(cancellationToken);

        var benefitAdvance = await CalculateBenefitAdvanceAsync(payslip.EmployeeId, salaryMonth, monthStart, cancellationToken);
        var advance = payments
            .Where(payment => AdvanceComponents.Contains(payment.SalaryComponent))
            .Sum(payment => payment.Amount) + benefitAdvance;
        var paid = payments
            .Where(payment =>
                payment.SalaryComponent == SalaryComponent.NetSalary ||
                payment.SalaryPaySlipId == payslip.Id)
            .Sum(payment => payment.Amount);
        var carryForwardDue = await CalculateCarryForwardDueAsync(payslip.EmployeeId, salaryMonth, monthStart, cancellationToken);
        var payable = RoundRupee(Math.Max(0, payslip.NetSalary + carryForwardDue - advance));
        var due = Math.Max(0, payable - paid);

        return new PayslipAmounts(
            RoundMoney(advance),
            RoundMoney(carryForwardDue),
            RoundMoney(paid),
            RoundMoney(payable),
            RoundMoney(due));
    }

    private async Task<decimal> CalculateBenefitAdvanceAsync(Guid employeeId, int salaryMonth, DateTime monthStart, CancellationToken cancellationToken)
    {
        var nextMonth = monthStart.AddMonths(1);
        var adjustments = await db.EmployeePayrollAdjustments.AsNoTracking()
            .Where(item =>
                item.EmployeeId == employeeId &&
                !item.Deleted &&
                item.RecoverFromSalary &&
                item.Status != "Closed" &&
                ((item.SalaryMonth.HasValue && item.SalaryMonth.Value == salaryMonth) || (item.OnDate >= monthStart && item.OnDate < nextMonth)) &&
                (item.AdjustmentType == "SalaryAdvance" || item.AdjustmentType == "AdvanceRecovery"))
            .ToListAsync(cancellationToken);

        return RoundMoney(adjustments.Sum(item => Math.Max(0, item.Amount - item.RecoveredAmount)));
    }

    private async Task<decimal> CalculateCarryForwardDueAsync(
        Guid employeeId,
        int currentSalaryMonth,
        DateTime monthStart,
        CancellationToken cancellationToken)
    {
        var previousPayslips = await db.SalaryPaySlips.AsNoTracking()
            .Where(payslip => payslip.EmployeeId == employeeId && payslip.PayPeriodStart < monthStart)
            .ToListAsync(cancellationToken);
        var previousPayments = await db.SalaryPayments.AsNoTracking()
            .Where(payment =>
                payment.EmployeeId == employeeId &&
                payment.SalaryMonth < currentSalaryMonth &&
                !payment.Deleted)
            .ToListAsync(cancellationToken);

        var earned = previousPayslips.Sum(payslip => RoundRupee(payslip.NetSalary));
        var previousAdjustments = await db.EmployeePayrollAdjustments.AsNoTracking()
            .Where(item =>
                item.EmployeeId == employeeId &&
                item.SalaryMonth.HasValue && item.SalaryMonth.Value < currentSalaryMonth &&
                !item.Deleted &&
                item.RecoverFromSalary &&
                item.Status != "Closed" &&
                (item.AdjustmentType == "SalaryAdvance" || item.AdjustmentType == "AdvanceRecovery"))
            .ToListAsync(cancellationToken);
        var advance = previousPayments
            .Where(payment => AdvanceComponents.Contains(payment.SalaryComponent))
            .Sum(payment => payment.Amount) + previousAdjustments.Sum(item => Math.Max(0, item.Amount - item.RecoveredAmount));
        var paid = previousPayments
            .Where(payment => payment.SalaryComponent == SalaryComponent.NetSalary || payment.SalaryPaySlipId != null)
            .Sum(payment => payment.Amount);

        return RoundMoney(Math.Max(0, earned - advance - paid));
    }

    private static decimal AttendanceFactor(MonthlyAttendance? attendance, DateTime monthStart)
    {
        var workingDays = WorkingDays(attendance, monthStart);
        if (workingDays <= 0)
        {
            return 1;
        }

        return Math.Clamp(BillableDays(attendance, monthStart) / workingDays, 0, 1);
    }

    private static decimal BillableDays(MonthlyAttendance? attendance, DateTime monthStart)
    {
        return attendance?.BillableDays ?? WorkingDays(attendance, monthStart);
    }

    private static decimal WorkingDays(MonthlyAttendance? attendance, DateTime monthStart)
    {
        return attendance?.NoOfWorkingDays > 0
            ? attendance.NoOfWorkingDays
            : DateTime.DaysInMonth(monthStart.Year, monthStart.Month);
    }

    private static int ToSalaryMonth(DateTime monthStart)
    {
        return (monthStart.Year * 100) + monthStart.Month;
    }

    private static DateTime FromSalaryMonth(int salaryMonth)
    {
        var year = salaryMonth / 100;
        var month = salaryMonth % 100;
        if (year < 2000 || month is < 1 or > 12)
        {
            throw new ArgumentException("Salary month must be in yyyyMM format.");
        }

        return new DateTime(year, month, 1);
    }

    private static decimal RoundMoney(decimal value)
    {
        return Math.Round(value, 2, MidpointRounding.AwayFromZero);
    }

    private static decimal RoundRupee(decimal value)
    {
        return Math.Round(value, 0, MidpointRounding.AwayFromZero);
    }

    private sealed record PayslipAmounts(
        decimal SalaryAdvance,
        decimal CarryForwardDue,
        decimal PaidAmount,
        decimal PayableAmount,
        decimal DueAmount);
}
