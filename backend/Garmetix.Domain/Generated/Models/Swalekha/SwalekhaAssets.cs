using System.ComponentModel.DataAnnotations;

namespace Garmetix.Core.Models.Swalekha;

public enum SwalekhaAssetCategory
{
    Immovable,
    Movable
}

/// <summary>
/// General personal property register - distinct from PersonalFin_10's SwalekhaOtherAsset
/// (which is scoped narrowly to PPF/EPF/NPS/Gold-as-investment snapshots). This tracks the
/// physical things an Owner holds: Immovable (house/flat/land) and Movable (gold, vehicles,
/// luxury items, electronics, etc). AssetSubType is deliberately free-text with a UI-side
/// suggestion list rather than a closed enum, matching this codebase's established pattern
/// for personal categories that don't have a fixed universe (see SwalekhaExpenseSheet.SheetType).
/// </summary>
public class SwalekhaAsset : SwalekhaOwnedEntity
{
    public SwalekhaAsset()
    {
        Name = string.Empty;
        AssetSubType = string.Empty;
    }

    [Display(Name = "Category")] public SwalekhaAssetCategory Category { get; set; }
    [Display(Name = "Type")] public string AssetSubType { get; set; }
    [Display(Name = "Name")] public string Name { get; set; }
    [Display(Name = "Purchase Value")] public decimal? PurchaseValue { get; set; }
    [Display(Name = "Purchase Date")] public DateTime? PurchaseDate { get; set; }
    [Display(Name = "Current Value")] public decimal CurrentValue { get; set; }
    [Display(Name = "As Of Date")] public DateTime AsOfDate { get; set; }
    [Display(Name = "Location")] public string? Location { get; set; }
    [Display(Name = "Is Active")] public bool IsActive { get; set; } = true;
    [Display(Name = "Notes")] public string? Notes { get; set; }
}
