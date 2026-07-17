using Garmetix.Core.Models.Base;
using System.ComponentModel.DataAnnotations;

namespace Garmetix.Core.Models.Swalekha;

public enum SwalekhaShareTransactionType
{
    Buy,
    Sell
}

public enum SwalekhaOtherAssetType
{
    PPF,
    EPF,
    NPS,
    Gold,
    Other
}

/// <summary>
/// A share/stock holding (PersonalFin_10 - Investments III). CurrentQuantity/TotalInvested/
/// RealizedPnL are running totals kept in lockstep with every SwalekhaShareTransaction, matching
/// the SwalekhaMutualFund pattern from PersonalFin_09 exactly - Buy debits the linked account and
/// grows the position, Sell credits it back, reduces TotalInvested at average cost per share, and
/// adds the realized gain/loss to RealizedPnL. CurrentPrice is manually updated (no live market
/// data provider in this stage).
/// </summary>
public class SwalekhaShareHolding : SwalekhaOwnedEntity
{
    public SwalekhaShareHolding()
    {
        Symbol = string.Empty;
    }

    [Display(Name = "Symbol")] public string Symbol { get; set; }
    [Display(Name = "Company Name")] public string? CompanyName { get; set; }
    [Display(Name = "Exchange")] public string? Exchange { get; set; }
    [Display(Name = "Demat Account")] public string? DematAccount { get; set; }
    [Display(Name = "Broker")] public string? Broker { get; set; }
    [Display(Name = "Account Id")] public Guid? AccountId { get; set; }
    [Display(Name = "Current Price")] public decimal? CurrentPrice { get; set; }
    [Display(Name = "Current Price Updated At")] public DateTime? CurrentPriceUpdatedAt { get; set; }
    [Display(Name = "Current Quantity")] public decimal CurrentQuantity { get; set; }
    [Display(Name = "Total Invested")] public decimal TotalInvested { get; set; }
    [Display(Name = "Realized PnL")] public decimal RealizedPnL { get; set; }
    [Display(Name = "Is Active")] public bool IsActive { get; set; } = true;
    [Display(Name = "Notes")] public string? Notes { get; set; }
}

/// <summary>
/// One buy/sell event against a SwalekhaShareHolding. Sell's contribution is removed from
/// TotalInvested at average cost per share (InvestedAmountRemoved), and the realized gain/loss on
/// that specific sale is recorded (RealizedPnLOnSale) - both needed so a later delete can reverse
/// the transaction's exact effect rather than re-deriving a ratio against values that may have
/// since changed, mirroring SwalekhaMutualFundTransaction's InvestedAmountRemoved from PersonalFin_09.
/// </summary>
public class SwalekhaShareTransaction : SwalekhaOwnedEntity
{
    public SwalekhaShareTransaction()
    {
        Narration = string.Empty;
    }

    [Display(Name = "Holding Id")] public Guid HoldingId { get; set; }
    [Display(Name = "Transaction Type")] public SwalekhaShareTransactionType TransactionType { get; set; }
    [Display(Name = "Transaction Date")] public DateTime TransactionDate { get; set; }
    [Display(Name = "Quantity")] public decimal Quantity { get; set; }
    [Display(Name = "Price Per Share")] public decimal PricePerShare { get; set; }
    [Display(Name = "Amount")] public decimal Amount { get; set; }
    [Display(Name = "Narration")] public string Narration { get; set; }
    [Display(Name = "Invested Amount Removed")] public decimal? InvestedAmountRemoved { get; set; }
    [Display(Name = "Realized PnL On Sale")] public decimal? RealizedPnLOnSale { get; set; }
}

/// <summary>
/// A simple asset snapshot (PersonalFin_10) for PPF/EPF/NPS/Gold and similar holdings the design
/// explicitly scoped as "simple asset entries" rather than full transaction-tracked positions -
/// just a current value the Owner updates periodically, contributing to Net Worth reporting later.
/// </summary>
public class SwalekhaOtherAsset : SwalekhaOwnedEntity
{
    public SwalekhaOtherAsset()
    {
        Name = string.Empty;
    }

    [Display(Name = "Asset Type")] public SwalekhaOtherAssetType AssetType { get; set; }
    [Display(Name = "Name")] public string Name { get; set; }
    [Display(Name = "Current Value")] public decimal CurrentValue { get; set; }
    [Display(Name = "As Of Date")] public DateTime AsOfDate { get; set; }
    [Display(Name = "Is Active")] public bool IsActive { get; set; } = true;
    [Display(Name = "Notes")] public string? Notes { get; set; }
}
