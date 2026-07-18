using Garmetix.Core.Models.Communication;
using Garmetix.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Garmetix.Api.Communication;

public sealed record ResolvedEmailProvider(EmailProviderConfiguration Provider, IReadOnlyDictionary<string, string> Credentials);

/// <summary>
/// Resolves which EmailProviderConfiguration applies for a given scope: Store override wins
/// over Company override wins over a tenant-wide default (CompanyId/StoreGroupId/StoreId all
/// null) wins over the seeded LocalMasterOnly fallback - the same Store -> Company -> Global
/// -> LocalMasterOnly priority chain the GST module's provider registry documents, made
/// concrete here as an explicit specificity-then-priority ordering. Never lets a caller pick
/// a provider outside its authorized scope - callers only ever pass the scope they are
/// already authorized for (via WorkspaceScope), never a provider id directly.
/// </summary>
public sealed class EmailProviderResolutionService(GarmetixDbContext db, EmailCredentialProtector protector)
{
    public async Task<ResolvedEmailProvider> ResolveAsync(Guid? companyId, Guid? storeGroupId, Guid? storeId, CancellationToken cancellationToken)
    {
        var candidates = await db.EmailProviderConfigurations.AsNoTracking()
            .Where(p => p.IsEnabled)
            .Where(p =>
                (p.StoreId == null || p.StoreId == storeId) &&
                (p.StoreGroupId == null || p.StoreGroupId == storeGroupId) &&
                (p.CompanyId == null || p.CompanyId == companyId))
            .ToListAsync(cancellationToken);

        var best = candidates
            .Where(p => p.ProviderType != EmailCatalog.ProviderTypes.LocalMasterOnly)
            .OrderByDescending(Specificity)
            .ThenBy(p => p.Priority)
            .FirstOrDefault();

        best ??= candidates
            .Where(p => p.ProviderType == EmailCatalog.ProviderTypes.LocalMasterOnly)
            .OrderBy(p => p.Priority)
            .FirstOrDefault();

        if (best is null)
        {
            // No provider row at all for this scope (not even the seeded LocalMasterOnly one) -
            // synthesize an in-memory fallback rather than throwing, matching the
            // "module works with zero external config out of the box" guarantee.
            best = new EmailProviderConfiguration
            {
                ProviderName = "Local Master (unseeded)",
                ProviderType = EmailCatalog.ProviderTypes.LocalMasterOnly,
                FromEmail = "no-reply@garmetix.local",
                FromName = "Garmetix",
            };
            return new ResolvedEmailProvider(best, new Dictionary<string, string>());
        }

        var credentialRows = await db.EmailProviderCredentials.AsNoTracking()
            .Where(c => c.ProviderId == best.Id)
            .ToListAsync(cancellationToken);

        var credentials = new Dictionary<string, string>();
        foreach (var row in credentialRows)
        {
            var decrypted = protector.Unprotect(row.EncryptedValue);
            if (decrypted is not null)
            {
                credentials[row.CredentialKey] = decrypted;
            }
        }

        return new ResolvedEmailProvider(best, credentials);
    }

    private static int Specificity(EmailProviderConfiguration provider)
    {
        if (provider.StoreId is not null) return 3;
        if (provider.StoreGroupId is not null) return 2;
        if (provider.CompanyId is not null) return 1;
        return 0;
    }
}
