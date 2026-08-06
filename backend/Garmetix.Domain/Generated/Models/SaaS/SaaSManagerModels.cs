using Garmetix.Core.Models.Base;
using System.ComponentModel.DataAnnotations;

namespace Garmetix.Core.Models.SaaS;

/// <summary>
/// An outside client business paying to run Garmetix as a hosted product. Distinct from
/// Garmetix.Core.Models.Stores.Company (a legal-entity master inside one business's own books) -
/// one SaaSClient can hold a license covering one or more Companies via TenantSubscription.
/// </summary>
public class SaaSClient : BaseEntity
{
    public SaaSClient()
    {
        ClientCode = string.Empty;
        Name = string.Empty;
        Country = "India";
    }

    [Display(Name = "Client Code")] public string ClientCode { get; set; }
    [Display(Name = "Name")] public string Name { get; set; }
    [Display(Name = "Email")] public string? Email { get; set; }
    [Display(Name = "Mobile")] public string? Mobile { get; set; }
    [Display(Name = "Address")] public string? Address { get; set; }
    [Display(Name = "City")] public string? City { get; set; }
    [Display(Name = "State")] public string? State { get; set; }
    [Display(Name = "Country")] public string Country { get; set; }
    [Display(Name = "Zip Code")] public string? ZipCode { get; set; }
    [Display(Name = "GSTIN")] public string? Gstin { get; set; }
    [Display(Name = "Active")] public bool Active { get; set; } = true;
}

/// <summary>A license plan a SaaSClient can be issued (Basic/Pro/Ultimate style tiers with quota limits).</summary>
public class SaaSPlan : BaseEntity
{
    public SaaSPlan()
    {
        PlanName = string.Empty;
        IncludedModulesCsv = string.Empty;
    }

    [Display(Name = "Plan Name")] public string PlanName { get; set; }
    [Display(Name = "Max Companies")] public int MaxCompanies { get; set; } = 1;
    [Display(Name = "Max Store Groups")] public int MaxStoreGroups { get; set; } = 1;
    [Display(Name = "Max Stores")] public int MaxStores { get; set; } = 2;
    [Display(Name = "Max Users")] public int MaxUsers { get; set; } = 20;
    [Display(Name = "Included Modules")] public string IncludedModulesCsv { get; set; }
    [Display(Name = "Active")] public bool Active { get; set; } = true;
}

/// <summary>
/// An offline-generated license token for a Client+Plan pair, meant to be sent via WhatsApp/email and
/// activated once by the client against one of their Companies (see TenantSubscription).
/// </summary>
public class SaaSToken : BaseEntity
{
    public SaaSToken()
    {
        TokenString = string.Empty;
    }

    [Display(Name = "Token")] public string TokenString { get; set; }
    [Display(Name = "Client Id")] public Guid SaaSClientId { get; set; }
    [Display(Name = "Plan Id")] public Guid SaaSPlanId { get; set; }
    [Display(Name = "Validity Days")] public int ValidityDays { get; set; } = 365;
    [Display(Name = "Expires At")] public DateTime ExpiresAt { get; set; }
    [Display(Name = "Is Activated")] public bool IsActivated { get; set; }
    [Display(Name = "Activated At")] public DateTime? ActivatedAtUtc { get; set; }
    [Display(Name = "Activated Company Id")] public Guid? ActivatedCompanyId { get; set; }
    [Display(Name = "Notes")] public string? Notes { get; set; }
}

/// <summary>
/// The result of activating a SaaSToken against one of the client's own Companies - a snapshot of the
/// plan's quotas at activation time, so a later plan edit never silently changes an already-activated
/// tenant's limits. Absence of an active row for a Company means unlimited/trial (never enforced),
/// matching this module's opt-in-only enforcement design.
/// </summary>
public class TenantSubscription : BaseEntity
{
    public TenantSubscription()
    {
        PlanName = string.Empty;
    }

    [Display(Name = "Company Id")] public Guid CompanyId { get; set; }
    [Display(Name = "Client Id")] public Guid SaaSClientId { get; set; }
    [Display(Name = "Plan Id")] public Guid SaaSPlanId { get; set; }
    [Display(Name = "Token Id")] public Guid? SaaSTokenId { get; set; }
    [Display(Name = "Plan Name")] public string PlanName { get; set; }
    [Display(Name = "Max Companies")] public int MaxCompanies { get; set; }
    [Display(Name = "Max Store Groups")] public int MaxStoreGroups { get; set; }
    [Display(Name = "Max Stores")] public int MaxStores { get; set; }
    [Display(Name = "Max Users")] public int MaxUsers { get; set; }
    [Display(Name = "Valid From")] public DateTime ValidFrom { get; set; }
    [Display(Name = "Valid To")] public DateTime ValidTo { get; set; }
    [Display(Name = "Is Active")] public bool IsActive { get; set; } = true;
}
