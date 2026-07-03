using Garmetix.Core.Enums;
using Garmetix.Core.Models.Base;

namespace Garmetix.Core.Models.Accounting;

public sealed class CashVoucherConversion : StoreBase
{
    public string Direction { get; set; } = string.Empty;
    public Guid CashVoucherId { get; set; }
    public Guid VoucherId { get; set; }
    public string CashVoucherNumber { get; set; } = string.Empty;
    public string VoucherNumber { get; set; } = string.Empty;
    public VoucherType VoucherType { get; set; }
    public decimal Amount { get; set; }
    public string PartyName { get; set; } = string.Empty;
    public string Particulars { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
    public Guid ConvertedByUserId { get; set; }
    public string ConvertedByUserName { get; set; } = string.Empty;
    public DateTime ConvertedAt { get; set; }
}
