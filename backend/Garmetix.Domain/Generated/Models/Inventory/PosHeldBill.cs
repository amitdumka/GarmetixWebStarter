using Garmetix.Core.Models.Base;
using System.ComponentModel.DataAnnotations;

namespace Garmetix.Core.Models.Inventory;

public class PosHeldBill : StoreBase
{
    public PosHeldBill()
    {
        ClientHeldBillId = string.Empty;
        CustomerName = "Walk-in Customer";
        CustomerMobileNumber = string.Empty;
        Note = string.Empty;
        DraftJson = "{}";
        Status = "Held";
        HeldByUserName = string.Empty;
    }

    [MaxLength(80)] public string ClientHeldBillId { get; set; }
    public DateTime HeldAt { get; set; } = DateTime.Now;
    [MaxLength(160)] public string CustomerName { get; set; }
    [MaxLength(40)] public string CustomerMobileNumber { get; set; }
    public int ItemCount { get; set; }
    public decimal Quantity { get; set; }
    public decimal PayableTotal { get; set; }
    [MaxLength(500)] public string Note { get; set; }
    public string DraftJson { get; set; }
    [MaxLength(40)] public string Status { get; set; }
    public Guid? HeldByUserId { get; set; }
    [MaxLength(160)] public string HeldByUserName { get; set; }
    public DateTime? ResumedAt { get; set; }
}
