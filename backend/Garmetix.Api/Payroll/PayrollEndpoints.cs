using Garmetix.Api.Accounting;
using Garmetix.Api.Auth;
using Garmetix.Core.Models.HRM;
using Garmetix.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Garmetix.Api.Payroll;

public static class PayrollEndpoints
{
    public static RouteGroupBuilder MapPayrollEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/payroll")
            .WithTags("Payroll")
            .RequireAuthorization(GarmetixPolicies.Payroll);

        group.MapPost("/payslips/generate-month", GeneratePayslipsAsync);
        group.MapGet("/payslips/recent", GetRecentPayslipsAsync);
        group.MapGet("/payslips/{id:guid}/print", GetPrintablePayslipAsync);

        return group;
    }

    public static RouteGroupBuilder MapSalaryPaymentEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/salary-payments")
            .WithTags("SalaryPayment")
            .RequireAuthorization(GarmetixPolicies.Payroll);

        group.MapGet("/", ListSalaryPaymentsAsync);
        group.MapGet("/{id:guid}", GetSalaryPaymentAsync);
        group.MapPost("/", SaveSalaryPaymentAsync);
        group.MapPut("/{id:guid}", UpdateSalaryPaymentAsync).RequireAuthorization(GarmetixPolicies.Edit);
        group.MapDelete("/{id:guid}", DeleteSalaryPaymentAsync).RequireAuthorization(GarmetixPolicies.Delete);

        return group;
    }

    private static async Task<IResult> GeneratePayslipsAsync(
        GeneratePayslipsRequest request,
        PayrollService service,
        CancellationToken cancellationToken)
    {
        try
        {
            return Results.Ok(await service.GeneratePayslipsAsync(request, cancellationToken));
        }
        catch (ArgumentException ex)
        {
            return Results.BadRequest(new { message = ex.Message });
        }
    }

    private static async Task<IResult> GetRecentPayslipsAsync(
        int? take,
        PayrollService service,
        CancellationToken cancellationToken)
    {
        return Results.Ok(await service.GetRecentPayslipsAsync(take, cancellationToken));
    }

    private static async Task<IResult> GetPrintablePayslipAsync(
        Guid id,
        PayrollService service,
        CancellationToken cancellationToken)
    {
        var payslip = await service.GetPrintablePayslipAsync(id, cancellationToken);
        return payslip is null ? Results.NotFound() : Results.Ok(payslip);
    }

    private static async Task<List<SalaryPayment>> ListSalaryPaymentsAsync(GarmetixDbContext db, CancellationToken cancellationToken)
    {
        return await db.SalaryPayments
            .AsNoTracking()
            .Where(item => !item.Deleted)
            .OrderByDescending(item => item.OnDate)
            .ThenByDescending(item => item.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    private static async Task<IResult> GetSalaryPaymentAsync(Guid id, GarmetixDbContext db, CancellationToken cancellationToken)
    {
        var payment = await db.SalaryPayments
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == id && !item.Deleted, cancellationToken);

        return payment is null ? Results.NotFound() : Results.Ok(payment);
    }

    private static async Task<IResult> SaveSalaryPaymentAsync(
        SalaryPayment request,
        GarmetixDbContext db,
        AccountingPostingService accounting,
        CancellationToken cancellationToken)
    {
        var validation = await ValidateSalaryPaymentAsync(request, db, cancellationToken);
        if (validation is not null)
        {
            return validation;
        }

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var payment = new SalaryPayment();
        ApplySalaryPayment(payment, request);
        payment.Deleted = false;
        db.SalaryPayments.Add(payment);
        await accounting.PostSalaryPaymentAsync(payment, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return Results.Created($"/api/salary-payments/{payment.Id}", payment);
    }

    private static async Task<IResult> UpdateSalaryPaymentAsync(
        Guid id,
        SalaryPayment request,
        GarmetixDbContext db,
        AccountingPostingService accounting,
        CancellationToken cancellationToken)
    {
        request.Id = id;
        var validation = await ValidateSalaryPaymentAsync(request, db, cancellationToken);
        if (validation is not null)
        {
            return validation;
        }

        var payment = await db.SalaryPayments.FirstOrDefaultAsync(item => item.Id == id && !item.Deleted, cancellationToken);
        if (payment is null)
        {
            return Results.NotFound();
        }

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        ApplySalaryPayment(payment, request);
        await accounting.PostSalaryPaymentAsync(payment, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return Results.Ok(payment);
    }

    private static async Task<IResult> DeleteSalaryPaymentAsync(
        Guid id,
        GarmetixDbContext db,
        AccountingPostingService accounting,
        CancellationToken cancellationToken)
    {
        var payment = await db.SalaryPayments.FirstOrDefaultAsync(item => item.Id == id && !item.Deleted, cancellationToken);
        if (payment is null)
        {
            return Results.NotFound();
        }

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        payment.Deleted = true;
        payment.UpdatedAt = DateTime.UtcNow;
        await accounting.RemoveSalaryPaymentPostingAsync(payment.Id, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return Results.NoContent();
    }

    private static async Task<IResult?> ValidateSalaryPaymentAsync(
        SalaryPayment request,
        GarmetixDbContext db,
        CancellationToken cancellationToken)
    {
        if (request.EmployeeId == Guid.Empty)
        {
            return Results.BadRequest(new { message = "Employee is required." });
        }

        if (request.CompanyId == Guid.Empty || request.StoreGroupId == Guid.Empty || request.StoreId == Guid.Empty)
        {
            return Results.BadRequest(new { message = "Company, store group, and store are required." });
        }

        if (string.IsNullOrWhiteSpace(request.VoucherNumber))
        {
            return Results.BadRequest(new { message = "Voucher number is required." });
        }

        if (request.SalaryMonth < 200001 || request.SalaryMonth % 100 is < 1 or > 12)
        {
            return Results.BadRequest(new { message = "Salary month must be in yyyyMM format." });
        }

        if (request.Amount <= 0)
        {
            return Results.BadRequest(new { message = "Payment amount must be greater than zero." });
        }

        if (!await db.Employees.AnyAsync(item => item.Id == request.EmployeeId, cancellationToken))
        {
            return Results.BadRequest(new { message = "Select a valid employee." });
        }

        var duplicate = await db.SalaryPayments.AnyAsync(
            item => item.Id != request.Id &&
                !item.Deleted &&
                item.CompanyId == request.CompanyId &&
                item.EmployeeId == request.EmployeeId &&
                item.VoucherNumber == request.VoucherNumber,
            cancellationToken);

        if (duplicate)
        {
            return Results.Conflict(new { message = "A salary payment with this voucher number already exists for the employee." });
        }

        return null;
    }

    private static void ApplySalaryPayment(SalaryPayment payment, SalaryPayment request)
    {
        payment.EmployeeId = request.EmployeeId;
        payment.VoucherNumber = request.VoucherNumber.Trim();
        payment.SalaryMonth = request.SalaryMonth;
        payment.OnDate = request.OnDate;
        payment.SalaryComponent = request.SalaryComponent;
        payment.GrossSalary = request.GrossSalary;
        payment.TotalDeductions = request.TotalDeductions;
        payment.NetSalary = request.NetSalary;
        payment.Amount = request.Amount;
        payment.PaymentMode = request.PaymentMode;
        payment.Remarks = request.Remarks?.Trim();
        payment.SalaryPaySlipId = request.SalaryPaySlipId;
        payment.CompanyId = request.CompanyId;
        payment.StoreGroupId = request.StoreGroupId;
        payment.StoreId = request.StoreId;
        payment.UpdatedAt = DateTime.UtcNow;
    }
}
