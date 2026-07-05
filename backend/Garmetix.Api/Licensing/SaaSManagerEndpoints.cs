using Garmetix.Core.Models.SaaS;
using Garmetix.Infrastructure.Data;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;

namespace Garmetix.Api.Licensing;

public static class SaaSManagerEndpoints
{
    public static void MapSaaSManagerEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/saas").RequireAuthorization("SuperAdmin");

        // --- Clients ---
        group.MapGet("/clients", async (GarmetixDbContext db, CancellationToken ct) =>
        {
            var clients = await db.SaaSClients.Where(c => !c.Deleted).ToListAsync(ct);
            return Results.Ok(clients);
        });

        group.MapPost("/clients", async (GarmetixDbContext db, [FromBody] SaaSClient client, CancellationToken ct) =>
        {
            db.SaaSClients.Add(client);
            await db.SaveChangesAsync(ct);
            return Results.Created($"/api/saas/clients/{client.Id}", client);
        });

        // --- Plans ---
        group.MapGet("/plans", async (GarmetixDbContext db, CancellationToken ct) =>
        {
            var plans = await db.SaaSPlans.Where(p => !p.Deleted).ToListAsync(ct);
            return Results.Ok(plans);
        });

        group.MapPost("/plans", async (GarmetixDbContext db, [FromBody] SaaSPlan plan, CancellationToken ct) =>
        {
            db.SaaSPlans.Add(plan);
            await db.SaveChangesAsync(ct);
            return Results.Created($"/api/saas/plans/{plan.Id}", plan);
        });

        // --- Tokens ---
        group.MapGet("/tokens", async (GarmetixDbContext db, CancellationToken ct) =>
        {
            var tokens = await db.SaaSTokens
                .Include(t => t.SaaSClient)
                .Include(t => t.SaaSPlan)
                .Where(t => !t.Deleted)
                .OrderByDescending(t => t.CreatedAtUtc)
                .ToListAsync(ct);
            return Results.Ok(tokens);
        });

        group.MapPost("/tokens/generate", async (GarmetixDbContext db, [FromBody] GenerateTokenRequest req, CancellationToken ct) =>
        {
            var client = await db.SaaSClients.FindAsync([req.ClientId], ct);
            var plan = await db.SaaSPlans.FindAsync([req.PlanId], ct);
            
            if (client == null || plan == null) return Results.BadRequest("Invalid Client or Plan.");

            var tokenString = GenerateSecureToken();
            var token = new SaaSToken
            {
                SaaSClientId = client.Id,
                SaaSPlanId = plan.Id,
                TokenString = tokenString,
                ExpiresAt = DateTime.UtcNow.AddDays(req.ValidityDays),
                IsActivated = false
            };

            db.SaaSTokens.Add(token);
            await db.SaveChangesAsync(ct);

            return Results.Ok(new { token.Id, token.TokenString, token.ExpiresAt });
        });

        group.MapPost("/tokens/activate", async (GarmetixDbContext db, [FromBody] ActivateTokenRequest req, CancellationToken ct) =>
        {
            var token = await db.SaaSTokens
                .Include(t => t.SaaSPlan)
                .FirstOrDefaultAsync(t => t.TokenString == req.TokenString && !t.Deleted, ct);

            if (token == null) return Results.BadRequest("Invalid token.");
            if (token.IsActivated) return Results.BadRequest("Token is already activated.");
            if (token.ExpiresAt < DateTime.UtcNow) return Results.BadRequest("Token has expired.");

            var company = await db.Companies.FindAsync([req.CompanyId], ct);
            if (company == null) return Results.BadRequest("Company not found.");

            // Activate Token
            token.IsActivated = true;
            token.ActivatedAt = DateTime.UtcNow;
            
            // Link company to client
            company.SaaSClientId = token.SaaSClientId;

            // Create or update Tenant Subscription
            var subscription = await db.TenantSubscriptions.FirstOrDefaultAsync(s => s.CompanyId == company.Id, ct);
            if (subscription == null)
            {
                subscription = new TenantSubscription { CompanyId = company.Id };
                db.TenantSubscriptions.Add(subscription);
            }

            subscription.PlanName = token.SaaSPlan!.PlanName;
            subscription.MaxStores = token.SaaSPlan.MaxStores;
            subscription.MaxUsers = token.SaaSPlan.MaxUsers;
            subscription.IncludedModulesCsv = token.SaaSPlan.IncludedModulesCsv;
            subscription.ValidFrom = DateTime.UtcNow;
            subscription.ValidTo = token.ExpiresAt; // Or extend based on plan
            subscription.IsActive = true;

            await db.SaveChangesAsync(ct);

            return Results.Ok(new { Message = "Token activated successfully.", Subscription = subscription });
        });
    }

    private static string GenerateSecureToken()
    {
        return "SaaS-" + Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
            .Replace("+", "")
            .Replace("/", "")
            .Replace("=", "")
            .Substring(0, 32);
    }
}

public record GenerateTokenRequest(Guid ClientId, Guid PlanId, int ValidityDays = 365);
public record ActivateTokenRequest(string TokenString, Guid CompanyId);
