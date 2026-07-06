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

        // ─── CLIENTS ────────────────────────────────────────────────────────
        group.MapGet("/clients", async (GarmetixDbContext db, CancellationToken ct) =>
        {
            var clients = await db.SaaSClients
                .Where(c => !c.Deleted)
                .OrderBy(c => c.Name)
                .ToListAsync(ct);
            return Results.Ok(clients);
        });

        group.MapPost("/clients", async (GarmetixDbContext db, [FromBody] SaaSClient client, CancellationToken ct) =>
        {
            if (await db.SaaSClients.AnyAsync(c => c.ClientCode == client.ClientCode && !c.Deleted, ct))
                return Results.BadRequest(new { message = $"Client code '{client.ClientCode}' already exists." });

            db.SaaSClients.Add(client);
            await db.SaveChangesAsync(ct);
            return Results.Created($"/api/saas/clients/{client.Id}", client);
        });

        group.MapPut("/clients/{id:guid}", async (Guid id, GarmetixDbContext db, [FromBody] SaaSClient update, CancellationToken ct) =>
        {
            var client = await db.SaaSClients.FindAsync([id], ct);
            if (client == null || client.Deleted) return Results.NotFound();

            client.ClientCode = update.ClientCode;
            client.Name       = update.Name;
            client.Email      = update.Email;
            client.Mobile     = update.Mobile;
            client.Address    = update.Address;
            client.City       = update.City;
            client.State      = update.State;
            client.Country    = update.Country;
            client.ZipCode    = update.ZipCode;
            client.GSTIN      = update.GSTIN;
            client.IsActive   = update.IsActive;

            await db.SaveChangesAsync(ct);
            return Results.Ok(client);
        });

        group.MapDelete("/clients/{id:guid}", async (Guid id, GarmetixDbContext db, CancellationToken ct) =>
        {
            var client = await db.SaaSClients.FindAsync([id], ct);
            if (client == null || client.Deleted) return Results.NotFound();

            client.Deleted = true;
            await db.SaveChangesAsync(ct);
            return Results.NoContent();
        });

        // ─── PLANS ──────────────────────────────────────────────────────────
        group.MapGet("/plans", async (GarmetixDbContext db, CancellationToken ct) =>
        {
            var plans = await db.SaaSPlans
                .Where(p => !p.Deleted)
                .OrderBy(p => p.PlanName)
                .ToListAsync(ct);
            return Results.Ok(plans);
        });

        group.MapPost("/plans", async (GarmetixDbContext db, [FromBody] SaaSPlan plan, CancellationToken ct) =>
        {
            db.SaaSPlans.Add(plan);
            await db.SaveChangesAsync(ct);
            return Results.Created($"/api/saas/plans/{plan.Id}", plan);
        });

        group.MapPut("/plans/{id:guid}", async (Guid id, GarmetixDbContext db, [FromBody] SaaSPlan update, CancellationToken ct) =>
        {
            var plan = await db.SaaSPlans.FindAsync([id], ct);
            if (plan == null || plan.Deleted) return Results.NotFound();

            plan.PlanName           = update.PlanName;
            plan.MaxCompanies       = update.MaxCompanies;
            plan.MaxStoreGroups     = update.MaxStoreGroups;
            plan.MaxStores          = update.MaxStores;
            plan.MaxUsers           = update.MaxUsers;
            plan.IncludedModulesCsv = update.IncludedModulesCsv;
            plan.IsActive           = update.IsActive;

            await db.SaveChangesAsync(ct);
            return Results.Ok(plan);
        });

        group.MapDelete("/plans/{id:guid}", async (Guid id, GarmetixDbContext db, CancellationToken ct) =>
        {
            var plan = await db.SaaSPlans.FindAsync([id], ct);
            if (plan == null || plan.Deleted) return Results.NotFound();

            plan.Deleted = true;
            await db.SaveChangesAsync(ct);
            return Results.NoContent();
        });

        // ─── TOKENS ─────────────────────────────────────────────────────────
        group.MapGet("/tokens", async (GarmetixDbContext db, CancellationToken ct) =>
        {
            var tokens = await db.SaaSTokens
                .Include(t => t.SaaSClient)
                .Include(t => t.SaaSPlan)
                .Where(t => !t.Deleted)
                .OrderByDescending(t => t.CreatedAt)
                .Select(t => new
                {
                    t.Id,
                    t.TokenString,
                    t.IsActivated,
                    t.ActivatedAt,
                    t.ExpiresAt,
                    t.CreatedAt,
                    SaaSClient = t.SaaSClient == null ? null : new { t.SaaSClient.Id, t.SaaSClient.Name, t.SaaSClient.ClientCode },
                    SaaSPlan   = t.SaaSPlan   == null ? null : new { t.SaaSPlan.Id,   t.SaaSPlan.PlanName }
                })
                .ToListAsync(ct);
            return Results.Ok(tokens);
        });

        group.MapDelete("/tokens/{id:guid}", async (Guid id, GarmetixDbContext db, CancellationToken ct) =>
        {
            var token = await db.SaaSTokens.FindAsync([id], ct);
            if (token == null || token.Deleted) return Results.NotFound();

            token.Deleted = true;
            await db.SaveChangesAsync(ct);
            return Results.NoContent();
        });

        // ─── SUBSCRIPTIONS ───────────────────────────────────────────────────
        group.MapGet("/subscriptions", async (GarmetixDbContext db, CancellationToken ct) =>
        {
            var subscriptions = await db.TenantSubscriptions
                .Include(s => s.Company)
                .Where(s => !s.Deleted)
                .OrderByDescending(s => s.ValidFrom)
                .Select(s => new
                {
                    s.Id,
                    s.PlanName,
                    s.ValidFrom,
                    s.ValidTo,
                    s.IsActive,
                    s.MaxStores,
                    s.MaxUsers,
                    s.IncludedModulesCsv,
                    Company = s.Company == null ? null : new { s.Company.Id, s.Company.Name, s.Company.Code }
                })
                .ToListAsync(ct);
            return Results.Ok(subscriptions);
        });

        // ─── TOKEN GENERATE ──────────────────────────────────────────────────
        group.MapPost("/tokens/generate", async (GarmetixDbContext db, [FromBody] GenerateTokenRequest req, CancellationToken ct) =>
        {
            var client = await db.SaaSClients.FindAsync([req.ClientId], ct);
            var plan   = await db.SaaSPlans.FindAsync([req.PlanId], ct);

            if (client == null || client.Deleted) return Results.BadRequest(new { message = "Invalid or deleted client." });
            if (plan   == null || plan.Deleted)   return Results.BadRequest(new { message = "Invalid or deleted plan."   });

            var tokenString = GenerateSecureToken();
            var token = new SaaSToken
            {
                SaaSClientId = client.Id,
                SaaSPlanId   = plan.Id,
                TokenString  = tokenString,
                ExpiresAt    = DateTime.UtcNow.AddDays(req.ValidityDays),
                IsActivated  = false
            };

            db.SaaSTokens.Add(token);
            await db.SaveChangesAsync(ct);

            return Results.Ok(new { token.Id, token.TokenString, token.ExpiresAt });
        });

        // ─── TOKEN ACTIVATE ──────────────────────────────────────────────────
        group.MapPost("/tokens/activate", async (GarmetixDbContext db, [FromBody] ActivateTokenRequest req, CancellationToken ct) =>
        {
            var token = await db.SaaSTokens
                .Include(t => t.SaaSPlan)
                .FirstOrDefaultAsync(t => t.TokenString == req.TokenString && !t.Deleted, ct);

            if (token == null)              return Results.BadRequest(new { message = "Invalid token." });
            if (token.IsActivated)         return Results.BadRequest(new { message = "Token is already activated." });
            if (token.ExpiresAt < DateTime.UtcNow) return Results.BadRequest(new { message = "Token has expired." });

            var company = await db.Companies.FindAsync([req.CompanyId], ct);
            if (company == null) return Results.BadRequest(new { message = "Company not found." });

            token.IsActivated = true;
            token.ActivatedAt = DateTime.UtcNow;
            company.SaaSClientId = token.SaaSClientId;

            var subscription = await db.TenantSubscriptions.FirstOrDefaultAsync(s => s.CompanyId == company.Id, ct);
            if (subscription == null)
            {
                subscription = new TenantSubscription { CompanyId = company.Id };
                db.TenantSubscriptions.Add(subscription);
            }

            subscription.PlanName           = token.SaaSPlan!.PlanName;
            subscription.MaxStores          = token.SaaSPlan.MaxStores;
            subscription.MaxUsers           = token.SaaSPlan.MaxUsers;
            subscription.IncludedModulesCsv = token.SaaSPlan.IncludedModulesCsv;
            subscription.ValidFrom          = DateTime.UtcNow;
            subscription.ValidTo            = token.ExpiresAt;
            subscription.IsActive           = true;

            await db.SaveChangesAsync(ct);
            return Results.Ok(new { message = "Token activated successfully." });
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
