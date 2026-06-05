using Garmetix.Api.Auth;

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
}
