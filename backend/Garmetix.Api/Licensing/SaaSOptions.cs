namespace Garmetix.Api.Licensing;

/// <summary>
/// Trial-mode default quotas and the subscription-expiry enforcement switch for the SaaS Manager
/// module. Every value here only ever applies to a Company that has been explicitly linked to a
/// SaaSClient (Company.SaaSClientId is set) - an un-linked company (every company that existed before
/// this module, and any company nobody has deliberately brought into the SaaS-client relationship)
/// is never touched by any of this, no matter what these settings say.
/// </summary>
public sealed class SaaSOptions
{
    /// <summary>Master switch for blocking API access once a linked company's subscription has actually expired. Off by default - same "never surprise an existing install" convention as LicenseOptions.EnforcementEnabled.</summary>
    public bool SubscriptionEnforcementEnabled { get; set; } = false;

    public int TrialMaxCompanies { get; set; } = 1;
    public int TrialMaxStoreGroups { get; set; } = 1;
    public int TrialMaxStores { get; set; } = 2;
    public int TrialMaxUsers { get; set; } = 20;
}
