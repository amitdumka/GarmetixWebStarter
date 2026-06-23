namespace Garmetix.Api.Inventory;

public static class StockReportCalculator
{
    public static string AgeBucket(decimal quantity, DateTime? lastInwardAt, DateTime asOf)
    {
        if (quantity <= 0)
        {
            return "Out of Stock";
        }

        if (!lastInwardAt.HasValue)
        {
            return "No Receipt History";
        }

        var days = Math.Max(0, (asOf.Date - lastInwardAt.Value.Date).Days);
        return days switch
        {
            <= 30 => "0-30 Days",
            <= 60 => "31-60 Days",
            <= 90 => "61-90 Days",
            <= 180 => "91-180 Days",
            _ => "180+ Days"
        };
    }

    public static string Risk(decimal quantity, decimal lowStockThreshold)
    {
        var threshold = Math.Max(1, lowStockThreshold);
        if (quantity <= 0)
        {
            return "Critical";
        }

        if (quantity <= threshold)
        {
            return "Low";
        }

        if (quantity <= threshold * 2)
        {
            return "Watch";
        }

        return "Healthy";
    }

    public static string Reconciliation(
        decimal ledgerQuantity,
        decimal projectedQuantity,
        decimal ledgerAverageCost,
        decimal projectedAverageCost)
    {
        var quantityMatches = Math.Abs(ledgerQuantity - projectedQuantity) <= 0.01m;
        var costMatches = Math.Abs(ledgerAverageCost - projectedAverageCost) <= 0.01m;
        return quantityMatches && costMatches ? "Matched" : "Mismatch";
    }
}
