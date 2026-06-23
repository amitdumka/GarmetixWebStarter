using Garmetix.Core.Models.Base;
using System.ComponentModel.DataAnnotations;

namespace Garmetix.Core.Models.GstReturns;

public class GstReturnAuditEntry : CompanyBase
{
    public GstReturnAuditEntry()
    {
        Form = string.Empty;
        ReturnPeriod = string.Empty;
        Gstin = string.Empty;
        Action = string.Empty;
        Summary = string.Empty;
        ActorName = string.Empty;
        DetailsJson = "{}";
    }

    [Display(Name = "Draft Id")] public Guid DraftId { get; set; }
    [Display(Name = "Form")] public string Form { get; set; }
    [Display(Name = "Return Period")] public string ReturnPeriod { get; set; }
    [Display(Name = "GSTIN")] public string Gstin { get; set; }
    [Display(Name = "Action")] public string Action { get; set; }
    [Display(Name = "Summary")] public string Summary { get; set; }
    [Display(Name = "Actor User Id")] public Guid? ActorUserId { get; set; }
    [Display(Name = "Actor Name")] public string ActorName { get; set; }
    [Display(Name = "Details JSON")] public string DetailsJson { get; set; }
}
