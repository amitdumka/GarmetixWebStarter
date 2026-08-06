using Garmetix.Api.Licensing;
using Garmetix.Api.Tests.Infrastructure;
using Garmetix.Core.Models.SaaS;
using Garmetix.Core.Models.Stores;
using Garmetix.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Xunit;

namespace Garmetix.Api.Tests.Licensing;

/// <summary>
/// Postgres-only (skipped without GARMETIX_TEST_POSTGRES, same as every other Postgres-gated test
/// here). Verifies the one rule the whole SaaS Manager module's safety depends on: a Company with no
/// SaaSClientId is never touched by any quota/expiry logic, no matter what plans/trial defaults exist -
/// and that a linked company correctly falls back to the configured trial defaults until (and unless)
/// it has an actual active subscription.
/// </summary>
public sealed class SaaSValidationServiceTests
{
    [PostgresFact]
    public async Task GetEffectiveQuotaAsync_ReturnsNull_ForCompanyNotLinkedToAnyClient()
    {
        var createdCompanyIds = new List<Guid>();
        try
        {
            await using var db = CreateDbContext();
            var company = new Company { Name = "Unlinked Co", Code = "UNLNK1" };
            db.Companies.Add(company);
            await db.SaveChangesAsync();
            createdCompanyIds.Add(company.Id);

            var service = new SaaSValidationService(DefaultOptions());
            var quota = await service.GetEffectiveQuotaAsync(db, company.Id, CancellationToken.None);

            Assert.Null(quota);
        }
        finally
        {
            await CleanupAsync(createdCompanyIds, [], [], []);
        }
    }

    [PostgresFact]
    public async Task GetEffectiveQuotaAsync_ReturnsTrialDefaults_ForLinkedCompanyWithNoActiveSubscription()
    {
        var createdCompanyIds = new List<Guid>();
        var createdClientIds = new List<Guid>();
        try
        {
            await using var db = CreateDbContext();
            var client = new SaaSClient { ClientCode = $"CLI-{Guid.NewGuid():N}"[..12], Name = "Test Client" };
            db.SaaSClients.Add(client);
            var company = new Company { Name = "Linked No Sub", Code = "LNKNOSUB1", SaaSClientId = client.Id };
            db.Companies.Add(company);
            await db.SaveChangesAsync();
            createdClientIds.Add(client.Id);
            createdCompanyIds.Add(company.Id);

            var service = new SaaSValidationService(DefaultOptions());
            var quota = await service.GetEffectiveQuotaAsync(db, company.Id, CancellationToken.None);

            Assert.NotNull(quota);
            Assert.True(quota!.IsTrialDefault);
            Assert.Equal(1, quota.MaxCompanies);
            Assert.Equal(1, quota.MaxStoreGroups);
            Assert.Equal(2, quota.MaxStores);
            Assert.Equal(20, quota.MaxUsers);
        }
        finally
        {
            await CleanupAsync(createdCompanyIds, createdClientIds, [], []);
        }
    }

    [PostgresFact]
    public async Task GetEffectiveQuotaAsync_ReturnsSubscriptionQuota_WhenActiveSubscriptionExists()
    {
        var createdCompanyIds = new List<Guid>();
        var createdClientIds = new List<Guid>();
        var createdSubscriptionIds = new List<Guid>();
        try
        {
            await using var db = CreateDbContext();
            var client = new SaaSClient { ClientCode = $"CLI-{Guid.NewGuid():N}"[..12], Name = "Paid Client" };
            var plan = new SaaSPlan { PlanName = "Pro", MaxCompanies = 5, MaxStoreGroups = 5, MaxStores = 25, MaxUsers = 200 };
            db.SaaSClients.Add(client);
            db.SaaSPlans.Add(plan);
            var company = new Company { Name = "Paid Co", Code = "PAIDCO1", SaaSClientId = client.Id };
            db.Companies.Add(company);
            await db.SaveChangesAsync();

            var subscription = new TenantSubscription
            {
                CompanyId = company.Id,
                SaaSClientId = client.Id,
                SaaSPlanId = plan.Id,
                PlanName = plan.PlanName,
                MaxCompanies = plan.MaxCompanies,
                MaxStoreGroups = plan.MaxStoreGroups,
                MaxStores = plan.MaxStores,
                MaxUsers = plan.MaxUsers,
                ValidFrom = DateTime.UtcNow.AddDays(-1),
                ValidTo = DateTime.UtcNow.AddDays(30),
                IsActive = true
            };
            db.TenantSubscriptions.Add(subscription);
            await db.SaveChangesAsync();

            createdClientIds.Add(client.Id);
            createdCompanyIds.Add(company.Id);
            createdSubscriptionIds.Add(subscription.Id);

            var service = new SaaSValidationService(DefaultOptions());
            var quota = await service.GetEffectiveQuotaAsync(db, company.Id, CancellationToken.None);

            Assert.NotNull(quota);
            Assert.False(quota!.IsTrialDefault);
            Assert.Equal("Pro", quota.PlanLabel);
            Assert.Equal(25, quota.MaxStores);
            Assert.Equal(200, quota.MaxUsers);
        }
        finally
        {
            await CleanupSubscriptionsAsync(createdSubscriptionIds);
            await CleanupAsync(createdCompanyIds, createdClientIds, [], []);
        }
    }

    [PostgresFact]
    public async Task ValidateStoreCreationAsync_Blocks_WhenLinkedCompanyAtTrialStoreLimit()
    {
        var createdCompanyIds = new List<Guid>();
        var createdClientIds = new List<Guid>();
        var createdStoreGroupIds = new List<Guid>();
        var createdStoreIds = new List<Guid>();
        try
        {
            await using var db = CreateDbContext();
            var client = new SaaSClient { ClientCode = $"CLI-{Guid.NewGuid():N}"[..12], Name = "At Limit Client" };
            db.SaaSClients.Add(client);
            var company = new Company { Name = "At Limit Co", Code = "ATLIM1", SaaSClientId = client.Id };
            db.Companies.Add(company);
            await db.SaveChangesAsync();
            createdClientIds.Add(client.Id);
            createdCompanyIds.Add(company.Id);

            var group = new StoreGroup { Name = "Group", GroupCode = "GRP1", CompanyId = company.Id };
            db.StoreGroups.Add(group);
            await db.SaveChangesAsync();
            createdStoreGroupIds.Add(group.Id);

            // Trial default MaxStores is 2 - seed exactly 2 existing stores so the next one must be blocked.
            var storeA = new Store { Name = "Store A", StoreCode = "SA1", CompanyId = company.Id, StoreGroupId = group.Id };
            var storeB = new Store { Name = "Store B", StoreCode = "SB1", CompanyId = company.Id, StoreGroupId = group.Id };
            db.Stores.AddRange(storeA, storeB);
            await db.SaveChangesAsync();
            createdStoreIds.AddRange([storeA.Id, storeB.Id]);

            var service = new SaaSValidationService(DefaultOptions());
            var error = await service.ValidateStoreCreationAsync(db, company.Id, CancellationToken.None);

            Assert.NotNull(error);
            Assert.Contains("2 store(s)", error);
        }
        finally
        {
            await CleanupStoresAsync(createdStoreIds);
            await CleanupStoreGroupsAsync(createdStoreGroupIds);
            await CleanupAsync(createdCompanyIds, createdClientIds, [], []);
        }
    }

    [PostgresFact]
    public async Task ValidateStoreCreationAsync_NeverBlocks_ForCompanyNotLinkedToAnyClient()
    {
        var createdCompanyIds = new List<Guid>();
        var createdStoreGroupIds = new List<Guid>();
        var createdStoreIds = new List<Guid>();
        try
        {
            await using var db = CreateDbContext();
            var company = new Company { Name = "Unlinked Many Stores", Code = "UNLMANY1" };
            db.Companies.Add(company);
            await db.SaveChangesAsync();
            createdCompanyIds.Add(company.Id);

            var group = new StoreGroup { Name = "Group", GroupCode = "GRPU1", CompanyId = company.Id };
            db.StoreGroups.Add(group);
            await db.SaveChangesAsync();
            createdStoreGroupIds.Add(group.Id);

            // Well past the trial default of 2 - proves an un-linked company is never capped by it.
            for (var i = 0; i < 5; i++)
            {
                var store = new Store { Name = $"Store {i}", StoreCode = $"US{i}", CompanyId = company.Id, StoreGroupId = group.Id };
                db.Stores.Add(store);
                await db.SaveChangesAsync();
                createdStoreIds.Add(store.Id);
            }

            var service = new SaaSValidationService(DefaultOptions());
            var error = await service.ValidateStoreCreationAsync(db, company.Id, CancellationToken.None);

            Assert.Null(error);
        }
        finally
        {
            await CleanupStoresAsync(createdStoreIds);
            await CleanupStoreGroupsAsync(createdStoreGroupIds);
            await CleanupAsync(createdCompanyIds, [], [], []);
        }
    }

    [PostgresFact]
    public async Task ValidateTokenActivationAsync_Blocks_WhenClientAtMaxCompaniesForPlan()
    {
        var createdCompanyIds = new List<Guid>();
        var createdClientIds = new List<Guid>();
        var createdSubscriptionIds = new List<Guid>();
        try
        {
            await using var db = CreateDbContext();
            var client = new SaaSClient { ClientCode = $"CLI-{Guid.NewGuid():N}"[..12], Name = "Single Company Client" };
            var plan = new SaaSPlan { PlanName = "Basic", MaxCompanies = 1, MaxStoreGroups = 1, MaxStores = 2, MaxUsers = 20 };
            db.SaaSClients.Add(client);
            db.SaaSPlans.Add(plan);
            var existingCompany = new Company { Name = "Existing Subscribed Co", Code = "EXSUB1", SaaSClientId = client.Id };
            var newCompany = new Company { Name = "New Co", Code = "NEWCO1", SaaSClientId = client.Id };
            db.Companies.AddRange(existingCompany, newCompany);
            await db.SaveChangesAsync();

            var subscription = new TenantSubscription
            {
                CompanyId = existingCompany.Id,
                SaaSClientId = client.Id,
                SaaSPlanId = plan.Id,
                PlanName = plan.PlanName,
                MaxCompanies = plan.MaxCompanies,
                MaxStoreGroups = plan.MaxStoreGroups,
                MaxStores = plan.MaxStores,
                MaxUsers = plan.MaxUsers,
                ValidFrom = DateTime.UtcNow.AddDays(-1),
                ValidTo = DateTime.UtcNow.AddDays(30),
                IsActive = true
            };
            db.TenantSubscriptions.Add(subscription);
            await db.SaveChangesAsync();

            createdClientIds.Add(client.Id);
            createdCompanyIds.AddRange([existingCompany.Id, newCompany.Id]);
            createdSubscriptionIds.Add(subscription.Id);

            var service = new SaaSValidationService(DefaultOptions());
            var error = await service.ValidateTokenActivationAsync(db, client.Id, newCompany.Id, plan, CancellationToken.None);

            Assert.NotNull(error);
            Assert.Contains("1 company/companies", error);
        }
        finally
        {
            await CleanupSubscriptionsAsync(createdSubscriptionIds);
            await CleanupAsync(createdCompanyIds, createdClientIds, [], []);
        }
    }

    private static IOptions<SaaSOptions> DefaultOptions() => Options.Create(new SaaSOptions());

    private static GarmetixDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<GarmetixDbContext>()
            .UseNpgsql(PostgresTestDatabase.ConnectionString)
            .Options;
        return new GarmetixDbContext(options);
    }

    private static async Task CleanupStoresAsync(IReadOnlyCollection<Guid> ids)
    {
        if (ids.Count == 0) return;
        await using var db = CreateDbContext();
        await db.Stores.IgnoreQueryFilters().Where(item => ids.Contains(item.Id)).ExecuteDeleteAsync();
    }

    private static async Task CleanupStoreGroupsAsync(IReadOnlyCollection<Guid> ids)
    {
        if (ids.Count == 0) return;
        await using var db = CreateDbContext();
        await db.StoreGroups.IgnoreQueryFilters().Where(item => ids.Contains(item.Id)).ExecuteDeleteAsync();
    }

    private static async Task CleanupSubscriptionsAsync(IReadOnlyCollection<Guid> ids)
    {
        if (ids.Count == 0) return;
        await using var db = CreateDbContext();
        await db.TenantSubscriptions.IgnoreQueryFilters().Where(item => ids.Contains(item.Id)).ExecuteDeleteAsync();
    }

    private static async Task CleanupAsync(
        IReadOnlyCollection<Guid> companyIds,
        IReadOnlyCollection<Guid> clientIds,
        IReadOnlyCollection<Guid> planIds,
        IReadOnlyCollection<Guid> tokenIds)
    {
        await using var db = CreateDbContext();
        if (companyIds.Count > 0)
        {
            await db.Companies.IgnoreQueryFilters().Where(item => companyIds.Contains(item.Id)).ExecuteDeleteAsync();
        }

        if (tokenIds.Count > 0)
        {
            await db.SaaSTokens.IgnoreQueryFilters().Where(item => tokenIds.Contains(item.Id)).ExecuteDeleteAsync();
        }

        if (planIds.Count > 0)
        {
            await db.SaaSPlans.IgnoreQueryFilters().Where(item => planIds.Contains(item.Id)).ExecuteDeleteAsync();
        }

        if (clientIds.Count > 0)
        {
            await db.SaaSClients.IgnoreQueryFilters().Where(item => clientIds.Contains(item.Id)).ExecuteDeleteAsync();
        }
    }
}
