namespace Garmetix.Api.Inventory;

public sealed record StockQuantityChange(
    decimal BeforeQuantity,
    decimal QuantityIn,
    decimal QuantityOut,
    decimal AfterQuantity,
    decimal Difference);

public static class StockOperationCalculator
{
    public static StockQuantityChange Adjustment(decimal currentQuantity, decimal quantity, bool increase)
    {
        ValidateCurrent(currentQuantity);
        ValidatePositive(quantity, "Adjustment quantity");
        if (!increase && quantity > currentQuantity)
        {
            throw new ArgumentException("Adjustment cannot reduce stock below zero.");
        }

        var quantityIn = increase ? quantity : 0;
        var quantityOut = increase ? 0 : quantity;
        return new(currentQuantity, quantityIn, quantityOut, currentQuantity + quantityIn - quantityOut, quantityIn - quantityOut);
    }

    public static StockQuantityChange PhysicalCount(decimal currentQuantity, decimal countedQuantity)
    {
        ValidateCurrent(currentQuantity);
        if (countedQuantity < 0)
        {
            throw new ArgumentException("Physical count cannot be negative.");
        }

        var difference = countedQuantity - currentQuantity;
        return new(
            currentQuantity,
            difference > 0 ? difference : 0,
            difference < 0 ? Math.Abs(difference) : 0,
            countedQuantity,
            difference);
    }

    public static StockQuantityChange Transfer(decimal currentQuantity, decimal quantity)
    {
        ValidateCurrent(currentQuantity);
        ValidatePositive(quantity, "Transfer quantity");
        if (quantity > currentQuantity)
        {
            throw new ArgumentException("Transfer cannot reduce source stock below zero.");
        }

        return new(currentQuantity, 0, quantity, currentQuantity - quantity, -quantity);
    }

    private static void ValidateCurrent(decimal currentQuantity)
    {
        if (currentQuantity < 0)
        {
            throw new ArgumentException("Current stock cannot be negative.");
        }
    }

    private static void ValidatePositive(decimal quantity, string label)
    {
        if (quantity <= 0)
        {
            throw new ArgumentException($"{label} must be greater than zero.");
        }
    }
}
