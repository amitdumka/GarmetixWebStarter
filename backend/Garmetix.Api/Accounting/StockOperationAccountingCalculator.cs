namespace Garmetix.Api.Accounting;

public enum StockOperationAccountingKind
{
    Excess,
    Shortage,
    WriteOff,
    Transfer
}

public sealed record StockOperationAccountingPlan(
    decimal SourceInventoryDebit,
    decimal SourceInventoryCredit,
    decimal DestinationInventoryDebit,
    decimal GainCredit,
    decimal LossDebit);

public static class StockOperationAccountingCalculator
{
    public static StockOperationAccountingPlan Create(StockOperationAccountingKind kind, decimal amount)
    {
        var value = Math.Round(Math.Abs(amount), 2, MidpointRounding.AwayFromZero);
        if (value == 0)
        {
            return new(0, 0, 0, 0, 0);
        }

        return kind switch
        {
            StockOperationAccountingKind.Excess => new(value, 0, 0, value, 0),
            StockOperationAccountingKind.Shortage => new(0, value, 0, 0, value),
            StockOperationAccountingKind.WriteOff => new(0, value, 0, 0, value),
            StockOperationAccountingKind.Transfer => new(0, value, value, 0, 0),
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unsupported stock accounting operation.")
        };
    }
}
