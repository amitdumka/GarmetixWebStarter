using Garmetix.Core.Models.Inventory;
using Garmetix.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Garmetix.Api.FinalAccounts;

public static class FinalAccountsInventoryAdapterLines
{
    public const string InventoryStockMappingKey = "INVENTORY.STOCK";
    public const string InventoryCogsMappingKey = "INVENTORY.COGS";
    public const string InventoryShortageMappingKey = "INVENTORY.SHORTAGE";
    public const string InventoryExcessMappingKey = "INVENTORY.EXCESS";
    public const string InventoryTransferClearingMappingKey = "INVENTORY.TRANSFER_CLEARING";
    public const string PurchaseDirectMappingKey = "PURCHASE.DIRECT";
    public const string PurchaseReturnMappingKey = "PURCHASE.RETURN";
    public const string ValuationMethod = "WeightedAverage";

    public static IReadOnlyList<FinalAccountsPostingPreviewLineRequest> SaleCogsLines(decimal costAmount, string narration)
        => BalancedTwoLeg(InventoryCogsMappingKey, InventoryStockMappingKey, costAmount, narration);

    public static IReadOnlyList<FinalAccountsPostingPreviewLineRequest> SaleReturnStockRestorationLines(decimal costAmount, string narration)
        => BalancedTwoLeg(InventoryStockMappingKey, InventoryCogsMappingKey, costAmount, narration);

    public static IReadOnlyList<FinalAccountsPostingPreviewLineRequest> PurchaseInventoryLines(decimal costAmount, string narration)
        => BalancedTwoLeg(InventoryStockMappingKey, PurchaseDirectMappingKey, costAmount, narration);

    public static IReadOnlyList<FinalAccountsPostingPreviewLineRequest> PurchaseReturnInventoryLines(decimal costAmount, string narration)
        => BalancedTwoLeg(PurchaseReturnMappingKey, InventoryStockMappingKey, costAmount, narration);

    public static IReadOnlyList<FinalAccountsPostingPreviewLineRequest> StockAdjustmentLines(decimal inventoryIncreaseValue, decimal inventoryDecreaseValue, string narration)
    {
        var increase = Amount(inventoryIncreaseValue);
        var decrease = Amount(inventoryDecreaseValue);
        if (increase == 0m && decrease == 0m)
        {
            throw new ArgumentException("Stock adjustment value must be non-zero.");
        }

        var lines = new List<FinalAccountsPostingPreviewLineRequest>();
        AddDebit(lines, InventoryStockMappingKey, increase, narration);
        AddCredit(lines, InventoryExcessMappingKey, increase, $"{narration} excess");
        AddDebit(lines, InventoryShortageMappingKey, decrease, $"{narration} shortage");
        AddCredit(lines, InventoryStockMappingKey, decrease, narration);
        EnsureBalanced(lines, narration);
        return FinalAccountsPaymentAdapterLines.NormalizeLines(lines);
    }

    public static IReadOnlyList<FinalAccountsPostingPreviewLineRequest> StockTransferLines(decimal outValue, decimal inValue, string narration)
    {
        var sourceValue = Amount(outValue);
        var destinationValue = Amount(inValue);
        if (sourceValue == 0m || destinationValue == 0m)
        {
            throw new ArgumentException("Stock transfer must include both source and destination movement values.");
        }

        var lines = new List<FinalAccountsPostingPreviewLineRequest>
        {
            new(InventoryTransferClearingMappingKey, sourceValue, 0m, $"{narration} source clearing"),
            new(InventoryStockMappingKey, 0m, sourceValue, $"{narration} source stock"),
            new(InventoryStockMappingKey, destinationValue, 0m, $"{narration} destination stock"),
            new(InventoryTransferClearingMappingKey, 0m, destinationValue, $"{narration} destination clearing")
        };
        EnsureBalanced(lines, narration);
        return FinalAccountsPaymentAdapterLines.NormalizeLines(lines);
    }

    public static decimal CostOutValue(IReadOnlyList<StockMovement> movements, string narration)
    {
        EnsureMovementEvidence(movements, narration);
        var amount = Amount(movements.Where(item => item.QuantityOut > 0m).Sum(item => Math.Abs(item.CostImpact)));
        if (amount == 0m)
        {
            throw new InvalidOperationException($"Missing stock-out cost evidence for {narration}.");
        }

        return amount;
    }

    public static decimal CostInValue(IReadOnlyList<StockMovement> movements, string narration)
    {
        EnsureMovementEvidence(movements, narration);
        var amount = Amount(movements.Where(item => item.QuantityIn > 0m).Sum(item => Math.Abs(item.CostImpact)));
        if (amount == 0m)
        {
            throw new InvalidOperationException($"Missing stock-in cost evidence for {narration}.");
        }

        return amount;
    }

    public static (decimal InValue, decimal OutValue) StockOperationValues(IReadOnlyList<StockMovement> movements, string narration)
    {
        EnsureMovementEvidence(movements, narration);
        var inValue = Amount(movements.Where(item => item.QuantityIn > 0m).Sum(item => Math.Abs(item.CostImpact)));
        var outValue = Amount(movements.Where(item => item.QuantityOut > 0m).Sum(item => Math.Abs(item.CostImpact)));
        if (inValue == 0m && outValue == 0m)
        {
            throw new InvalidOperationException($"Missing stock operation cost evidence for {narration}.");
        }

        return (inValue, outValue);
    }

    public static string MovementHashPayload(string sourceReference, IReadOnlyList<StockMovement> movements)
        => string.Join("|", [
            sourceReference,
            ValuationMethod,
            string.Join(";", movements
                .OrderBy(item => item.OnDate)
                .ThenBy(item => item.CreatedAt)
                .ThenBy(item => item.Id)
                .Select(item => string.Join(",", [
                    item.Id.ToString("D"),
                    item.OnDate.ToString("O"),
                    item.StockId?.ToString("D") ?? string.Empty,
                    item.ProductId.ToString("D"),
                    item.Barcode,
                    item.MovementType,
                    item.SourceType ?? string.Empty,
                    item.SourceId?.ToString("D") ?? string.Empty,
                    FinalAccountsJournalRules.RoundAmount(item.QuantityIn).ToString("0.00"),
                    FinalAccountsJournalRules.RoundAmount(item.QuantityOut).ToString("0.00"),
                    FinalAccountsJournalRules.RoundAmount(item.CostPrice).ToString("0.00"),
                    FinalAccountsJournalRules.RoundAmount(item.CostImpact).ToString("0.00"),
                    item.ValuationMethod
                ])))
        ]);

    private static IReadOnlyList<FinalAccountsPostingPreviewLineRequest> BalancedTwoLeg(string debitMappingKey, string creditMappingKey, decimal amount, string narration)
    {
        var rounded = Amount(amount);
        if (rounded == 0m)
        {
            throw new ArgumentException("Inventory posting amount must be non-zero.");
        }

        return
        [
            new FinalAccountsPostingPreviewLineRequest(debitMappingKey, rounded, 0m, narration),
            new FinalAccountsPostingPreviewLineRequest(creditMappingKey, 0m, rounded, narration)
        ];
    }

    private static void EnsureMovementEvidence(IReadOnlyList<StockMovement> movements, string narration)
    {
        if (movements.Count == 0)
        {
            throw new ArgumentException($"No stock movements were found for {narration}.");
        }

        if (movements.Any(item => item.QuantityAfter < 0m))
        {
            throw new InvalidOperationException($"Negative stock movement evidence blocks inventory posting for {narration}.");
        }

        if (movements.Any(item => (item.QuantityIn > 0m || item.QuantityOut > 0m) && item.CostPrice <= 0m))
        {
            throw new InvalidOperationException($"Missing stock cost evidence blocks inventory posting for {narration}.");
        }
    }

    private static void AddDebit(List<FinalAccountsPostingPreviewLineRequest> lines, string mappingKey, decimal amount, string narration)
    {
        var rounded = Amount(amount);
        if (rounded > 0m)
        {
            lines.Add(new FinalAccountsPostingPreviewLineRequest(mappingKey, rounded, 0m, narration));
        }
    }

    private static void AddCredit(List<FinalAccountsPostingPreviewLineRequest> lines, string mappingKey, decimal amount, string narration)
    {
        var rounded = Amount(amount);
        if (rounded > 0m)
        {
            lines.Add(new FinalAccountsPostingPreviewLineRequest(mappingKey, 0m, rounded, narration));
        }
    }

    private static decimal Amount(decimal value)
        => FinalAccountsJournalRules.RoundAmount(Math.Abs(value));

    private static void EnsureBalanced(IReadOnlyList<FinalAccountsPostingPreviewLineRequest> lines, string narration)
    {
        var debit = FinalAccountsJournalRules.RoundAmount(lines.Sum(line => line.Debit));
        var credit = FinalAccountsJournalRules.RoundAmount(lines.Sum(line => line.Credit));
        if (debit <= 0m || credit <= 0m || debit != credit)
        {
            throw new InvalidOperationException($"Inventory posting preview is not balanced for {narration}.");
        }
    }
}

public sealed class SaleCogsAdapter(GarmetixDbContext db) : FinalAccountsPostingAdapterBase(db)
{
    public override string AdapterKey => "sale-cogs";
    public override string SourceType => "Inventory";
    public override string RuleCode => "SaleCogs";
    public override string DisplayName => "Sale COGS";
    public override string SourceTable => "StockMovements";
    public override string Description => "Debits COGS and credits inventory from sale stock-out movements.";

    public override async Task<FinalAccountsPostingPreviewRequest> BuildPreviewAsync(Guid sourceId, FinalAccountsScopeDto scope, HttpContext context, CancellationToken cancellationToken)
    {
        _ = context;
        var movements = await LoadMovementsAsync(sourceId, ["SalesInvoice", "SalesExchange", "VyaparSaleImport"], quantityOut: true, cancellationToken);
        var first = movements.First();
        EnsureScope(scope, first.CompanyId, first.StoreGroupId, first.StoreId);
        var reference = first.SourceNumber ?? first.SourceId?.ToString("D") ?? sourceId.ToString("D");
        var narration = $"Sale COGS {reference}";
        var lines = FinalAccountsInventoryAdapterLines.SaleCogsLines(FinalAccountsInventoryAdapterLines.CostOutValue(movements, narration), narration);
        return FinalAccountsPaymentAdapterLines.Request(scope, SourceType, RuleCode, sourceId, reference, FinalAccountsInventoryAdapterLines.MovementHashPayload(reference, movements), lines);
    }

    private Task<List<StockMovement>> LoadMovementsAsync(Guid sourceId, IReadOnlyList<string> sourceTypes, bool quantityOut, CancellationToken cancellationToken)
        => Db.StockMovements.AsNoTracking()
            .Where(item => !item.Deleted
                && item.SourceId == sourceId
                && item.SourceType != null
                && sourceTypes.Contains(item.SourceType)
                && (quantityOut ? item.QuantityOut > 0m : item.QuantityIn > 0m))
            .OrderBy(item => item.OnDate)
            .ThenBy(item => item.CreatedAt)
            .ThenBy(item => item.Id)
            .ToListAsync(cancellationToken);
}

public sealed class SaleReturnStockRestorationAdapter(GarmetixDbContext db) : FinalAccountsPostingAdapterBase(db)
{
    public override string AdapterKey => "sale-return-stock-restoration";
    public override string SourceType => "Inventory";
    public override string RuleCode => "SaleReturnStockRestoration";
    public override string DisplayName => "Sale Return Stock Restoration";
    public override string SourceTable => "StockMovements";
    public override string Description => "Debits inventory and credits COGS from sale return or cancellation stock-in movements.";

    public override async Task<FinalAccountsPostingPreviewRequest> BuildPreviewAsync(Guid sourceId, FinalAccountsScopeDto scope, HttpContext context, CancellationToken cancellationToken)
    {
        _ = context;
        var movements = await LoadMovementsAsync(sourceId, ["SalesReturn", "SalesInvoiceCancellation"], cancellationToken);
        var first = movements.First();
        EnsureScope(scope, first.CompanyId, first.StoreGroupId, first.StoreId);
        var reference = first.SourceNumber ?? first.SourceId?.ToString("D") ?? sourceId.ToString("D");
        var narration = $"Sale return stock restoration {reference}";
        var lines = FinalAccountsInventoryAdapterLines.SaleReturnStockRestorationLines(FinalAccountsInventoryAdapterLines.CostInValue(movements, narration), narration);
        return FinalAccountsPaymentAdapterLines.Request(scope, SourceType, RuleCode, sourceId, reference, FinalAccountsInventoryAdapterLines.MovementHashPayload(reference, movements), lines);
    }

    private Task<List<StockMovement>> LoadMovementsAsync(Guid sourceId, IReadOnlyList<string> sourceTypes, CancellationToken cancellationToken)
        => Db.StockMovements.AsNoTracking()
            .Where(item => !item.Deleted
                && item.SourceId == sourceId
                && item.SourceType != null
                && sourceTypes.Contains(item.SourceType)
                && item.QuantityIn > 0m)
            .OrderBy(item => item.OnDate)
            .ThenBy(item => item.CreatedAt)
            .ThenBy(item => item.Id)
            .ToListAsync(cancellationToken);
}

public sealed class PurchaseInventoryAdapter(GarmetixDbContext db) : FinalAccountsPostingAdapterBase(db)
{
    public override string AdapterKey => "purchase-inventory";
    public override string SourceType => "Inventory";
    public override string RuleCode => "PurchaseInventory";
    public override string DisplayName => "Purchase Inventory";
    public override string SourceTable => "StockMovements";
    public override string Description => "Debits inventory and credits direct purchases from purchase stock-in movements.";

    public override async Task<FinalAccountsPostingPreviewRequest> BuildPreviewAsync(Guid sourceId, FinalAccountsScopeDto scope, HttpContext context, CancellationToken cancellationToken)
    {
        _ = context;
        var movements = await LoadMovementsAsync(sourceId, ["PurchaseInvoice", "PurchaseInvoiceImport"], quantityIn: true, cancellationToken);
        var first = movements.First();
        EnsureScope(scope, first.CompanyId, first.StoreGroupId, first.StoreId);
        var reference = first.SourceNumber ?? first.SourceId?.ToString("D") ?? sourceId.ToString("D");
        var narration = $"Purchase inventory {reference}";
        var lines = FinalAccountsInventoryAdapterLines.PurchaseInventoryLines(FinalAccountsInventoryAdapterLines.CostInValue(movements, narration), narration);
        return FinalAccountsPaymentAdapterLines.Request(scope, SourceType, RuleCode, sourceId, reference, FinalAccountsInventoryAdapterLines.MovementHashPayload(reference, movements), lines);
    }

    private Task<List<StockMovement>> LoadMovementsAsync(Guid sourceId, IReadOnlyList<string> sourceTypes, bool quantityIn, CancellationToken cancellationToken)
        => Db.StockMovements.AsNoTracking()
            .Where(item => !item.Deleted
                && item.SourceId == sourceId
                && item.SourceType != null
                && sourceTypes.Contains(item.SourceType)
                && (quantityIn ? item.QuantityIn > 0m : item.QuantityOut > 0m))
            .OrderBy(item => item.OnDate)
            .ThenBy(item => item.CreatedAt)
            .ThenBy(item => item.Id)
            .ToListAsync(cancellationToken);
}

public sealed class PurchaseReturnInventoryAdapter(GarmetixDbContext db) : FinalAccountsPostingAdapterBase(db)
{
    public override string AdapterKey => "purchase-return-inventory";
    public override string SourceType => "Inventory";
    public override string RuleCode => "PurchaseReturnInventory";
    public override string DisplayName => "Purchase Return Inventory";
    public override string SourceTable => "StockMovements";
    public override string Description => "Debits purchase return and credits inventory from purchase-return stock-out movements.";

    public override async Task<FinalAccountsPostingPreviewRequest> BuildPreviewAsync(Guid sourceId, FinalAccountsScopeDto scope, HttpContext context, CancellationToken cancellationToken)
    {
        _ = context;
        var movements = await LoadMovementsAsync(sourceId, cancellationToken);
        var first = movements.First();
        EnsureScope(scope, first.CompanyId, first.StoreGroupId, first.StoreId);
        var reference = first.SourceNumber ?? first.SourceId?.ToString("D") ?? sourceId.ToString("D");
        var narration = $"Purchase return inventory {reference}";
        var lines = FinalAccountsInventoryAdapterLines.PurchaseReturnInventoryLines(FinalAccountsInventoryAdapterLines.CostOutValue(movements, narration), narration);
        return FinalAccountsPaymentAdapterLines.Request(scope, SourceType, RuleCode, sourceId, reference, FinalAccountsInventoryAdapterLines.MovementHashPayload(reference, movements), lines);
    }

    private Task<List<StockMovement>> LoadMovementsAsync(Guid sourceId, CancellationToken cancellationToken)
        => Db.StockMovements.AsNoTracking()
            .Where(item => !item.Deleted
                && item.SourceId == sourceId
                && item.SourceType == "PurchaseReturn"
                && item.QuantityOut > 0m)
            .OrderBy(item => item.OnDate)
            .ThenBy(item => item.CreatedAt)
            .ThenBy(item => item.Id)
            .ToListAsync(cancellationToken);
}

public sealed class StockAdjustmentAdapter(GarmetixDbContext db) : FinalAccountsPostingAdapterBase(db)
{
    public override string AdapterKey => "stock-adjustment";
    public override string SourceType => "Inventory";
    public override string RuleCode => "StockAdjustment";
    public override string DisplayName => "Stock Adjustment";
    public override string SourceTable => "StockOperationDocuments";
    public override string Description => "Maps stock excess, shortage and write-off operation values.";

    public override async Task<FinalAccountsPostingPreviewRequest> BuildPreviewAsync(Guid sourceId, FinalAccountsScopeDto scope, HttpContext context, CancellationToken cancellationToken)
    {
        _ = context;
        var document = await Db.StockOperationDocuments.AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == sourceId && !item.Deleted && item.Status != "Cancelled" && item.OperationType != "Transfer", cancellationToken)
            ?? throw new ArgumentException("Stock adjustment document source was not found.");
        EnsureScope(scope, document.CompanyId, document.StoreGroupId, document.StoreId);
        var movements = await LoadMovementsAsync(sourceId, cancellationToken);
        var (inValue, outValue) = FinalAccountsInventoryAdapterLines.StockOperationValues(movements, document.DocumentNumber);
        var lines = FinalAccountsInventoryAdapterLines.StockAdjustmentLines(inValue, outValue, $"Stock adjustment {document.DocumentNumber}");
        return FinalAccountsPaymentAdapterLines.Request(scope, SourceType, RuleCode, sourceId, document.DocumentNumber, FinalAccountsInventoryAdapterLines.MovementHashPayload(document.DocumentNumber, movements), lines);
    }

    private Task<List<StockMovement>> LoadMovementsAsync(Guid sourceId, CancellationToken cancellationToken)
        => Db.StockMovements.AsNoTracking()
            .Where(item => !item.Deleted
                && item.SourceId == sourceId
                && item.SourceType == "StockOperationDocument")
            .OrderBy(item => item.OnDate)
            .ThenBy(item => item.CreatedAt)
            .ThenBy(item => item.Id)
            .ToListAsync(cancellationToken);
}

public sealed class StockTransferAdapter(GarmetixDbContext db) : FinalAccountsPostingAdapterBase(db)
{
    public override string AdapterKey => "stock-transfer";
    public override string SourceType => "Inventory";
    public override string RuleCode => "StockTransfer";
    public override string DisplayName => "Stock Transfer";
    public override string SourceTable => "StockOperationDocuments";
    public override string Description => "Maps inter-store transfer source and destination movement values through transfer clearing.";

    public override async Task<FinalAccountsPostingPreviewRequest> BuildPreviewAsync(Guid sourceId, FinalAccountsScopeDto scope, HttpContext context, CancellationToken cancellationToken)
    {
        _ = context;
        var document = await Db.StockOperationDocuments.AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == sourceId && !item.Deleted && item.Status != "Cancelled" && item.OperationType == "Transfer", cancellationToken)
            ?? throw new ArgumentException("Stock transfer document source was not found.");
        EnsureScope(scope, document.CompanyId, document.StoreGroupId, document.StoreId);
        var movements = await LoadMovementsAsync(sourceId, cancellationToken);
        var (inValue, outValue) = FinalAccountsInventoryAdapterLines.StockOperationValues(movements, document.DocumentNumber);
        var lines = FinalAccountsInventoryAdapterLines.StockTransferLines(outValue, inValue, $"Stock transfer {document.DocumentNumber}");
        return FinalAccountsPaymentAdapterLines.Request(scope, SourceType, RuleCode, sourceId, document.DocumentNumber, FinalAccountsInventoryAdapterLines.MovementHashPayload(document.DocumentNumber, movements), lines);
    }

    private Task<List<StockMovement>> LoadMovementsAsync(Guid sourceId, CancellationToken cancellationToken)
        => Db.StockMovements.AsNoTracking()
            .Where(item => !item.Deleted
                && item.SourceId == sourceId
                && item.SourceType == "StockOperationDocument")
            .OrderBy(item => item.OnDate)
            .ThenBy(item => item.CreatedAt)
            .ThenBy(item => item.Id)
            .ToListAsync(cancellationToken);
}
