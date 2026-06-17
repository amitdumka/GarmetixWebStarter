using Garmetix.Core.Models.Base;
using System.ComponentModel.DataAnnotations;

namespace Garmetix.Core.Models.Audit;

public class AuditLogEntry : BaseEntity
{
    [Display(Name = "Occurred At")] public DateTime OccurredAt { get; set; } = DateTime.Now;
    [Display(Name = "Action")] public string Action { get; set; } = string.Empty;
    [Display(Name = "Module")] public string Module { get; set; } = string.Empty;
    [Display(Name = "Entity Name")] public string EntityName { get; set; } = string.Empty;
    [Display(Name = "Entity Display Name")] public string EntityDisplayName { get; set; } = string.Empty;
    [Display(Name = "Entity Id")] public Guid EntityId { get; set; }
    [Display(Name = "Reference")] public string Reference { get; set; } = string.Empty;
    [Display(Name = "Company")] public Guid? CompanyId { get; set; }
    [Display(Name = "Store Group")] public Guid? StoreGroupId { get; set; }
    [Display(Name = "Store")] public Guid? StoreId { get; set; }
    [Display(Name = "User")] public Guid? UserId { get; set; }
    [Display(Name = "User Name")] public string? UserName { get; set; }
    [Display(Name = "Source")] public string Source { get; set; } = "SaveChanges";
    [Display(Name = "Request Method")] public string? RequestMethod { get; set; }
    [Display(Name = "Request Path")] public string? RequestPath { get; set; }
    [Display(Name = "IP Address")] public string? IpAddress { get; set; }
    [Display(Name = "Reason")] public string? Reason { get; set; }
    [Display(Name = "Before JSON")] public string? BeforeJson { get; set; }
    [Display(Name = "After JSON")] public string? AfterJson { get; set; }
    [Display(Name = "Changes JSON")] public string? ChangesJson { get; set; }
    [Display(Name = "Changed Field Count")] public int ChangedFieldCount { get; set; }
    [Display(Name = "Trace Identifier")] public string? TraceIdentifier { get; set; }
}
