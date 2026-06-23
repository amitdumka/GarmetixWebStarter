using Garmetix.Api.Auth;
using Garmetix.Api.Workspace;
using Garmetix.Core.Models.HRM;
using Garmetix.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Garmetix.Api.Hr;

public static class HrEndpoints
{
    private static readonly HashSet<string> AllowedEmployeeStatuses = new(StringComparer.OrdinalIgnoreCase)
    {
        "Active", "On Leave", "Resigned", "Terminated", "Inactive"
    };

    private static readonly HashSet<string> AllowedAdjustmentTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "SalaryAdvance", "AdvanceRecovery", "Leave", "Bonus", "LeaveEncashment", "PF", "Gratuity", "Other"
    };

    public static RouteGroupBuilder MapHrEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/hr")
            .WithTags("HR")
            .RequireAuthorization(GarmetixPolicies.Hr);

        group.MapPost("/monthly-attendance/generate", GenerateMonthlyAttendanceAsync);
        group.MapGet("/employee-master/summary", EmployeeMasterSummaryAsync);
        group.MapGet("/employees/{id:guid}/id-card", EmployeeIdCardAsync);
        group.MapPost("/employees/{id:guid}/lifecycle", UpdateLifecycleAsync).RequireAuthorization(GarmetixPolicies.Edit);

        var hrPayroll = app.MapGroup("/api/hr-payroll")
            .WithTags("HR Payroll")
            .RequireAuthorization();

        hrPayroll.MapGet("/adjustments", ListPayrollAdjustmentsAsync);
        hrPayroll.MapGet("/adjustments/summary", PayrollAdjustmentSummaryAsync);
        hrPayroll.MapPost("/adjustments", CreatePayrollAdjustmentAsync);
        hrPayroll.MapPut("/adjustments/{id:guid}", UpdatePayrollAdjustmentAsync).RequireAuthorization(GarmetixPolicies.Edit);
        hrPayroll.MapDelete("/adjustments/{id:guid}", DeletePayrollAdjustmentAsync).RequireAuthorization(GarmetixPolicies.Delete);

        return group;
    }

    private static async Task<IResult> GenerateMonthlyAttendanceAsync(
        GenerateMonthlyAttendanceRequest request,
        MonthlyAttendanceService service,
        CancellationToken cancellationToken)
    {
        try
        {
            return Results.Ok(await service.GenerateAsync(request, cancellationToken));
        }
        catch (ArgumentException ex)
        {
            return Results.BadRequest(new { message = ex.Message });
        }
    }

    private static async Task<IResult> EmployeeMasterSummaryAsync(GarmetixDbContext db, HttpContext context, CancellationToken cancellationToken)
    {
        var employees = await WorkspaceScope.ApplyTo(db.Employees.AsNoTracking(), context).ToListAsync(cancellationToken);
        var employeeIds = employees.Select(employee => employee.Id).ToList();
        var withSalaryStructure = await WorkspaceScope.ApplyTo(db.SalaryStructures.AsNoTracking(), context)
            .Where(item => employeeIds.Contains(item.EmployeeId) && item.ToDate == null)
            .Select(item => item.EmployeeId)
            .Distinct()
            .CountAsync(cancellationToken);
        var openAdvances = await WorkspaceScope.ApplyTo(db.EmployeePayrollAdjustments.AsNoTracking(), context)
            .Where(item => item.AdjustmentType == "SalaryAdvance" && item.Status != "Closed")
            .ToListAsync(cancellationToken);

        var now = DateTime.Today;
        var monthStart = new DateTime(now.Year, now.Month, 1);
        var messages = new List<string>();
        var missingPhoto = employees.Count(item => string.IsNullOrWhiteSpace(item.PhotoDataUrl));
        var missingAadhaar = employees.Count(item => string.IsNullOrWhiteSpace(item.Aadhar) || item.Aadhar.Length != 12);
        var missingPan = employees.Count(item => string.IsNullOrWhiteSpace(item.PAN));
        var missingBank = employees.Count(item => string.IsNullOrWhiteSpace(item.BankAccountNumber) || string.IsNullOrWhiteSpace(item.IFSC));
        var missingEmergency = employees.Count(item => string.IsNullOrWhiteSpace(item.EmergencyContact));

        if (missingPhoto > 0) messages.Add($"{missingPhoto} employee photo(s) missing for ID card and future face attendance.");
        if (missingAadhaar > 0) messages.Add($"{missingAadhaar} Aadhaar detail(s) missing or invalid.");
        if (missingBank > 0) messages.Add($"{missingBank} bank detail(s) missing for payroll payout.");
        if (withSalaryStructure < employees.Count(item => item.Working)) messages.Add("Some active employees do not have a current salary structure.");
        if (messages.Count == 0) messages.Add("Employee master is ready for attendance and payroll acceptance.");

        return Results.Ok(new EmployeeMasterSummaryDto(
            employees.Count,
            employees.Count(item => item.Working && item.EmployeeStatus != "Resigned" && item.EmployeeStatus != "Terminated"),
            missingPhoto,
            missingAadhaar,
            missingPan,
            missingBank,
            missingEmergency,
            employees.Count(item => item.LeavingDate >= monthStart),
            withSalaryStructure,
            openAdvances.Count,
            openAdvances.Sum(item => Math.Max(0, item.Amount - item.RecoveredAmount)),
            messages));
    }

    private static async Task<IResult> EmployeeIdCardAsync(Guid id, GarmetixDbContext db, HttpContext context, CancellationToken cancellationToken)
    {
        var employee = await WorkspaceScope.ApplyTo(db.Employees.AsNoTracking(), context)
            .FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (employee is null)
        {
            return Results.NotFound();
        }

        var store = await db.Stores.AsNoTracking().FirstOrDefaultAsync(item => item.Id == employee.StoreId, cancellationToken);
        var company = await db.Companies.AsNoTracking().FirstOrDefaultAsync(item => item.Id == employee.CompanyId, cancellationToken);
        return Results.Ok(new EmployeeIdCardDto(
            employee.Id,
            employee.EmployeeCode ?? $"EMP-{employee.EmpId:0000}",
            employee.StaffName,
            employee.Department ?? "-",
            employee.Designation ?? employee.Category.ToString(),
            store?.Name ?? "Store",
            company?.Name ?? "Company",
            MaskMobile(employee.Mobile),
            employee.EmergencyContact ?? "-",
            employee.BloodGroup ?? "-",
            employee.PhotoDataUrl ?? string.Empty,
            employee.JoiningDate));
    }

    private static async Task<IResult> UpdateLifecycleAsync(Guid id, EmployeeLifecycleRequest request, GarmetixDbContext db, HttpContext context, CancellationToken cancellationToken)
    {
        var employee = await WorkspaceScope.ApplyTo(db.Employees, context).FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (employee is null)
        {
            return Results.NotFound();
        }

        var status = string.IsNullOrWhiteSpace(request.Status) ? "Active" : request.Status.Trim();
        if (!AllowedEmployeeStatuses.Contains(status))
        {
            return Results.BadRequest(new { message = "Status must be Active, On Leave, Resigned, Terminated or Inactive." });
        }

        employee.EmployeeStatus = status;
        employee.ExitReason = string.IsNullOrWhiteSpace(request.ExitReason) ? null : request.ExitReason.Trim();
        employee.Working = status.Equals("Active", StringComparison.OrdinalIgnoreCase) || status.Equals("On Leave", StringComparison.OrdinalIgnoreCase);
        employee.LeavingDate = employee.Working ? null : request.ExitDate ?? DateTime.Today;
        employee.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
        return Results.Ok(employee);
    }

    private static async Task<IResult> ListPayrollAdjustmentsAsync(GarmetixDbContext db, HttpContext context, CancellationToken cancellationToken)
    {
        if (!CanUseHrPayroll(context)) return Results.Forbid();
        var employees = await WorkspaceScope.ApplyTo(db.Employees.AsNoTracking(), context).ToDictionaryAsync(item => item.Id, cancellationToken);
        var adjustments = await WorkspaceScope.ApplyTo(db.EmployeePayrollAdjustments.AsNoTracking(), context)
            .OrderByDescending(item => item.OnDate)
            .Take(500)
            .ToListAsync(cancellationToken);
        return Results.Ok(adjustments.Select(item => ToAdjustmentRow(item, employees.TryGetValue(item.EmployeeId, out var employee) ? employee : null)).ToList());
    }

    private static async Task<IResult> PayrollAdjustmentSummaryAsync(GarmetixDbContext db, HttpContext context, CancellationToken cancellationToken)
    {
        if (!CanUseHrPayroll(context)) return Results.Forbid();
        var rows = await WorkspaceScope.ApplyTo(db.EmployeePayrollAdjustments.AsNoTracking(), context).ToListAsync(cancellationToken);
        var openAdvances = rows.Where(item => item.AdjustmentType == "SalaryAdvance" && item.Status != "Closed").ToList();
        var messages = new List<string>();
        if (openAdvances.Count > 0) messages.Add($"{openAdvances.Count} open salary advance(s) must be reviewed before final payroll.");
        if (!rows.Any(item => item.AdjustmentType == "PF")) messages.Add("PF rows are optional, but no PF adjustment has been recorded yet.");
        if (!rows.Any(item => item.AdjustmentType == "Gratuity")) messages.Add("Gratuity rows are optional, but no gratuity provision/settlement has been recorded yet.");
        if (messages.Count == 0) messages.Add("HR benefits and payroll adjustment register is ready.");

        return Results.Ok(new EmployeeBenefitSummaryDto(
            openAdvances.Count,
            openAdvances.Sum(item => Math.Max(0, item.Amount - item.RecoveredAmount)),
            rows.Sum(item => item.RecoveredAmount),
            rows.Where(item => item.AdjustmentType == "Leave" || item.AdjustmentType == "LeaveEncashment").Sum(item => item.LeaveDays),
            rows.Where(item => item.AdjustmentType == "Bonus").Sum(item => item.Amount),
            rows.Sum(item => item.PfEmployee),
            rows.Sum(item => item.PfEmployer),
            rows.Sum(item => item.GratuityAmount),
            messages));
    }

    private static async Task<IResult> CreatePayrollAdjustmentAsync(EmployeePayrollAdjustmentRequest request, GarmetixDbContext db, HttpContext context, CancellationToken cancellationToken)
    {
        if (!CanUseHrPayroll(context)) return Results.Forbid();
        var entity = new EmployeePayrollAdjustment { Id = request.Id == Guid.Empty ? Guid.NewGuid() : request.Id };
        var validation = await ApplyAdjustmentRequestAsync(entity, request, db, context, cancellationToken);
        if (validation is not null) return Results.BadRequest(new { message = validation });
        db.EmployeePayrollAdjustments.Add(entity);
        await db.SaveChangesAsync(cancellationToken);
        return Results.Created($"/api/hr-payroll/adjustments/{entity.Id}", entity);
    }

    private static async Task<IResult> UpdatePayrollAdjustmentAsync(Guid id, EmployeePayrollAdjustmentRequest request, GarmetixDbContext db, HttpContext context, CancellationToken cancellationToken)
    {
        if (!CanUseHrPayroll(context)) return Results.Forbid();
        var entity = await WorkspaceScope.ApplyTo(db.EmployeePayrollAdjustments, context).FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (entity is null) return Results.NotFound();
        var validation = await ApplyAdjustmentRequestAsync(entity, request with { Id = id }, db, context, cancellationToken);
        if (validation is not null) return Results.BadRequest(new { message = validation });
        await db.SaveChangesAsync(cancellationToken);
        return Results.Ok(entity);
    }

    private static async Task<IResult> DeletePayrollAdjustmentAsync(Guid id, GarmetixDbContext db, HttpContext context, CancellationToken cancellationToken)
    {
        if (!CanUseHrPayroll(context)) return Results.Forbid();
        var entity = await WorkspaceScope.ApplyTo(db.EmployeePayrollAdjustments, context).FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (entity is null) return Results.NotFound();
        entity.Deleted = true;
        entity.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
        return Results.NoContent();
    }

    private static async Task<string?> ApplyAdjustmentRequestAsync(EmployeePayrollAdjustment entity, EmployeePayrollAdjustmentRequest request, GarmetixDbContext db, HttpContext context, CancellationToken cancellationToken)
    {
        var employee = await WorkspaceScope.ApplyTo(db.Employees.AsNoTracking(), context)
            .FirstOrDefaultAsync(item => item.Id == request.EmployeeId, cancellationToken);
        if (employee is null) return "Select a valid employee in the current workspace.";

        var type = string.IsNullOrWhiteSpace(request.AdjustmentType) ? "SalaryAdvance" : request.AdjustmentType.Trim();
        if (!AllowedAdjustmentTypes.Contains(type)) return "Adjustment type must be SalaryAdvance, AdvanceRecovery, Leave, Bonus, LeaveEncashment, PF, Gratuity or Other.";
        if (request.Amount < 0 || request.LeaveDays < 0 || request.RecoveredAmount < 0 || request.PfEmployee < 0 || request.PfEmployer < 0 || request.GratuityAmount < 0) return "Payroll adjustment values cannot be negative.";

        entity.EmployeeId = employee.Id;
        entity.AdjustmentType = type;
        entity.OnDate = request.OnDate == default ? DateTime.Today : request.OnDate.Date;
        entity.SalaryMonth = request.SalaryMonth;
        entity.Amount = Math.Round(request.Amount, 2);
        entity.LeaveDays = Math.Round(request.LeaveDays, 2);
        entity.RecoverFromSalary = request.RecoverFromSalary;
        entity.RecoveredAmount = Math.Round(request.RecoveredAmount, 2);
        entity.PfEmployee = Math.Round(request.PfEmployee, 2);
        entity.PfEmployer = Math.Round(request.PfEmployer, 2);
        entity.GratuityAmount = Math.Round(request.GratuityAmount, 2);
        entity.Status = string.IsNullOrWhiteSpace(request.Status) ? "Open" : request.Status.Trim();
        entity.Remarks = string.IsNullOrWhiteSpace(request.Remarks) ? null : request.Remarks.Trim();
        entity.CompanyId = employee.CompanyId;
        entity.StoreGroupId = employee.StoreGroupId;
        entity.StoreId = employee.StoreId;
        entity.UpdatedAt = DateTime.UtcNow;
        return null;
    }

    private static EmployeePayrollAdjustmentRowDto ToAdjustmentRow(EmployeePayrollAdjustment item, Employee? employee) => new(
        item.Id,
        item.EmployeeId,
        employee?.EmployeeCode ?? (employee is null ? string.Empty : $"EMP-{employee.EmpId:0000}"),
        employee?.StaffName ?? "Employee",
        item.AdjustmentType,
        item.OnDate,
        item.SalaryMonth,
        item.Amount,
        item.LeaveDays,
        item.RecoverFromSalary,
        item.RecoveredAmount,
        item.PfEmployee,
        item.PfEmployer,
        item.GratuityAmount,
        item.Status,
        item.Remarks,
        item.CompanyId,
        item.StoreGroupId,
        item.StoreId);

    private static bool CanUseHrPayroll(HttpContext context)
        => AccessPermissionMatrix.CanAccessPolicy(context.User, GarmetixPolicies.Hr)
            || AccessPermissionMatrix.CanAccessPolicy(context.User, GarmetixPolicies.Payroll);

    private static string MaskMobile(string? mobile)
    {
        if (string.IsNullOrWhiteSpace(mobile) || mobile.Length < 4) return "-";
        return new string('•', Math.Max(0, mobile.Length - 4)) + mobile[^4..];
    }
}
