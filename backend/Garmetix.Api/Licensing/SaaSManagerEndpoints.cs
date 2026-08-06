using Garmetix.Api.Auth;
using Garmetix.Api.Database;
using Garmetix.Core.Models.SaaS;
using Garmetix.Core.Models.Stores;
using Garmetix.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Garmetix.Api.Licensing;

/// <summary>
/// Multi-tenant SaaS operator console: Clients (the outside businesses paying for a Garmetix license),
/// Plans (quota tiers), Tokens (offline-generated, WhatsApp/email-delivered activation codes), and the
/// resulting per-Company TenantSubscriptions. Client/Plan/Token management is SuperAdmin-only (this is
/// the platform operator's own back office, not something a tenant's own Admin/Owner should see or
/// edit); activating a token against one of a tenant's own companies, and viewing that company's own
/// subscription, is available to any Admin/Owner (see GarmetixPolicies.Admin gating below).
/// </summary>
public static class SaaSManagerEndpoints
{
    public static RouteGroupBuilder MapSaaSManagerEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/saas").WithTags("SaaS Manager");

        group.MapGet("/clients", ListClientsAsync).RequireAuthorization(GarmetixPolicies.SuperAdmin);
        group.MapPost("/clients", CreateClientAsync).RequireAuthorization(GarmetixPolicies.SuperAdmin);
        group.MapPut("/clients/{id:guid}", UpdateClientAsync).RequireAuthorization(GarmetixPolicies.SuperAdmin);
        group.MapDelete("/clients/{id:guid}", DeleteClientAsync).RequireAuthorization(GarmetixPolicies.SuperAdmin);

        group.MapGet("/plans", ListPlansAsync).RequireAuthorization(GarmetixPolicies.SuperAdmin);
        group.MapPost("/plans", CreatePlanAsync).RequireAuthorization(GarmetixPolicies.SuperAdmin);
        group.MapPut("/plans/{id:guid}", UpdatePlanAsync).RequireAuthorization(GarmetixPolicies.SuperAdmin);
        group.MapDelete("/plans/{id:guid}", DeletePlanAsync).RequireAuthorization(GarmetixPolicies.SuperAdmin);

        group.MapGet("/tokens", ListTokensAsync).RequireAuthorization(GarmetixPolicies.SuperAdmin);
        group.MapPost("/tokens/generate", GenerateTokenAsync).RequireAuthorization(GarmetixPolicies.SuperAdmin);
        group.MapDelete("/tokens/{id:guid}", DeleteTokenAsync).RequireAuthorization(GarmetixPolicies.SuperAdmin);

        group.MapPost("/tokens/activate", ActivateTokenAsync).RequireAuthorization(GarmetixPolicies.Admin);
        group.MapGet("/subscriptions", ListSubscriptionsAsync).RequireAuthorization(GarmetixPolicies.Admin);

        group.MapGet("/clients/{clientId:guid}/companies", ListClientCompaniesAsync).RequireAuthorization(GarmetixPolicies.SuperAdmin);
        group.MapPost("/clients/{clientId:guid}/companies/{companyId:guid}/link", LinkCompanyAsync).RequireAuthorization(GarmetixPolicies.SuperAdmin);
        group.MapPost("/clients/{clientId:guid}/companies/{companyId:guid}/unlink", UnlinkCompanyAsync).RequireAuthorization(GarmetixPolicies.SuperAdmin);

        return group;
    }

    // ---------- Clients ----------

    private static async Task<IResult> ListClientsAsync(GarmetixDbContext db, ILoggerFactory loggerFactory, CancellationToken cancellationToken)
    {
        await DatabaseSchemaRepairService.RepairSaaSManagerStorageAsync(db, loggerFactory.CreateLogger("SaaSManagerStorageRepair"), cancellationToken);
        var clients = await db.SaaSClients.AsNoTracking()
            .Where(item => !item.Deleted)
            .OrderBy(item => item.Name)
            .ToListAsync(cancellationToken);
        return Results.Ok(clients);
    }

    private static async Task<IResult> CreateClientAsync(SaveSaaSClientRequest request, GarmetixDbContext db, CancellationToken cancellationToken)
    {
        var code = (request.ClientCode ?? string.Empty).Trim();
        var name = (request.Name ?? string.Empty).Trim();
        if (code.Length < 2 || name.Length < 2)
        {
            return Results.BadRequest(new { message = "Client Code and Name are required." });
        }

        if (await db.SaaSClients.AnyAsync(item => !item.Deleted && item.ClientCode == code, cancellationToken))
        {
            return Results.Conflict(new { message = $"A client with code '{code}' already exists." });
        }

        var client = new SaaSClient
        {
            ClientCode = code,
            Name = name,
            Email = request.Email?.Trim(),
            Mobile = request.Mobile?.Trim(),
            Address = request.Address?.Trim(),
            City = request.City?.Trim(),
            State = request.State?.Trim(),
            Country = string.IsNullOrWhiteSpace(request.Country) ? "India" : request.Country.Trim(),
            ZipCode = request.ZipCode?.Trim(),
            Gstin = string.IsNullOrWhiteSpace(request.Gstin) ? null : request.Gstin.Trim().ToUpperInvariant(),
            Active = request.Active
        };

        db.SaaSClients.Add(client);
        await db.SaveChangesAsync(cancellationToken);
        return Results.Created($"/api/saas/clients/{client.Id}", client);
    }

    private static async Task<IResult> UpdateClientAsync(Guid id, SaveSaaSClientRequest request, GarmetixDbContext db, CancellationToken cancellationToken)
    {
        var client = await db.SaaSClients.FirstOrDefaultAsync(item => !item.Deleted && item.Id == id, cancellationToken);
        if (client is null)
        {
            return Results.NotFound();
        }

        var code = (request.ClientCode ?? string.Empty).Trim();
        var name = (request.Name ?? string.Empty).Trim();
        if (code.Length < 2 || name.Length < 2)
        {
            return Results.BadRequest(new { message = "Client Code and Name are required." });
        }

        if (await db.SaaSClients.AnyAsync(item => !item.Deleted && item.Id != id && item.ClientCode == code, cancellationToken))
        {
            return Results.Conflict(new { message = $"A client with code '{code}' already exists." });
        }

        client.ClientCode = code;
        client.Name = name;
        client.Email = request.Email?.Trim();
        client.Mobile = request.Mobile?.Trim();
        client.Address = request.Address?.Trim();
        client.City = request.City?.Trim();
        client.State = request.State?.Trim();
        client.Country = string.IsNullOrWhiteSpace(request.Country) ? "India" : request.Country.Trim();
        client.ZipCode = request.ZipCode?.Trim();
        client.Gstin = string.IsNullOrWhiteSpace(request.Gstin) ? null : request.Gstin.Trim().ToUpperInvariant();
        client.Active = request.Active;
        client.UpdatedAt = DateTime.UtcNow;

        await db.SaveChangesAsync(cancellationToken);
        return Results.Ok(client);
    }

    private static async Task<IResult> DeleteClientAsync(Guid id, GarmetixDbContext db, CancellationToken cancellationToken)
    {
        var client = await db.SaaSClients.FirstOrDefaultAsync(item => !item.Deleted && item.Id == id, cancellationToken);
        if (client is null)
        {
            return Results.NotFound();
        }

        if (await db.SaaSTokens.AnyAsync(item => !item.Deleted && item.SaaSClientId == id && item.IsActivated, cancellationToken))
        {
            return Results.BadRequest(new { message = "This client has activated tokens. Remove/reassign those first before deleting the client." });
        }

        client.Deleted = true;
        client.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
        return Results.NoContent();
    }

    // ---------- Plans ----------

    private static async Task<IResult> ListPlansAsync(GarmetixDbContext db, CancellationToken cancellationToken)
    {
        var plans = await db.SaaSPlans.AsNoTracking()
            .Where(item => !item.Deleted)
            .OrderBy(item => item.MaxCompanies).ThenBy(item => item.PlanName)
            .ToListAsync(cancellationToken);
        return Results.Ok(plans);
    }

    private static async Task<IResult> CreatePlanAsync(SaveSaaSPlanRequest request, GarmetixDbContext db, CancellationToken cancellationToken)
    {
        var validationError = ValidatePlanRequest(request);
        if (validationError is not null)
        {
            return Results.BadRequest(new { message = validationError });
        }

        var plan = new SaaSPlan
        {
            PlanName = request.PlanName.Trim(),
            MaxCompanies = Math.Max(1, request.MaxCompanies),
            MaxStoreGroups = Math.Max(1, request.MaxStoreGroups),
            MaxStores = Math.Max(1, request.MaxStores),
            MaxUsers = Math.Max(1, request.MaxUsers),
            IncludedModulesCsv = (request.IncludedModulesCsv ?? string.Empty).Trim(),
            Active = request.Active
        };

        db.SaaSPlans.Add(plan);
        await db.SaveChangesAsync(cancellationToken);
        return Results.Created($"/api/saas/plans/{plan.Id}", plan);
    }

    private static async Task<IResult> UpdatePlanAsync(Guid id, SaveSaaSPlanRequest request, GarmetixDbContext db, CancellationToken cancellationToken)
    {
        var plan = await db.SaaSPlans.FirstOrDefaultAsync(item => !item.Deleted && item.Id == id, cancellationToken);
        if (plan is null)
        {
            return Results.NotFound();
        }

        var validationError = ValidatePlanRequest(request);
        if (validationError is not null)
        {
            return Results.BadRequest(new { message = validationError });
        }

        plan.PlanName = request.PlanName.Trim();
        plan.MaxCompanies = Math.Max(1, request.MaxCompanies);
        plan.MaxStoreGroups = Math.Max(1, request.MaxStoreGroups);
        plan.MaxStores = Math.Max(1, request.MaxStores);
        plan.MaxUsers = Math.Max(1, request.MaxUsers);
        plan.IncludedModulesCsv = (request.IncludedModulesCsv ?? string.Empty).Trim();
        plan.Active = request.Active;
        plan.UpdatedAt = DateTime.UtcNow;

        await db.SaveChangesAsync(cancellationToken);
        return Results.Ok(plan);
    }

    private static async Task<IResult> DeletePlanAsync(Guid id, GarmetixDbContext db, CancellationToken cancellationToken)
    {
        var plan = await db.SaaSPlans.FirstOrDefaultAsync(item => !item.Deleted && item.Id == id, cancellationToken);
        if (plan is null)
        {
            return Results.NotFound();
        }

        if (await db.SaaSTokens.AnyAsync(item => !item.Deleted && item.SaaSPlanId == id, cancellationToken))
        {
            return Results.BadRequest(new { message = "This plan already has tokens issued against it. Deactivate it instead of deleting." });
        }

        plan.Deleted = true;
        plan.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
        return Results.NoContent();
    }

    private static string? ValidatePlanRequest(SaveSaaSPlanRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.PlanName) || request.PlanName.Trim().Length < 2)
        {
            return "Plan Name is required.";
        }

        return null;
    }

    // ---------- Tokens ----------

    private static async Task<IResult> ListTokensAsync(GarmetixDbContext db, CancellationToken cancellationToken)
    {
        var tokens = await db.SaaSTokens.AsNoTracking().Where(item => !item.Deleted).OrderByDescending(item => item.CreatedAt).ToListAsync(cancellationToken);
        var clientNames = await db.SaaSClients.AsNoTracking().Where(item => !item.Deleted).ToDictionaryAsync(item => item.Id, item => item.Name, cancellationToken);
        var planNames = await db.SaaSPlans.AsNoTracking().Where(item => !item.Deleted).ToDictionaryAsync(item => item.Id, item => item.PlanName, cancellationToken);
        var companyIds = tokens.Where(item => item.ActivatedCompanyId.HasValue).Select(item => item.ActivatedCompanyId!.Value).Distinct().ToList();
        var companyNames = await db.Companies.AsNoTracking().Where(item => companyIds.Contains(item.Id)).ToDictionaryAsync(item => item.Id, item => item.Name, cancellationToken);

        var result = tokens.Select(token => new SaaSTokenDto(
            token.Id,
            token.TokenString,
            token.SaaSClientId,
            clientNames.GetValueOrDefault(token.SaaSClientId, "Unknown Client"),
            token.SaaSPlanId,
            planNames.GetValueOrDefault(token.SaaSPlanId, "Unknown Plan"),
            token.ValidityDays,
            token.ExpiresAt,
            token.IsActivated,
            token.ActivatedAtUtc,
            token.ActivatedCompanyId,
            token.ActivatedCompanyId.HasValue ? companyNames.GetValueOrDefault(token.ActivatedCompanyId.Value, "Unknown Company") : null,
            token.Notes)).ToList();

        return Results.Ok(result);
    }

    private static async Task<IResult> GenerateTokenAsync(GenerateSaaSTokenRequest request, GarmetixDbContext db, CancellationToken cancellationToken)
    {
        var client = await db.SaaSClients.AsNoTracking().FirstOrDefaultAsync(item => !item.Deleted && item.Id == request.SaaSClientId, cancellationToken);
        if (client is null)
        {
            return Results.BadRequest(new { message = "Select a valid client." });
        }

        var plan = await db.SaaSPlans.AsNoTracking().FirstOrDefaultAsync(item => !item.Deleted && item.Id == request.SaaSPlanId, cancellationToken);
        if (plan is null)
        {
            return Results.BadRequest(new { message = "Select a valid plan." });
        }

        var validityDays = request.ValidityDays.GetValueOrDefault(365);
        if (validityDays <= 0)
        {
            return Results.BadRequest(new { message = "Validity days must be greater than zero." });
        }

        var token = new SaaSToken
        {
            TokenString = GenerateTokenString(client.ClientCode),
            SaaSClientId = client.Id,
            SaaSPlanId = plan.Id,
            ValidityDays = validityDays,
            ExpiresAt = DateTime.UtcNow.AddDays(validityDays),
            IsActivated = false,
            Notes = string.IsNullOrWhiteSpace(request.Notes) ? null : request.Notes.Trim()
        };

        db.SaaSTokens.Add(token);
        await db.SaveChangesAsync(cancellationToken);

        return Results.Created($"/api/saas/tokens/{token.Id}", new SaaSTokenDto(
            token.Id, token.TokenString, client.Id, client.Name, plan.Id, plan.PlanName,
            token.ValidityDays, token.ExpiresAt, false, null, null, null, token.Notes));
    }

    private static async Task<IResult> DeleteTokenAsync(Guid id, GarmetixDbContext db, CancellationToken cancellationToken)
    {
        var token = await db.SaaSTokens.FirstOrDefaultAsync(item => !item.Deleted && item.Id == id, cancellationToken);
        if (token is null)
        {
            return Results.NotFound();
        }

        if (token.IsActivated)
        {
            return Results.BadRequest(new { message = "This token is already activated and is now the record of an active subscription. It cannot be deleted." });
        }

        token.Deleted = true;
        token.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
        return Results.NoContent();
    }

    private static async Task<IResult> ActivateTokenAsync(
        ActivateSaaSTokenRequest request,
        GarmetixDbContext db,
        SaaSValidationService saasValidation,
        CancellationToken cancellationToken)
    {
        var tokenString = (request.TokenString ?? string.Empty).Trim();
        if (tokenString.Length == 0)
        {
            return Results.BadRequest(new { message = "License token is required." });
        }

        var token = await db.SaaSTokens.FirstOrDefaultAsync(item => !item.Deleted && item.TokenString == tokenString, cancellationToken);
        if (token is null)
        {
            return Results.BadRequest(new { message = "This token was not recognized. Check the token and try again." });
        }

        if (token.ExpiresAt <= DateTime.UtcNow)
        {
            return Results.BadRequest(new { message = "This token has expired. Contact your SaaS provider for a new one." });
        }

        if (token.IsActivated && token.ActivatedCompanyId != request.CompanyId)
        {
            return Results.BadRequest(new { message = "This token has already been activated against a different company." });
        }

        var company = await db.Companies.AsNoTracking().FirstOrDefaultAsync(item => !item.Deleted && item.Id == request.CompanyId, cancellationToken);
        if (company is null)
        {
            return Results.BadRequest(new { message = "Select a valid company to activate this token against." });
        }

        var plan = await db.SaaSPlans.AsNoTracking().FirstOrDefaultAsync(item => !item.Deleted && item.Id == token.SaaSPlanId, cancellationToken);
        if (plan is null)
        {
            return Results.BadRequest(new { message = "The plan behind this token no longer exists. Contact your SaaS provider." });
        }

        var quotaError = await saasValidation.ValidateTokenActivationAsync(db, token.SaaSClientId, request.CompanyId, plan, cancellationToken);
        if (quotaError is not null)
        {
            return Results.BadRequest(new { message = quotaError });
        }

        var existing = await db.TenantSubscriptions.FirstOrDefaultAsync(item => !item.Deleted && item.CompanyId == request.CompanyId, cancellationToken);
        var now = DateTime.UtcNow;
        var validTo = now.AddDays(token.ValidityDays);

        if (existing is null)
        {
            existing = new TenantSubscription
            {
                CompanyId = request.CompanyId
            };
            db.TenantSubscriptions.Add(existing);
        }

        existing.SaaSClientId = token.SaaSClientId;
        existing.SaaSPlanId = plan.Id;
        existing.SaaSTokenId = token.Id;
        existing.PlanName = plan.PlanName;
        existing.MaxCompanies = plan.MaxCompanies;
        existing.MaxStoreGroups = plan.MaxStoreGroups;
        existing.MaxStores = plan.MaxStores;
        existing.MaxUsers = plan.MaxUsers;
        existing.ValidFrom = now;
        existing.ValidTo = validTo;
        existing.IsActive = true;
        existing.UpdatedAt = now;

        token.IsActivated = true;
        token.ActivatedAtUtc = now;
        token.ActivatedCompanyId = request.CompanyId;
        token.UpdatedAt = now;

        await db.SaveChangesAsync(cancellationToken);

        var dto = await BuildSubscriptionDtoAsync(db, existing, cancellationToken);
        return Results.Ok(new ActivateSaaSTokenResponse("Token activated and subscription is now active.", dto));
    }

    private static async Task<IResult> ListSubscriptionsAsync(HttpContext context, GarmetixDbContext db, CancellationToken cancellationToken)
    {
        var query = db.TenantSubscriptions.AsNoTracking().Where(item => !item.Deleted);
        if (!Garmetix.Api.Workspace.WorkspaceScope.HasFullAccess(context))
        {
            query = Garmetix.Api.Workspace.WorkspaceScope.ApplyTo(query, context);
        }

        var subscriptions = await query.OrderByDescending(item => item.ValidTo).ToListAsync(cancellationToken);
        var result = new List<TenantSubscriptionDto>(subscriptions.Count);
        foreach (var subscription in subscriptions)
        {
            result.Add(await BuildSubscriptionDtoAsync(db, subscription, cancellationToken));
        }

        return Results.Ok(result);
    }

    // ---------- Client <-> Company linking ----------

    private static async Task<IResult> ListClientCompaniesAsync(Guid clientId, GarmetixDbContext db, CancellationToken cancellationToken)
    {
        var companies = await db.Companies.AsNoTracking().Where(item => !item.Deleted).OrderBy(item => item.Name).ToListAsync(cancellationToken);
        var result = companies.Select(company => new SaaSClientCompanyDto(
            company.Id,
            company.Name,
            company.Code,
            company.SaaSClientId == clientId,
            company.SaaSClientId.HasValue && company.SaaSClientId != clientId)).ToList();
        return Results.Ok(result);
    }

    private static async Task<IResult> LinkCompanyAsync(Guid clientId, Guid companyId, GarmetixDbContext db, SaaSValidationService saasValidation, CancellationToken cancellationToken)
    {
        var client = await db.SaaSClients.AsNoTracking().FirstOrDefaultAsync(item => !item.Deleted && item.Id == clientId, cancellationToken);
        if (client is null)
        {
            return Results.NotFound(new { message = "Client not found." });
        }

        var company = await db.Companies.FirstOrDefaultAsync(item => !item.Deleted && item.Id == companyId, cancellationToken);
        if (company is null)
        {
            return Results.NotFound(new { message = "Company not found." });
        }

        if (company.SaaSClientId.HasValue && company.SaaSClientId != clientId)
        {
            return Results.BadRequest(new { message = "This company is already linked to a different SaaS client. Unlink it there first." });
        }

        var quotaError = await saasValidation.ValidateCompanyLinkAsync(db, clientId, companyId, cancellationToken);
        if (quotaError is not null)
        {
            return Results.BadRequest(new { message = quotaError });
        }

        company.SaaSClientId = clientId;
        company.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
        return Results.Ok(new { message = $"{company.Name} linked to {client.Name}." });
    }

    private static async Task<IResult> UnlinkCompanyAsync(Guid clientId, Guid companyId, GarmetixDbContext db, CancellationToken cancellationToken)
    {
        var company = await db.Companies.FirstOrDefaultAsync(item => !item.Deleted && item.Id == companyId && item.SaaSClientId == clientId, cancellationToken);
        if (company is null)
        {
            return Results.NotFound(new { message = "This company is not linked to this client." });
        }

        company.SaaSClientId = null;
        company.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
        return Results.Ok(new { message = $"{company.Name} unlinked. It is no longer subject to any SaaS quota or expiry enforcement." });
    }

    private static async Task<TenantSubscriptionDto> BuildSubscriptionDtoAsync(GarmetixDbContext db, TenantSubscription subscription, CancellationToken cancellationToken)
    {
        var company = await db.Companies.AsNoTracking().FirstOrDefaultAsync(item => item.Id == subscription.CompanyId, cancellationToken);
        var client = await db.SaaSClients.AsNoTracking().FirstOrDefaultAsync(item => item.Id == subscription.SaaSClientId, cancellationToken);
        var currentStoreGroups = await db.StoreGroups.AsNoTracking().CountAsync(item => !item.Deleted && item.CompanyId == subscription.CompanyId, cancellationToken);
        var currentStores = await db.Stores.AsNoTracking().CountAsync(item => !item.Deleted && item.CompanyId == subscription.CompanyId, cancellationToken);
        var currentUsers = await db.Users.AsNoTracking().CountAsync(item => item.CompanyId == subscription.CompanyId, cancellationToken);

        return new TenantSubscriptionDto(
            subscription.Id,
            subscription.CompanyId,
            company?.Name ?? "Unknown Company",
            subscription.SaaSClientId,
            client?.Name ?? "Unknown Client",
            subscription.SaaSPlanId,
            subscription.PlanName,
            subscription.MaxCompanies,
            subscription.MaxStoreGroups,
            subscription.MaxStores,
            subscription.MaxUsers,
            currentStoreGroups,
            currentStores,
            currentUsers,
            subscription.ValidFrom,
            subscription.ValidTo,
            subscription.IsActive && subscription.ValidTo >= DateTime.UtcNow);
    }

    private static string GenerateTokenString(string clientCode)
    {
        var normalizedCode = new string((clientCode ?? string.Empty).Trim().ToUpperInvariant().Where(char.IsLetterOrDigit).ToArray());
        if (normalizedCode.Length == 0)
        {
            normalizedCode = "CLIENT";
        }

        return $"GMX-SAAS-{normalizedCode}-{Guid.NewGuid():N}".ToUpperInvariant();
    }
}
