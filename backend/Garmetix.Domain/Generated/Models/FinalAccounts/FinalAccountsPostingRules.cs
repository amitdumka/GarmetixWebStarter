using System.ComponentModel.DataAnnotations;
using Garmetix.Core.Models.Base;

namespace Garmetix.Core.Models.FinalAccounts;

public sealed class FinalAccountsPostingRule : BaseEntity
{
    [Display(Name = "Company")] public Guid? CompanyId { get; set; }
    [Display(Name = "Store Group")] public Guid? StoreGroupId { get; set; }
    [Display(Name = "Store")] public Guid? StoreId { get; set; }
    [Display(Name = "Source Type")] public string SourceType { get; set; } = string.Empty;
    [Display(Name = "Rule Code")] public string RuleCode { get; set; } = string.Empty;
    [Display(Name = "Version")] public string Version { get; set; } = "GarmentRetail.v1";
    [Display(Name = "Name")] public string Name { get; set; } = string.Empty;
    [Display(Name = "Description")] public string? Description { get; set; }
    [Display(Name = "Effective From")] public DateTime EffectiveFrom { get; set; } = DateTime.UtcNow.Date;
    [Display(Name = "System Rule")] public bool IsSystem { get; set; } = true;
    [Display(Name = "Active")] public bool IsActive { get; set; } = true;
    [Display(Name = "Revision")] public int Revision { get; set; }
    [Display(Name = "Created By")] public string? CreatedBy { get; set; }
    [Display(Name = "Updated By")] public string? UpdatedBy { get; set; }
}

public sealed class FinalAccountsPostingRuleLine : BaseEntity
{
    [Display(Name = "Company")] public Guid? CompanyId { get; set; }
    [Display(Name = "Store Group")] public Guid? StoreGroupId { get; set; }
    [Display(Name = "Store")] public Guid? StoreId { get; set; }
    [Display(Name = "Posting Rule")] public Guid PostingRuleId { get; set; }
    [Display(Name = "Mapping Key")] public string MappingKey { get; set; } = string.Empty;
    [Display(Name = "Display Name")] public string DisplayName { get; set; } = string.Empty;
    [Display(Name = "Category")] public string MappingCategory { get; set; } = string.Empty;
    [Display(Name = "Direction")] public string Direction { get; set; } = string.Empty;
    [Display(Name = "Expected Account Type")] public string? ExpectedAccountType { get; set; }
    [Display(Name = "Required")] public bool IsRequired { get; set; } = true;
    [Display(Name = "Allow Control Account")] public bool AllowControlAccount { get; set; } = true;
    [Display(Name = "Sort Order")] public int SortOrder { get; set; }
    [Display(Name = "Notes")] public string? Notes { get; set; }
    [Display(Name = "Revision")] public int Revision { get; set; }
    [Display(Name = "Created By")] public string? CreatedBy { get; set; }
    [Display(Name = "Updated By")] public string? UpdatedBy { get; set; }
}
