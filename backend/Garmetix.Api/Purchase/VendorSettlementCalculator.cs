namespace Garmetix.Api.Purchase;

public sealed record VendorSettlementAllocationInput(Guid PurchaseInvoiceId, decimal Amount, decimal OutstandingAmount);

public sealed record VendorSettlementPlan(
    decimal AvailableAmount,
    decimal AdjustedAmount,
    decimal RefundAmount,
    decimal TotalAmount,
    string SettlementType);

public static class VendorSettlementCalculator
{
    public static VendorSettlementPlan Build(
        decimal noteAmount,
        decimal previouslySettledAmount,
        decimal refundAmount,
        IReadOnlyCollection<VendorSettlementAllocationInput> allocations)
    {
        var availableAmount = Money(Math.Max(noteAmount - previouslySettledAmount, 0));
        var cleanRefund = Money(refundAmount);
        if (cleanRefund < 0)
        {
            throw new ArgumentException("Refund amount cannot be negative.");
        }

        if (allocations.GroupBy(item => item.PurchaseInvoiceId).Any(group => group.Count() > 1))
        {
            throw new ArgumentException("A purchase invoice can be selected only once.");
        }

        foreach (var allocation in allocations)
        {
            if (allocation.PurchaseInvoiceId == Guid.Empty || allocation.Amount <= 0)
            {
                throw new ArgumentException("Each adjustment must have a purchase invoice and an amount greater than zero.");
            }

            if (Money(allocation.Amount) > Money(allocation.OutstandingAmount))
            {
                throw new ArgumentException("An adjustment cannot exceed the selected purchase invoice outstanding amount.");
            }
        }

        var adjustedAmount = Money(allocations.Sum(item => item.Amount));
        var totalAmount = Money(adjustedAmount + cleanRefund);
        if (totalAmount <= 0)
        {
            throw new ArgumentException("Enter a refund or allocate the debit note to at least one outstanding purchase.");
        }

        if (totalAmount > availableAmount)
        {
            throw new ArgumentException("Settlement total cannot exceed the available debit-note amount.");
        }

        var settlementType = adjustedAmount > 0 && cleanRefund > 0
            ? "Mixed"
            : cleanRefund > 0 ? "Refund" : "Adjustment";

        return new VendorSettlementPlan(availableAmount, adjustedAmount, cleanRefund, totalAmount, settlementType);
    }

    private static decimal Money(decimal value)
        => Math.Round(value, 2, MidpointRounding.AwayFromZero);
}
