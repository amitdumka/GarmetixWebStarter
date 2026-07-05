using System.Security.Claims;
using Garmetix.Api.Auth;
using Microsoft.EntityFrameworkCore;

namespace Garmetix.Api.Licensing;

public static class LicenseEndpoints
{
    public static RouteGroupBuilder MapLicenseEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/license")
            .WithTags("License Activation")
            .RequireAuthorization(GarmetixPolicies.Admin);

        group.MapGet("/status", Status);
        group.MapGet("/subscriptions", ListSubscriptions);
        group.MapPost("/generate", Generate);
        group.MapPost("/activate", Activate);
        group.MapDelete("/activation", RemoveActivation);

        return group;
    }

    private static IResult Status(LicenseActivationService service)
        => Results.Ok(service.GetStatus());

    private static IResult Generate(LicenseGenerateRequest request, LicenseActivationService service)
    {
        try
        {
            return Results.Ok(service.Generate(request));
        }
        catch (InvalidOperationException ex)
        {
            return Results.BadRequest(new { message = ex.Message });
        }
    }

    private static async Task<IResult> Activate(
        LicenseActivateRequest request, 
        LicenseActivationService service, 
        Garmetix.Infrastructure.Data.GarmetixDbContext db,
        Garmetix.Infrastructure.Audit.AuditActorContext auditActor,
        ClaimsPrincipal user,
        CancellationToken cancellationToken)
    {
        try
        {
            if (auditActor.CompanyId == null)
            {
                return Results.BadRequest(new { message = "You must be logged into a tenant account to activate a license." });
            }

            var activatedBy = user.Identity?.Name
                ?? user.FindFirstValue(ClaimTypes.NameIdentifier)
                ?? user.FindFirstValue(ClaimTypes.Email)
                ?? "admin";
            
            // 1. Activate using the legacy service (validates token, writes to local file as fallback)
            var status = service.Activate(request, activatedBy);

            // 2. Save it to the SaaS TenantSubscriptions table
            if (status.Valid)
            {
                var existingSub = await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.FirstOrDefaultAsync(
                    db.TenantSubscriptions, 
                    s => s.CompanyId == auditActor.CompanyId.Value, 
                    cancellationToken);

                if (existingSub != null)
                {
                    existingSub.PlanName = status.Plan ?? "Basic";
                    existingSub.ValidTo = status.ExpiresAtUtc?.UtcDateTime ?? System.DateTime.UtcNow.AddDays(30);
                    existingSub.MaxUsers = status.MaxUsers ?? 10;
                    existingSub.MaxStores = status.MaxStores ?? 1;
                    existingSub.IsActive = true;
                }
                else
                {
                    var newSub = new Garmetix.Core.Models.SaaS.TenantSubscription
                    {
                        CompanyId = auditActor.CompanyId.Value,
                        PlanName = status.Plan ?? "Basic",
                        ValidFrom = status.IssuedAtUtc?.UtcDateTime ?? System.DateTime.UtcNow,
                        ValidTo = status.ExpiresAtUtc?.UtcDateTime ?? System.DateTime.UtcNow.AddDays(30),
                        MaxUsers = status.MaxUsers ?? 10,
                        MaxStores = status.MaxStores ?? 1,
                        IsActive = true
                    };
                    db.TenantSubscriptions.Add(newSub);
                }

                await db.SaveChangesAsync(cancellationToken);
            }

            return Results.Ok(status);
        }
        catch (InvalidOperationException ex)
        {
            return Results.BadRequest(new { message = ex.Message });
        }
    }

    private static IResult RemoveActivation(LicenseActivationService service)
        => Results.Ok(service.RemoveActivation());

    private static async Task<IResult> ListSubscriptions(
        HttpContext context, 
        Garmetix.Infrastructure.Data.GarmetixDbContext db, 
        CancellationToken cancellationToken)
    {
        var isSuperAdmin = bool.TryParse(context.User.FindFirstValue("superAdmin"), out var superAdmin) && superAdmin;
        if (!isSuperAdmin)
        {
            return Results.Forbid();
        }

        var subscriptions = await db.TenantSubscriptions
            .AsNoTracking()
            .Include(s => s.Company)
            .ToListAsync(cancellationToken);

        return Results.Ok(subscriptions.Select(s => new {
            s.Id,
            s.CompanyId,
            CompanyName = s.Company?.Name,
            s.PlanName,
            s.ValidFrom,
            s.ValidTo,
            s.IsActive,
            s.MaxStores,
            s.MaxUsers,
            s.IncludedModulesCsv
        }));
    }
}
