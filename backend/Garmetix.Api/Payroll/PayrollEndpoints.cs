using Garmetix.Api.Accounting;
using Garmetix.Api.Auth;
using Garmetix.Api.Numbering;
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
        group.MapGet("/payslips/{id:guid}/pdf", DownloadPayslipPdfAsync);

        return group;
    }

    public static RouteGroupBuilder MapSalaryPaymentEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/salary-payments")
            .WithTags("SalaryPayment")
            .RequireAuthorization(GarmetixPolicies.Payroll);

        group.MapGet("/", ListSalaryPaymentsAsync);
        group.MapGet("/{id:guid}", GetSalaryPaymentAsync);
        group.MapGet("/{id:guid}/pdf", DownloadSalaryPaymentPdfAsync);
        group.MapPost("/preview", PreviewSalaryPaymentAsync);
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

    private static async Task<IResult> DownloadPayslipPdfAsync(
        Guid id,
        PayrollService service,
        CancellationToken cancellationToken)
    {
        var payslip = await service.GetPrintablePayslipAsync(id, cancellationToken);
        return payslip is null
            ? Results.NotFound()
            : Results.File(PayrollPdfDocument.BuildPayslip(payslip), "application/pdf", $"payslip-{payslip.Summary.MonthYear.Replace(' ', '-')}.pdf");
    }

    private static async Task<IResult> DownloadSalaryPaymentPdfAsync(
        Guid id,
        GarmetixDbContext db,
        CancellationToken cancellationToken)
    {
        var payment = await db.SalaryPayments.AsNoTracking()
            .Include(item => item.Employee)
            .FirstOrDefaultAsync(item => item.Id == id && !item.Deleted, cancellationToken);
        if (payment is null)
        {
            return Results.NotFound();
        }

        var company = await db.Companies.AsNoTracking().FirstOrDefaultAsync(item => item.Id == payment.CompanyId, cancellationToken);
        var store = await db.Stores.AsNoTracking().FirstOrDefaultAsync(item => item.Id == payment.StoreId, cancellationToken);
        var model = new SalaryPaymentPdfModel(
            payment.Id,
            company?.Name ?? "Garmetix",
            string.Join(", ", new[] { company?.Address, company?.City, company?.State, company?.ZipCode }.Where(value => !string.IsNullOrWhiteSpace(value))),
            store?.Name ?? "Store",
            payment.Employee?.StaffName ?? "Employee",
            payment.VoucherNumber,
            payment.SalaryMonth,
            payment.OnDate,
            payment.GrossSalary,
            payment.TotalDeductions,
            payment.NetSalary,
            payment.Amount,
            payment.PaymentMode.ToString(),
            payment.Remarks ?? string.Empty);
        return Results.File(PayrollPdfDocument.BuildSalaryPayment(model), "application/pdf", $"{SafePdfFileName(payment.VoucherNumber, "salary-payment")}.pdf");
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
        SalaryPaymentUpsertRequest request,
        GarmetixDbContext db,
        AccountingPostingService accounting,
        DocumentNumberService documentNumbers,
        PayrollService payroll,
        CancellationToken cancellationToken)
    {
        var validation = await ValidateSalaryPaymentAsync(request, null, db, cancellationToken);
        if (validation is not null)
        {
            return validation;
        }

        var outstandingValidation = await ValidateOutstandingSalaryAsync(request, null, payroll, cancellationToken);
        if (outstandingValidation is not null)
        {
            return outstandingValidation;
        }

        var strategy = db.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
            var payment = new SalaryPayment();
            ApplySalaryPayment(payment, request);
            payment.VoucherNumber = await documentNumbers.NextSalaryPaymentAsync(
                request.CompanyId,
                request.StoreGroupId,
                request.StoreId,
                request.OnDate,
                cancellationToken);
            payment.Deleted = false;
            db.SalaryPayments.Add(payment);
            await accounting.PostSalaryPaymentAsync(payment, cancellationToken);
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            return Results.Created($"/api/salary-payments/{payment.Id}", payment);
        });
    }

    private static async Task<IResult> UpdateSalaryPaymentAsync(
        Guid id,
        SalaryPaymentUpsertRequest request,
        GarmetixDbContext db,
        AccountingPostingService accounting,
        PayrollService payroll,
        CancellationToken cancellationToken)
    {
        var validation = await ValidateSalaryPaymentAsync(request, id, db, cancellationToken);
        if (validation is not null)
        {
            return validation;
        }

        var outstandingValidation = await ValidateOutstandingSalaryAsync(request, id, payroll, cancellationToken);
        if (outstandingValidation is not null)
        {
            return outstandingValidation;
        }

        var strategy = db.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
            var payment = await db.SalaryPayments.FirstOrDefaultAsync(item => item.Id == id && !item.Deleted, cancellationToken);
            if (payment is null)
            {
                return Results.NotFound();
            }

            ApplySalaryPayment(payment, request);
            await accounting.PostSalaryPaymentAsync(payment, cancellationToken);
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            return Results.Ok(payment);
        });
    }

    private static async Task<IResult> DeleteSalaryPaymentAsync(
        Guid id,
        GarmetixDbContext db,
        AccountingPostingService accounting,
        CancellationToken cancellationToken)
    {
        var strategy = db.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
            var payment = await db.SalaryPayments.FirstOrDefaultAsync(item => item.Id == id && !item.Deleted, cancellationToken);
            if (payment is null)
            {
                return Results.NotFound();
            }

            payment.Deleted = true;
            payment.UpdatedAt = DateTime.UtcNow;
            await accounting.RemoveSalaryPaymentPostingAsync(payment.Id, cancellationToken);
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            return Results.NoContent();
        });
    }

    private static async Task<IResult?> ValidateSalaryPaymentAsync(
        SalaryPaymentUpsertRequest request,
        Guid? paymentId,
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

        if (request.SalaryPaySlipId.HasValue &&
            !await db.SalaryPaySlips.AnyAsync(
                item => item.Id == request.SalaryPaySlipId.Value && item.EmployeeId == request.EmployeeId && !item.Deleted,
                cancellationToken))
        {
            return Results.BadRequest(new { message = "The selected payslip was not found for this employee." });
        }

        return null;
    }

    private static async Task<IResult?> ValidateOutstandingSalaryAsync(
        SalaryPaymentUpsertRequest request,
        Guid? paymentId,
        PayrollService payroll,
        CancellationToken cancellationToken)
    {
        if (request.SalaryComponent != Garmetix.Core.Enums.SalaryComponent.NetSalary &&
            !request.SalaryPaySlipId.HasValue)
        {
            return null;
        }

        try
        {
            var preview = await payroll.PreviewSalaryPaymentAsync(
                new SalaryPaymentPreviewRequest(
                    request.EmployeeId,
                    request.SalaryMonth,
                    request.SalaryPaySlipId,
                    paymentId),
                cancellationToken);
            var requestedAmount = RoundRupee(request.Amount);
            if (preview.RoundedPaidAmount <= 0)
            {
                return Results.Conflict(new { message = "This salary is already fully paid. No amount remains due." });
            }

            if (requestedAmount > preview.RoundedPaidAmount)
            {
                return Results.BadRequest(new
                {
                    message = $"Paid amount cannot exceed the outstanding salary of {preview.RoundedPaidAmount:0}."
                });
            }
        }
        catch (ArgumentException ex)
        {
            return Results.BadRequest(new { message = ex.Message });
        }

        return null;
    }

    private static void ApplySalaryPayment(SalaryPayment payment, SalaryPaymentUpsertRequest request)
    {
        payment.EmployeeId = request.EmployeeId;
        payment.SalaryMonth = request.SalaryMonth;
        payment.OnDate = request.OnDate.Date;
        payment.SalaryComponent = request.SalaryComponent;
        payment.GrossSalary = RoundMoney(request.GrossSalary);
        payment.TotalDeductions = RoundMoney(request.TotalDeductions);
        payment.NetSalary = RoundMoney(request.NetSalary);
        payment.Amount = RoundRupee(request.Amount);
        payment.PaymentMode = request.PaymentMode;
        payment.Remarks = request.Remarks?.Trim();
        payment.SalaryPaySlipId = request.SalaryPaySlipId;
        payment.CompanyId = request.CompanyId;
        payment.StoreGroupId = request.StoreGroupId;
        payment.StoreId = request.StoreId;
        payment.UpdatedAt = DateTime.UtcNow;
    }

    private static async Task<IResult> PreviewSalaryPaymentAsync(
        SalaryPaymentPreviewRequest request,
        PayrollService service,
        CancellationToken cancellationToken)
    {
        try
        {
            return Results.Ok(await service.PreviewSalaryPaymentAsync(request, cancellationToken));
        }
        catch (ArgumentException ex)
        {
            return Results.BadRequest(new { message = ex.Message });
        }
    }

    private static decimal RoundMoney(decimal value)
    {
        return Math.Round(value, 2, MidpointRounding.AwayFromZero);
    }

    private static decimal RoundRupee(decimal value)
    {
        return Math.Round(value, 0, MidpointRounding.AwayFromZero);
    }

    private static string SafePdfFileName(string? value, string fallback)
    {
        var source = string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
        var safe = new string(source
            .Select(character => char.IsLetterOrDigit(character) || character is '-' or '_' ? character : '-')
            .ToArray());

        safe = string.Join("-", safe.Split('-', StringSplitOptions.RemoveEmptyEntries));
        return string.IsNullOrWhiteSpace(safe) ? fallback : safe;
    }
}
