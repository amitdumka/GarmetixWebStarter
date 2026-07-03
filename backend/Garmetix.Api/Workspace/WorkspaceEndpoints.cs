using Garmetix.Api.Auth;
using Garmetix.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Garmetix.Api.Workspace;

public static class WorkspaceEndpoints
{
    public static RouteGroupBuilder MapWorkspaceEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/workspace")
            .WithTags("Workspace")
            .RequireAuthorization();

        group.MapGet("/options", GetOptionsAsync);

        return group;
    }

    private static async Task<WorkspaceOptionsResponse> GetOptionsAsync(
        HttpContext context,
        GarmetixDbContext db,
        CancellationToken cancellationToken)
    {
        var claimCompanyId = WorkspaceScope.ClaimGuid(context, "companyId");
        var claimStoreGroupId = WorkspaceScope.ClaimGuid(context, "storeGroupId");
        var claimStoreId = WorkspaceScope.ClaimGuid(context, "storeId");

        var companies = await WorkspaceScope.ApplyTo(db.Companies.AsNoTracking(), context)
            .OrderBy(company => company.Name)
            .Select(company => new WorkspaceCompanyDto(
                company.Id,
                company.Name,
                company.Code ?? string.Empty,
                company.Active))
            .ToListAsync(cancellationToken);

        var companyIds = companies.Select(company => company.Id).ToArray();
        var restrictCompanies = companyIds.Length > 0 || claimCompanyId.HasValue;

        var storeGroups = await WorkspaceScope.ApplyTo(db.StoreGroups.AsNoTracking(), context)
            .Where(group => !restrictCompanies || companyIds.Contains(group.CompanyId))
            .OrderBy(group => group.Name)
            .Select(group => new WorkspaceStoreGroupDto(
                group.Id,
                group.CompanyId,
                group.Name,
                group.GroupCode ?? string.Empty,
                group.Active))
            .ToListAsync(cancellationToken);

        var storeGroupIds = storeGroups.Select(group => group.Id).ToArray();
        var restrictStoreGroups = storeGroupIds.Length > 0 || claimStoreGroupId.HasValue;

        var stores = await WorkspaceScope.ApplyTo(db.Stores.AsNoTracking(), context)
            .Where(store => !restrictCompanies || companyIds.Contains(store.CompanyId))
            .Where(store => !restrictStoreGroups || storeGroupIds.Contains(store.StoreGroupId))
            .OrderBy(store => store.Name)
            .Select(store => new WorkspaceStoreDto(
                store.Id,
                store.CompanyId,
                store.StoreGroupId,
                store.Name,
                store.StoreCode ?? string.Empty,
                store.Active))
            .ToListAsync(cancellationToken);

        var defaultStoreId = stores.Any(store => store.Id == claimStoreId)
            ? claimStoreId
            : stores.FirstOrDefault()?.Id;
        var defaultStore = stores.FirstOrDefault(store => store.Id == defaultStoreId);

        var defaultStoreGroupId = storeGroups.Any(group => group.Id == claimStoreGroupId)
            ? claimStoreGroupId
            : defaultStore?.StoreGroupId ?? storeGroups.FirstOrDefault()?.Id;

        var defaultCompanyId = companies.Any(company => company.Id == claimCompanyId)
            ? claimCompanyId
            : defaultStore?.CompanyId
              ?? storeGroups.FirstOrDefault(group => group.Id == defaultStoreGroupId)?.CompanyId
              ?? companies.FirstOrDefault()?.Id;

        return new WorkspaceOptionsResponse(
            companies,
            storeGroups,
            stores,
            defaultCompanyId == Guid.Empty ? null : defaultCompanyId,
            defaultStoreGroupId == Guid.Empty ? null : defaultStoreGroupId,
            defaultStoreId == Guid.Empty ? null : defaultStoreId,
            !WorkspaceScope.HasFullAccess(context) && claimCompanyId.HasValue,
            !WorkspaceScope.HasFullAccess(context) && claimStoreGroupId.HasValue,
            !WorkspaceScope.HasFullAccess(context) && claimStoreId.HasValue,
            WorkspaceScope.AppOperation(context));
    }
}
