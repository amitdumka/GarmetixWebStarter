using Garmetix.Core.Models.SaaS;
using Garmetix.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Garmetix.Api.Licensing;

public sealed record SaaSEffectiveQuota(int MaxCompanies, int MaxStoreGroups, int MaxStores, int MaxUsers, string PlanLabel, bool IsTrialDefault);

/// <summary>
/// Quota enforcement for the SaaS Manager module. The one and only gate that decides whether any of
/// this ever applies is Company.SaaSClientId: a company nobody has explicitly linked to a SaaS client
/// (every company that existed before this module, and every future company unless someone
/// deliberately links it) is never checked against anything here, no matter what plan/trial-default
/// values are configured. Linked companies fall back to a configurable trial default
/// (SaaSOptions.TrialMax*) whenever they have no active TenantSubscription - matching the exact
/// "never block trial-mode API requests when no subscription is found" precedent this module was
/// designed around, generalized so an un-linked company is trial-mode forever, not just until someone
/// remembers to activate a token.
/// </summary>
public sealed class SaaSValidationService(IOptions<SaaSOptions> options)
{
    public async Task<TenantSubscription?> GetActiveSubscriptionAsync(GarmetixDbContext db, Guid companyId, CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        return await db.TenantSubscriptions.AsNoTracking()
            .Where(item => !item.Deleted && item.CompanyId == companyId && item.IsActive && item.ValidTo >= now)
            .OrderByDescending(item => item.ValidTo)
            .FirstOrDefaultAsync(cancellationToken);
    }

    /// <summary>Returns null for any company that is not linked to a SaaS client - meaning "never enforce anything here."</summary>
    public async Task<SaaSEffectiveQuota?> GetEffectiveQuotaAsync(GarmetixDbContext db, Guid companyId, CancellationToken cancellationToken)
    {
        var company = await db.Companies.AsNoTracking().FirstOrDefaultAsync(item => item.Id == companyId, cancellationToken);
        if (company?.SaaSClientId is null)
        {
            return null;
        }

        var subscription = await GetActiveSubscriptionAsync(db, companyId, cancellationToken);
        if (subscription is not null)
        {
            return new SaaSEffectiveQuota(subscription.MaxCompanies, subscription.MaxStoreGroups, subscription.MaxStores, subscription.MaxUsers, subscription.PlanName, false);
        }

        var trial = options.Value;
        return new SaaSEffectiveQuota(trial.TrialMaxCompanies, trial.TrialMaxStoreGroups, trial.TrialMaxStores, trial.TrialMaxUsers, "Trial", true);
    }

    public async Task<string?> ValidateStoreGroupCreationAsync(GarmetixDbContext db, Guid companyId, CancellationToken cancellationToken)
    {
        var quota = await GetEffectiveQuotaAsync(db, companyId, cancellationToken);
        if (quota is null)
        {
            return null;
        }

        var count = await db.StoreGroups.AsNoTracking().CountAsync(group => !group.Deleted && group.CompanyId == companyId, cancellationToken);
        return count >= quota.MaxStoreGroups
            ? $"{QuotaLabel(quota)} allows up to {quota.MaxStoreGroups} store group(s) for this company. {UpgradeHint(quota)}"
            : null;
    }

    public async Task<string?> ValidateStoreCreationAsync(GarmetixDbContext db, Guid companyId, CancellationToken cancellationToken)
    {
        var quota = await GetEffectiveQuotaAsync(db, companyId, cancellationToken);
        if (quota is null)
        {
            return null;
        }

        var count = await db.Stores.AsNoTracking().CountAsync(store => !store.Deleted && store.CompanyId == companyId, cancellationToken);
        return count >= quota.MaxStores
            ? $"{QuotaLabel(quota)} allows up to {quota.MaxStores} store(s) for this company. {UpgradeHint(quota)}"
            : null;
    }

    public async Task<string?> ValidateUserCreationAsync(GarmetixDbContext db, Guid? companyId, CancellationToken cancellationToken)
    {
        if (companyId is null)
        {
            return null;
        }

        var quota = await GetEffectiveQuotaAsync(db, companyId.Value, cancellationToken);
        if (quota is null)
        {
            return null;
        }

        var count = await db.Users.AsNoTracking().CountAsync(user => user.CompanyId == companyId, cancellationToken);
        return count >= quota.MaxUsers
            ? $"{QuotaLabel(quota)} allows up to {quota.MaxUsers} user(s) for this company. {UpgradeHint(quota)}"
            : null;
    }

    /// <summary>Enforced at token-activation time: how many distinct companies this client already has an active subscription against.</summary>
    public async Task<string?> ValidateTokenActivationAsync(GarmetixDbContext db, Guid saaSClientId, Guid companyId, SaaSPlan plan, CancellationToken cancellationToken)
    {
        var activeCompanyIds = await db.TenantSubscriptions.AsNoTracking()
            .Where(item => !item.Deleted && item.SaaSClientId == saaSClientId && item.IsActive)
            .Select(item => item.CompanyId)
            .Distinct()
            .ToListAsync(cancellationToken);

        if (!activeCompanyIds.Contains(companyId) && activeCompanyIds.Count >= plan.MaxCompanies)
        {
            return $"Plan '{plan.PlanName}' allows up to {plan.MaxCompanies} company/companies for this client. Contact your SaaS provider for a higher-tier plan.";
        }

        return null;
    }

    /// <summary>Enforced when linking a company to a client directly (independent of any token): the client's currently-linked company count against its best active plan, or the trial default if it has none.</summary>
    public async Task<string?> ValidateCompanyLinkAsync(GarmetixDbContext db, Guid saaSClientId, Guid companyId, CancellationToken cancellationToken)
    {
        var linkedCount = await db.Companies.AsNoTracking().CountAsync(company => !company.Deleted && company.SaaSClientId == saaSClientId && company.Id != companyId, cancellationToken);
        var bestActivePlanCap = await db.TenantSubscriptions.AsNoTracking()
            .Where(item => !item.Deleted && item.SaaSClientId == saaSClientId && item.IsActive && item.ValidTo >= DateTime.UtcNow)
            .OrderByDescending(item => item.MaxCompanies)
            .Select(item => item.MaxCompanies)
            .FirstOrDefaultAsync(cancellationToken);

        var cap = bestActivePlanCap > 0 ? bestActivePlanCap : options.Value.TrialMaxCompanies;
        return linkedCount >= cap
            ? $"This client is already linked to {linkedCount} company/companies, at or above its plan/trial cap of {cap}. Activate a higher-tier plan before linking another company."
            : null;
    }

    private static string QuotaLabel(SaaSEffectiveQuota quota) => quota.IsTrialDefault ? "The trial default" : $"Plan '{quota.PlanLabel}'";

    private static string UpgradeHint(SaaSEffectiveQuota quota) => quota.IsTrialDefault
        ? "Activate a paid plan on the /subscription page to raise this limit."
        : "Upgrade the plan or contact your SaaS provider.";
}
