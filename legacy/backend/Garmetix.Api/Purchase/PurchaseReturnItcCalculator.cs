namespace Garmetix.Api.Purchase;

public sealed record PurchaseReturnTaxSource(
    decimal BilledQuantity,
    decimal TaxableAmount,
    decimal TaxAmount,
    decimal CgstAmount,
    decimal SgstAmount,
    decimal IgstAmount,
    decimal DiscountAmount);

public sealed record PurchaseReturnTaxResult(
    decimal Ratio,
    decimal TaxableAmount,
    decimal TaxAmount,
    decimal CgstAmount,
    decimal SgstAmount,
    decimal IgstAmount,
    decimal DiscountAmount,
    decimal ReturnAmount);

public static class PurchaseReturnItcCalculator
{
    public static PurchaseReturnTaxResult Calculate(PurchaseReturnTaxSource source, decimal returnedQuantity)
    {
        if (source.BilledQuantity <= 0)
        {
            throw new ArgumentException("Purchased quantity must be greater than zero.");
        }

        if (returnedQuantity <= 0 || returnedQuantity > source.BilledQuantity)
        {
            throw new ArgumentException("Returned quantity must be greater than zero and cannot exceed purchased quantity.");
        }

        var ratio = returnedQuantity / source.BilledQuantity;
        var taxable = Round(source.TaxableAmount * ratio);
        var tax = Round(source.TaxAmount * ratio);
        var cgst = Round(source.CgstAmount * ratio);
        var sgst = Round(source.SgstAmount * ratio);
        var igst = Round(source.IgstAmount * ratio);
        var componentDifference = tax - (cgst + sgst + igst);

        if (componentDifference != 0)
        {
            if (source.IgstAmount != 0)
            {
                igst = Round(igst + componentDifference);
            }
            else if (source.CgstAmount >= source.SgstAmount)
            {
                cgst = Round(cgst + componentDifference);
            }
            else
            {
                sgst = Round(sgst + componentDifference);
            }
        }

        return new PurchaseReturnTaxResult(
            ratio,
            taxable,
            tax,
            cgst,
            sgst,
            igst,
            Round(source.DiscountAmount * ratio),
            Round(taxable + tax));
    }

    private static decimal Round(decimal value) => Math.Round(value, 2, MidpointRounding.AwayFromZero);
}
