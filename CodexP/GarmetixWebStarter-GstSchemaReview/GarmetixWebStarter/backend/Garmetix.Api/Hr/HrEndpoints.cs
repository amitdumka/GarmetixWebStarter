using Garmetix.Api.Auth;

namespace Garmetix.Api.Hr;

public static class HrEndpoints
{
    public static RouteGroupBuilder MapHrEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/hr")
            .WithTags("HR")
            .RequireAuthorization(GarmetixPolicies.Hr);

        group.MapPost("/monthly-attendance/generate", GenerateMonthlyAttendanceAsync);

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
}
