using Garmetix.Api.Communication;
using Garmetix.Api.Tests.Infrastructure;
using Garmetix.Core.Models.Communication;
using Garmetix.Infrastructure.Data;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Garmetix.Api.Tests.Communication;

/// <summary>
/// Postgres-only (skipped without GARMETIX_TEST_POSTGRES, same as every other Postgres-gated
/// test here) - verifies the Store -> Company -> Global -> LocalMasterOnly resolution order
/// documented in docs/communication-mail-architecture.md section 6.
/// </summary>
public sealed class EmailProviderResolutionServiceTests
{
    [PostgresFact]
    public async Task ResolveAsync_PrefersStoreScopedProviderOverCompanyAndGlobal()
    {
        var companyId = Guid.NewGuid();
        var storeId = Guid.NewGuid();
        var createdIds = new List<Guid>();

        try
        {
            await using var db = CreateDbContext();
            // Deliberately no un-scoped "global" row here - only company/store-scoped rows are
            // needed to prove store beats company, and a company-null row could otherwise
            // collide with other tests' resolution if this suite is ever run in parallel.
            var companyProvider = new EmailProviderConfiguration { ProviderType = EmailCatalog.ProviderTypes.Smtp, ProviderName = "Company", FromEmail = "b@x.com", CompanyId = companyId, Priority = 100 };
            var storeProvider = new EmailProviderConfiguration { ProviderType = EmailCatalog.ProviderTypes.Smtp, ProviderName = "Store", FromEmail = "c@x.com", CompanyId = companyId, StoreId = storeId, Priority = 100 };
            db.EmailProviderConfigurations.AddRange(companyProvider, storeProvider);
            await db.SaveChangesAsync();
            createdIds.AddRange([companyProvider.Id, storeProvider.Id]);

            var resolver = new EmailProviderResolutionService(db, CreateProtector());
            var resolved = await resolver.ResolveAsync(companyId, null, storeId, CancellationToken.None);

            Assert.Equal(storeProvider.Id, resolved.Provider.Id);
        }
        finally
        {
            await CleanupAsync(createdIds);
        }
    }

    [PostgresFact]
    public async Task ResolveAsync_FallsBackToCompanyProviderWhenNoStoreOverrideExists()
    {
        var companyId = Guid.NewGuid();
        var storeId = Guid.NewGuid();
        var createdIds = new List<Guid>();

        try
        {
            await using var db = CreateDbContext();
            var companyProvider = new EmailProviderConfiguration { ProviderType = EmailCatalog.ProviderTypes.Smtp, ProviderName = "Company", FromEmail = "b@x.com", CompanyId = companyId, Priority = 100 };
            db.EmailProviderConfigurations.Add(companyProvider);
            await db.SaveChangesAsync();
            createdIds.Add(companyProvider.Id);

            var resolver = new EmailProviderResolutionService(db, CreateProtector());
            var resolved = await resolver.ResolveAsync(companyId, null, storeId, CancellationToken.None);

            Assert.Equal(companyProvider.Id, resolved.Provider.Id);
        }
        finally
        {
            await CleanupAsync(createdIds);
        }
    }

    [PostgresFact]
    public async Task ResolveAsync_FallsBackToLocalMasterOnlyWhenNoExternalProviderMatches()
    {
        var companyId = Guid.NewGuid();
        var createdIds = new List<Guid>();

        try
        {
            await using var db = CreateDbContext();
            var localMaster = new EmailProviderConfiguration { ProviderType = EmailCatalog.ProviderTypes.LocalMasterOnly, ProviderName = "Local Master", FromEmail = "a@x.com", Priority = 999 };
            db.EmailProviderConfigurations.Add(localMaster);
            await db.SaveChangesAsync();
            createdIds.Add(localMaster.Id);

            var resolver = new EmailProviderResolutionService(db, CreateProtector());
            var resolved = await resolver.ResolveAsync(companyId, null, null, CancellationToken.None);

            Assert.Equal(EmailCatalog.ProviderTypes.LocalMasterOnly, resolved.Provider.ProviderType);
        }
        finally
        {
            await CleanupAsync(createdIds);
        }
    }

    private static EmailCredentialProtector CreateProtector()
    {
        var services = new ServiceCollection();
        services.AddDataProtection();
        var provider = services.BuildServiceProvider().GetRequiredService<IDataProtectionProvider>();
        return new EmailCredentialProtector(provider);
    }

    private static GarmetixDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<GarmetixDbContext>()
            .UseNpgsql(PostgresTestDatabase.ConnectionString)
            .Options;
        return new GarmetixDbContext(options);
    }

    private static async Task CleanupAsync(IReadOnlyCollection<Guid> providerIds)
    {
        if (providerIds.Count == 0)
        {
            return;
        }

        await using var db = CreateDbContext();
        await db.EmailProviderConfigurations
            .IgnoreQueryFilters()
            .Where(p => providerIds.Contains(p.Id))
            .ExecuteDeleteAsync();
    }
}
