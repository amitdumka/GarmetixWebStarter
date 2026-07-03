using Garmetix.Core.Models.Base;
using System.ComponentModel.DataAnnotations;

namespace Garmetix.Core.Models.GstReturns;

public class GstReturnDraft : CompanyBase
{
    public GstReturnDraft()
    {
        Form = string.Empty;
        Gstin = string.Empty;
        ReturnPeriod = string.Empty;
        Title = string.Empty;
        Status = "Draft";
        PayloadJson = "{}";
        LastPreviewIssuesJson = "[]";
        CreatedByUserName = string.Empty;
        UpdatedByUserName = string.Empty;
    }

    [Display(Name = "Form")] public string Form { get; set; }
    [Display(Name = "GSTIN")] public string Gstin { get; set; }
    [Display(Name = "Return Period")] public string ReturnPeriod { get; set; }
    [Display(Name = "Title")] public string Title { get; set; }
    [Display(Name = "Status")] public string Status { get; set; }
    [Display(Name = "Payload JSON")] public string PayloadJson { get; set; }
    [Display(Name = "Last Preview Issues JSON")] public string LastPreviewIssuesJson { get; set; }
    [Display(Name = "Row Count")] public int RowCount { get; set; }
    [Display(Name = "Taxable Value")] public decimal TaxableValue { get; set; }
    [Display(Name = "Integrated Tax")] public decimal IntegratedTax { get; set; }
    [Display(Name = "Central Tax")] public decimal CentralTax { get; set; }
    [Display(Name = "State Tax")] public decimal StateTax { get; set; }
    [Display(Name = "Cess")] public decimal Cess { get; set; }
    [Display(Name = "Created By User Id")] public Guid? CreatedByUserId { get; set; }
    [Display(Name = "Created By User Name")] public string CreatedByUserName { get; set; }
    [Display(Name = "Updated By User Id")] public Guid? UpdatedByUserId { get; set; }
    [Display(Name = "Updated By User Name")] public string UpdatedByUserName { get; set; }
    [Display(Name = "Filed At")] public DateTime? FiledAt { get; set; }
    [Display(Name = "Locked At")] public DateTime? LockedAt { get; set; }
}
