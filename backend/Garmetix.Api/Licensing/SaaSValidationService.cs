using Garmetix.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Garmetix.Api.Licensing;

public class SaaSValidationService(GarmetixDbContext db)
{
    private const int TrialMaxCompanies = 1;
    private const int TrialMaxStoreGroups = 1;
    private const int TrialMaxStores = 2;
    private const int TrialMaxUsers = 20;

    public async Task<string?> EnsureCanAddCompanyAsync(Guid? saasClientId = null, CancellationToken cancellationToken = default)
    {
        int maxCompanies = TrialMaxCompanies;

        if (saasClientId.HasValue)
        {
            // Find the most generous active token for this client
            var token = await db.SaaSTokens
                .Include(t => t.SaaSPlan)
                .Where(t => t.SaaSClientId == saasClientId.Value && t.IsActivated && t.ExpiresAt > DateTime.UtcNow)
                .OrderByDescending(t => t.SaaSPlan!.MaxCompanies)
                .FirstOrDefaultAsync(cancellationToken);

            if (token?.SaaSPlan != null)
            {
                maxCompanies = token.SaaSPlan.MaxCompanies;
            }
            
            var clientCompanyCount = await db.Companies.CountAsync(c => c.SaaSClientId == saasClientId.Value && !c.Deleted, cancellationToken);
            if (clientCompanyCount >= maxCompanies)
            {
                return $"SaaS limit reached: Maximum {maxCompanies} company (tenant) allowed for this client.";
            }
        }
        else
        {
            var companyCount = await db.Companies.CountAsync(c => !c.Deleted, cancellationToken);
            if (companyCount >= TrialMaxCompanies)
            {
                return $"SaaS limit reached: Maximum {TrialMaxCompanies} company (tenant) allowed in this trial environment. Please provide a client token.";
            }
        }
        return null;
    }

    public async Task<string?> EnsureCanAddStoreGroupAsync(Guid companyId, CancellationToken cancellationToken)
    {
        var subscription = await db.TenantSubscriptions.FirstOrDefaultAsync(s => s.CompanyId == companyId, cancellationToken);
        if (subscription != null && (!subscription.IsActive || subscription.ValidTo < DateTime.UtcNow))
            return "Your SaaS subscription has expired. Cannot add new store groups.";

        int maxStoreGroups = subscription != null ? int.MaxValue : TrialMaxStoreGroups;
        var storeGroupCount = await db.StoreGroups.CountAsync(sg => sg.CompanyId == companyId && sg.Active && !sg.Deleted, cancellationToken);
        
        if (storeGroupCount >= maxStoreGroups)
            return $"SaaS limit reached: Maximum {maxStoreGroups} store group(s) allowed.";
        return null;
    }

    public async Task<string?> EnsureCanAddStoreAsync(Guid companyId, CancellationToken cancellationToken)
    {
        var subscription = await db.TenantSubscriptions.FirstOrDefaultAsync(s => s.CompanyId == companyId, cancellationToken);
        if (subscription != null && (!subscription.IsActive || subscription.ValidTo < DateTime.UtcNow))
        {
            return "Your SaaS subscription has expired. Cannot add new stores.";
        }

        int maxStores = subscription?.MaxStores ?? TrialMaxStores;
        var storeCount = await db.Stores.CountAsync(s => s.CompanyId == companyId && s.Active && !s.Deleted, cancellationToken);
        if (storeCount >= maxStores)
        {
            return $"SaaS limit reached: Your current plan allows a maximum of {maxStores} store(s). Please upgrade to add more.";
        }

        return null;
    }

    public async Task<string?> EnsureCanAddUserAsync(Guid companyId, CancellationToken cancellationToken)
    {
        var subscription = await db.TenantSubscriptions.FirstOrDefaultAsync(s => s.CompanyId == companyId, cancellationToken);
        if (subscription != null && (!subscription.IsActive || subscription.ValidTo < DateTime.UtcNow))
        {
            return "Your SaaS subscription has expired. Cannot add new users.";
        }

        int maxUsers = subscription?.MaxUsers ?? TrialMaxUsers;
        var userCount = await db.Users.CountAsync(u => u.CompanyId == companyId && u.IsActive && !u.IsSuperAdmin, cancellationToken);
        if (userCount >= maxUsers)
        {
            return $"SaaS limit reached: Your current plan allows a maximum of {maxUsers} login user(s). Please upgrade to add more.";
        }

        return null;
    }

    public async Task<string?> EnsureCanAddEmployeeAsync(Guid companyId, CancellationToken cancellationToken)
    {
        var subscription = await db.TenantSubscriptions.FirstOrDefaultAsync(s => s.CompanyId == companyId, cancellationToken);
        if (subscription != null && (!subscription.IsActive || subscription.ValidTo < DateTime.UtcNow))
        {
            return "Your SaaS subscription has expired. Cannot add new employees.";
        }

        int maxUsers = subscription?.MaxUsers ?? TrialMaxUsers;
        // We count Working employees as active
        var employeeCount = await db.Employees.CountAsync(e => e.CompanyId == companyId && e.Working && !e.Deleted, cancellationToken);
        if (employeeCount >= maxUsers)
        {
            return $"SaaS limit reached: Your current plan allows a maximum of {maxUsers} employee(s). Please upgrade to add more.";
        }

        return null;
    }
}
