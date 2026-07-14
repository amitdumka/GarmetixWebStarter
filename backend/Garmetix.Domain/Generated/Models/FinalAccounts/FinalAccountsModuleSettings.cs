using System.ComponentModel.DataAnnotations;
using Garmetix.Core.Models.Base;

namespace Garmetix.Core.Models.FinalAccounts;

public sealed class FinalAccountsModuleSettings : BaseEntity
{
    [Display(Name = "Company")] public Guid? CompanyId { get; set; }
    [Display(Name = "Store Group")] public Guid? StoreGroupId { get; set; }
    [Display(Name = "Store")] public Guid? StoreId { get; set; }
    [Display(Name = "Enabled")] public bool Enabled { get; set; }
    [Display(Name = "Posting Mode")] public string PostingMode { get; set; } = "ManualSync";
    [Display(Name = "Statement Template")] public string StatementTemplate { get; set; } = "GarmentRetail.v1";
    [Display(Name = "Inventory Valuation Method")] public string InventoryValuationMethod { get; set; } = "WeightedAverage";
    [Display(Name = "Rounding Scale")] public int RoundingScale { get; set; } = 2;
    [Display(Name = "Allow Historical Backfill")] public bool AllowHistoricalBackfill { get; set; }
    [Display(Name = "Allow Tally Export")] public bool AllowTallyExport { get; set; }
    [Display(Name = "Allow Projections")] public bool AllowProjections { get; set; }
    [Display(Name = "Allow Period Reopen")] public bool AllowPeriodReopen { get; set; }
    [Display(Name = "Created By")] public string? CreatedBy { get; set; }
    [Display(Name = "Updated By")] public string? UpdatedBy { get; set; }
}
