using Garmetix.Core.Enums;
using Garmetix.Core.Models.Inventory;
using Garmetix.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Garmetix.Api.FinalAccounts;

public static class FinalAccountsPurchaseAdapterLines
{
    public const string VendorPayableMappingKey = "VENDOR.PAYABLE";
    public const string PurchaseDirectMappingKey = "PURCHASE.DIRECT";
    public const string PurchaseReturnMappingKey = "PURCHASE.RETURN";
    public const string PurchaseFreightMappingKey = "PURCHASE.FREIGHT";
    public const string PurchaseRoundingMappingKey = "PURCHASE.ROUNDING";
    public const string InputCgstMappingKey = "GST.INPUT_CGST";
    public const string InputSgstMappingKey = "GST.INPUT_SGST";
    public const string InputIgstMappingKey = "GST.INPUT_IGST";

    public static IReadOnlyList<FinalAccountsPostingPreviewLineRequest> PurchaseInvoiceLines(
        decimal billAmount,
        decimal taxableAmount,
        decimal freightAmount,
        decimal taxAmount,
        decimal? cgstAmount,
        decimal? sgstAmount,
        decimal? igstAmount,
        bool interState,
        decimal roundOff,
        string narration)
    {
        var lines = new List<FinalAccountsPostingPreviewLineRequest>();
        AddDebit(lines, PurchaseDirectMappingKey, PositiveAmount(taxableAmount), narration);
        AddInputTax(lines, taxAmount, cgstAmount, sgstAmount, igstAmount, interState, debitTax: true, $"{narration} GST");
        AddDebit(lines, PurchaseFreightMappingKey, PositiveAmount(freightAmount), $"{narration} freight");
        AddPurchaseRoundOff(lines, roundOff, narration);
        AddCredit(lines, VendorPayableMappingKey, PositiveAmount(billAmount), narration);
        EnsureBalanced(lines, narration);
        return lines;
    }

    public static IReadOnlyList<FinalAccountsPostingPreviewLineRequest> PurchaseReturnLines(
        decimal returnAmount,
        decimal taxableAmount,
        decimal taxAmount,
        decimal? cgstAmount,
        decimal? sgstAmount,
        decimal? igstAmount,
        bool interState,
        decimal freightAmount,
        decimal freightTaxAmount,
        string narration)
    {
        var lines = new List<FinalAccountsPostingPreviewLineRequest>();
        var roundedReturn = PositiveAmount(returnAmount);
        var roundedFreight = PositiveAmount(freightAmount);
        var roundedFreightTax = PositiveAmount(freightTaxAmount);
        var returnRoundOff = FinalAccountsJournalRules.RoundAmount(roundedReturn - PositiveAmount(taxableAmount) - PositiveAmount(taxAmount));

        AddDebit(lines, VendorPayableMappingKey, roundedReturn, narration);
        AddCredit(lines, PurchaseReturnMappingKey, PositiveAmount(taxableAmount), narration);
        AddInputTax(lines, taxAmount, cgstAmount, sgstAmount, igstAmount, interState, debitTax: false, $"{narration} ITC reversal");
        AddPurchaseReturnRoundOff(lines, returnRoundOff, narration);

        if (roundedFreight > 0m || roundedFreightTax > 0m)
        {
            AddDebit(lines, VendorPayableMappingKey, FinalAccountsJournalRules.RoundAmount(roundedFreight + roundedFreightTax), $"{narration} freight recovery");
            AddCredit(lines, PurchaseFreightMappingKey, roundedFreight, $"{narration} freight recovery");
            AddInputTax(lines, roundedFreightTax, null, null, null, interState, debitTax: false, $"{narration} freight GST reversal");
        }

        EnsureBalanced(lines, narration);
        return lines;
    }

    public static IReadOnlyList<FinalAccountsPostingPreviewLineRequest> PurchaseCancellationLines(
        decimal billAmount,
        decimal taxableAmount,
        decimal freightAmount,
        decimal taxAmount,
        decimal? cgstAmount,
        decimal? sgstAmount,
        decimal? igstAmount,
        bool interState,
        decimal roundOff,
        string narration)
        => PurchaseInvoiceLines(
                billAmount,
                taxableAmount,
                freightAmount,
                taxAmount,
                cgstAmount,
                sgstAmount,
                igstAmount,
                interState,
                roundOff,
                narration)
            .Select(line => new FinalAccountsPostingPreviewLineRequest(
                line.MappingKey,
                line.Credit,
                line.Debit,
                $"{line.Narration} cancellation"))
            .ToList();

    public static string PurchaseInvoiceHashPayload(PurchaseInvoice invoice, IReadOnlyList<PurchaseInvoiceItem> items)
        => string.Join("|", [
            invoice.OnDate.ToString("O"),
            invoice.InvoiceNumber,
            invoice.InvoiceType.ToString(),
            invoice.InvoiceStatus.ToString(),
            invoice.OriginalInvoiceId?.ToString("D") ?? string.Empty,
            invoice.VendorId.ToString("D"),
            invoice.StoreGroupId?.ToString("D") ?? string.Empty,
            invoice.StoreId?.ToString("D") ?? string.Empty,
            invoice.PaymentMode?.ToString() ?? string.Empty,
            FinalAccountsJournalRules.RoundAmount(invoice.BillAmount).ToString("0.00"),
            FinalAccountsJournalRules.RoundAmount(invoice.BasePrice).ToString("0.00"),
            FinalAccountsJournalRules.RoundAmount(invoice.TaxAmount).ToString("0.00"),
            FinalAccountsJournalRules.RoundAmount(invoice.CGSTAmount ?? 0m).ToString("0.00"),
            FinalAccountsJournalRules.RoundAmount(invoice.SGSTAmount ?? 0m).ToString("0.00"),
            FinalAccountsJournalRules.RoundAmount(invoice.IGSTAmount ?? 0m).ToString("0.00"),
            FinalAccountsJournalRules.RoundAmount(invoice.FrightAmount).ToString("0.00"),
            FinalAccountsJournalRules.RoundAmount(invoice.RoundOff).ToString("0.00"),
            PurchaseItemDimensionSummary(items)
        ]);

    public static string PurchaseReturnHashPayload(PurchaseReturn source, IReadOnlyList<PurchaseReturnItem> items)
        => string.Join("|", [
            source.OnDate.ToString("O"),
            source.ReturnNumber,
            source.Status,
            source.ReturnKind,
            source.PurchaseInvoiceId.ToString("D"),
            source.VendorId.ToString("D"),
            source.StoreGroupId.ToString("D"),
            source.StoreId.ToString("D"),
            source.DebitNoteId?.ToString("D") ?? string.Empty,
            source.DebitNoteNumber ?? string.Empty,
            FinalAccountsJournalRules.RoundAmount(source.ReturnAmount).ToString("0.00"),
            FinalAccountsJournalRules.RoundAmount(source.TaxableAmount).ToString("0.00"),
            FinalAccountsJournalRules.RoundAmount(source.TaxAmount).ToString("0.00"),
            FinalAccountsJournalRules.RoundAmount(source.CGSTAmount).ToString("0.00"),
            FinalAccountsJournalRules.RoundAmount(source.SGSTAmount).ToString("0.00"),
            FinalAccountsJournalRules.RoundAmount(source.IGSTAmount).ToString("0.00"),
            FinalAccountsJournalRules.RoundAmount(source.FreightAmount).ToString("0.00"),
            FinalAccountsJournalRules.RoundAmount(source.FreightTaxAmount).ToString("0.00"),
            PurchaseReturnDimensionSummary(items)
        ]);

    public static string CommercialNoteHashPayload(CommercialNote note)
        => FinalAccountsSalesAdapterLines.CommercialNoteHashPayload(note);

    public static string PurchaseItemDimensionSummary(IReadOnlyList<PurchaseInvoiceItem> items)
    {
        var productIds = items.Select(item => item.ProductId).Where(id => id != Guid.Empty).Distinct().OrderBy(id => id).ToList();
        var categoryIds = items.Select(item => item.ProductCategoryId).Where(id => id.HasValue).Select(id => id!.Value).Distinct().OrderBy(id => id).ToList();
        var quantity = FinalAccountsJournalRules.RoundAmount(items.Sum(item => item.BilledQuantity));
        return string.Join(";", [
            $"items={items.Count}",
            $"qty={quantity:0.00}",
            $"products={productIds.Count}:{string.Join(",", productIds.Select(id => id.ToString("D")))}",
            $"categories={categoryIds.Count}:{string.Join(",", categoryIds.Select(id => id.ToString("D")))}"
        ]);
    }

    public static string PurchaseReturnDimensionSummary(IReadOnlyList<PurchaseReturnItem> items)
    {
        var productIds = items.Select(item => item.ProductId).Where(id => id != Guid.Empty).Distinct().OrderBy(id => id).ToList();
        var categoryIds = items.Select(item => item.ProductCategoryId).Where(id => id.HasValue).Select(id => id!.Value).Distinct().OrderBy(id => id).ToList();
        var quantity = FinalAccountsJournalRules.RoundAmount(items.Sum(item => item.ReturnedQuantity));
        return string.Join(";", [
            $"items={items.Count}",
            $"qty={quantity:0.00}",
            $"products={productIds.Count}:{string.Join(",", productIds.Select(id => id.ToString("D")))}",
            $"categories={categoryIds.Count}:{string.Join(",", categoryIds.Select(id => id.ToString("D")))}"
        ]);
    }

    private static void AddInputTax(
        List<FinalAccountsPostingPreviewLineRequest> lines,
        decimal taxAmount,
        decimal? cgstAmount,
        decimal? sgstAmount,
        decimal? igstAmount,
        bool interState,
        bool debitTax,
        string narration)
    {
        foreach (var (mappingKey, amount) in InputTaxBreakdown(taxAmount, cgstAmount, sgstAmount, igstAmount, interState))
        {
            if (debitTax)
            {
                AddDebit(lines, mappingKey, amount, narration);
            }
            else
            {
                AddCredit(lines, mappingKey, amount, narration);
            }
        }
    }

    private static IReadOnlyList<(string MappingKey, decimal Amount)> InputTaxBreakdown(
        decimal taxAmount,
        decimal? cgstAmount,
        decimal? sgstAmount,
        decimal? igstAmount,
        bool interState)
    {
        var rows = new List<(string MappingKey, decimal Amount)>();
        AddTaxAmount(rows, InputCgstMappingKey, cgstAmount ?? 0m);
        AddTaxAmount(rows, InputSgstMappingKey, sgstAmount ?? 0m);
        AddTaxAmount(rows, InputIgstMappingKey, igstAmount ?? 0m);
        var explicitTotal = rows.Sum(item => item.Amount);
        var expectedTotal = Math.Max(PositiveAmount(taxAmount), explicitTotal);
        var residual = FinalAccountsJournalRules.RoundAmount(expectedTotal - explicitTotal);
        if (residual <= 0m)
        {
            return rows;
        }

        if (interState)
        {
            AddTaxAmount(rows, InputIgstMappingKey, residual);
            return rows;
        }

        var half = FinalAccountsJournalRules.RoundAmount(residual / 2m);
        AddTaxAmount(rows, InputCgstMappingKey, half);
        AddTaxAmount(rows, InputSgstMappingKey, FinalAccountsJournalRules.RoundAmount(residual - half));
        return rows;
    }

    private static void AddTaxAmount(List<(string MappingKey, decimal Amount)> rows, string mappingKey, decimal amount)
    {
        var rounded = PositiveAmount(amount);
        if (rounded == 0m)
        {
            return;
        }

        var index = rows.FindIndex(item => string.Equals(item.MappingKey, mappingKey, StringComparison.OrdinalIgnoreCase));
        if (index >= 0)
        {
            rows[index] = (mappingKey, FinalAccountsJournalRules.RoundAmount(rows[index].Amount + rounded));
        }
        else
        {
            rows.Add((mappingKey, rounded));
        }
    }

    private static void AddPurchaseRoundOff(List<FinalAccountsPostingPreviewLineRequest> lines, decimal roundOff, string narration)
    {
        var rounded = FinalAccountsJournalRules.RoundAmount(roundOff);
        if (rounded > 0m)
        {
            AddDebit(lines, PurchaseRoundingMappingKey, rounded, $"{narration} round off");
        }
        else if (rounded < 0m)
        {
            AddCredit(lines, PurchaseRoundingMappingKey, Math.Abs(rounded), $"{narration} round off");
        }
    }

    private static void AddPurchaseReturnRoundOff(List<FinalAccountsPostingPreviewLineRequest> lines, decimal roundOff, string narration)
    {
        var rounded = FinalAccountsJournalRules.RoundAmount(roundOff);
        if (rounded > 0m)
        {
            AddCredit(lines, PurchaseRoundingMappingKey, rounded, $"{narration} round off reversal");
        }
        else if (rounded < 0m)
        {
            AddDebit(lines, PurchaseRoundingMappingKey, Math.Abs(rounded), $"{narration} round off reversal");
        }
    }

    private static void AddDebit(List<FinalAccountsPostingPreviewLineRequest> lines, string mappingKey, decimal amount, string narration)
    {
        var rounded = FinalAccountsJournalRules.RoundAmount(amount);
        if (rounded > 0m)
        {
            lines.Add(new FinalAccountsPostingPreviewLineRequest(mappingKey, rounded, 0m, narration));
        }
    }

    private static void AddCredit(List<FinalAccountsPostingPreviewLineRequest> lines, string mappingKey, decimal amount, string narration)
    {
        var rounded = FinalAccountsJournalRules.RoundAmount(amount);
        if (rounded > 0m)
        {
            lines.Add(new FinalAccountsPostingPreviewLineRequest(mappingKey, 0m, rounded, narration));
        }
    }

    private static decimal PositiveAmount(decimal amount)
        => FinalAccountsJournalRules.RoundAmount(Math.Abs(amount));

    private static void EnsureBalanced(IReadOnlyList<FinalAccountsPostingPreviewLineRequest> lines, string narration)
    {
        var debit = FinalAccountsJournalRules.RoundAmount(lines.Sum(line => line.Debit));
        var credit = FinalAccountsJournalRules.RoundAmount(lines.Sum(line => line.Credit));
        if (debit <= 0m || credit <= 0m || debit != credit)
        {
            throw new InvalidOperationException($"Purchase posting preview is not balanced for {narration}.");
        }
    }
}

public sealed class PurchaseInvoiceAdapter(GarmetixDbContext db) : FinalAccountsPostingAdapterBase(db)
{
    public override string AdapterKey => "purchase-invoice";
    public override string SourceType => "Purchase";
    public override string RuleCode => "PurchaseInvoice";
    public override string DisplayName => "Purchase Invoice";
    public override string SourceTable => "PurchaseInvoices";
    public override string Description => "Debits purchase, input GST and freight while crediting vendor payable for purchase invoices.";

    public override async Task<FinalAccountsPostingPreviewRequest> BuildPreviewAsync(Guid sourceId, FinalAccountsScopeDto scope, HttpContext context, CancellationToken cancellationToken)
    {
        _ = context;
        var source = await Db.PurchaseInvoices.AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == sourceId && !item.Deleted && item.InvoiceStatus != InvoiceStatus.Cancelled, cancellationToken)
            ?? throw new ArgumentException("Purchase invoice source was not found.");
        var items = await Db.PurchaseInvoiceItems.AsNoTracking()
            .Where(item => item.InvoiceId == source.Id && !item.Deleted)
            .OrderBy(item => item.ProductId)
            .ThenBy(item => item.Id)
            .ToListAsync(cancellationToken);
        EnsureScope(scope, source.CompanyId, source.StoreGroupId, source.StoreId);
        var lines = FinalAccountsPurchaseAdapterLines.PurchaseInvoiceLines(
            source.BillAmount,
            source.BasePrice,
            source.FrightAmount,
            source.TaxAmount,
            source.CGSTAmount,
            source.SGSTAmount,
            source.IGSTAmount,
            source.InterState,
            source.RoundOff,
            $"Purchase invoice {source.InvoiceNumber}");

        return FinalAccountsPaymentAdapterLines.Request(scope, SourceType, RuleCode, source.Id, source.InvoiceNumber, FinalAccountsPurchaseAdapterLines.PurchaseInvoiceHashPayload(source, items), lines);
    }
}

public sealed class PurchaseInvoiceCancellationAdapter(GarmetixDbContext db) : FinalAccountsPostingAdapterBase(db)
{
    public override string AdapterKey => "purchase-invoice-cancellation";
    public override string SourceType => "Purchase";
    public override string RuleCode => "PurchaseCancellation";
    public override string DisplayName => "Purchase Invoice Cancellation";
    public override string SourceTable => "PurchaseInvoices";
    public override string Description => "Reverses purchase, input GST, freight, rounding and vendor payable for cancelled purchase invoices.";

    public override async Task<FinalAccountsPostingPreviewRequest> BuildPreviewAsync(Guid sourceId, FinalAccountsScopeDto scope, HttpContext context, CancellationToken cancellationToken)
    {
        _ = context;
        var source = await Db.PurchaseInvoices.AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == sourceId && !item.Deleted && item.InvoiceStatus == InvoiceStatus.Cancelled, cancellationToken)
            ?? throw new ArgumentException("Cancelled purchase invoice source was not found.");
        var items = await Db.PurchaseInvoiceItems.AsNoTracking()
            .Where(item => item.InvoiceId == source.Id && !item.Deleted)
            .OrderBy(item => item.ProductId)
            .ThenBy(item => item.Id)
            .ToListAsync(cancellationToken);
        EnsureScope(scope, source.CompanyId, source.StoreGroupId, source.StoreId);
        var lines = FinalAccountsPurchaseAdapterLines.PurchaseCancellationLines(
            source.BillAmount,
            source.BasePrice,
            source.FrightAmount,
            source.TaxAmount,
            source.CGSTAmount,
            source.SGSTAmount,
            source.IGSTAmount,
            source.InterState,
            source.RoundOff,
            $"Cancel purchase invoice {source.InvoiceNumber}");

        return FinalAccountsPaymentAdapterLines.Request(scope, SourceType, RuleCode, source.Id, source.InvoiceNumber, FinalAccountsPurchaseAdapterLines.PurchaseInvoiceHashPayload(source, items), lines);
    }
}

public sealed class PurchaseReturnAdapter(GarmetixDbContext db) : FinalAccountsPostingAdapterBase(db)
{
    public override string AdapterKey => "purchase-return";
    public override string SourceType => "Purchase";
    public override string RuleCode => "PurchaseReturn";
    public override string DisplayName => "Purchase Return";
    public override string SourceTable => "PurchaseReturns";
    public override string Description => "Debits vendor payable and credits purchase return, input GST reversal and freight recovery.";

    public override async Task<FinalAccountsPostingPreviewRequest> BuildPreviewAsync(Guid sourceId, FinalAccountsScopeDto scope, HttpContext context, CancellationToken cancellationToken)
    {
        _ = context;
        var source = await Db.PurchaseReturns.AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == sourceId && !item.Deleted && item.Status != "Cancelled", cancellationToken)
            ?? throw new ArgumentException("Purchase return source was not found.");
        var items = await Db.PurchaseReturnItems.AsNoTracking()
            .Where(item => item.PurchaseReturnId == source.Id && !item.Deleted)
            .OrderBy(item => item.ProductId)
            .ThenBy(item => item.Id)
            .ToListAsync(cancellationToken);
        EnsureScope(scope, source.CompanyId, source.StoreGroupId, source.StoreId);
        var lines = FinalAccountsPurchaseAdapterLines.PurchaseReturnLines(
            source.ReturnAmount,
            source.TaxableAmount,
            source.TaxAmount,
            source.CGSTAmount,
            source.SGSTAmount,
            source.IGSTAmount,
            interState: source.IGSTAmount > 0m,
            freightAmount: source.FreightAmount,
            freightTaxAmount: source.FreightTaxAmount,
            narration: $"Purchase return {source.ReturnNumber}");

        return FinalAccountsPaymentAdapterLines.Request(scope, SourceType, RuleCode, source.Id, source.ReturnNumber, FinalAccountsPurchaseAdapterLines.PurchaseReturnHashPayload(source, items), lines);
    }
}

public sealed class VendorDebitNoteAdapter(GarmetixDbContext db) : FinalAccountsPostingAdapterBase(db)
{
    public override string AdapterKey => "vendor-debit-note";
    public override string SourceType => "Purchase";
    public override string RuleCode => "PurchaseReturn";
    public override string DisplayName => "Vendor Debit Note";
    public override string SourceTable => "CommercialNotes";
    public override string Description => "Maps supplier debit notes to purchase return, input GST reversal and vendor payable reduction.";

    public override async Task<FinalAccountsPostingPreviewRequest> BuildPreviewAsync(Guid sourceId, FinalAccountsScopeDto scope, HttpContext context, CancellationToken cancellationToken)
    {
        _ = context;
        var source = await Db.CommercialNotes.AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == sourceId
                && !item.Deleted
                && item.NoteType == NoteType.DebitNote
                && item.PartyType == PartyType.Vendor,
                cancellationToken)
            ?? throw new ArgumentException("Vendor debit note source was not found.");
        EnsureScope(scope, source.CompanyId, source.StoreGroupId, source.StoreId);
        var lines = FinalAccountsPurchaseAdapterLines.PurchaseReturnLines(
            source.Amount,
            source.TaxableAmount,
            source.TaxAmount,
            null,
            null,
            null,
            interState: false,
            freightAmount: 0m,
            freightTaxAmount: 0m,
            $"Vendor debit note {source.NoteNumber}");

        return FinalAccountsPaymentAdapterLines.Request(scope, SourceType, RuleCode, source.Id, source.NoteNumber, FinalAccountsPurchaseAdapterLines.CommercialNoteHashPayload(source), lines);
    }
}

public sealed class VendorAdvancePaymentAdapter(GarmetixDbContext db) : FinalAccountsPostingAdapterBase(db)
{
    public override string AdapterKey => "vendor-advance-payment";
    public override string SourceType => "CashBank";
    public override string RuleCode => "VendorAdvancePayment";
    public override string DisplayName => "Vendor Advance Payment";
    public override string SourceTable => "PurchasePayments";
    public override string Description => "Debits vendor advance and credits the paid payment rail for supplier advances.";

    public override async Task<FinalAccountsPostingPreviewRequest> BuildPreviewAsync(Guid sourceId, FinalAccountsScopeDto scope, HttpContext context, CancellationToken cancellationToken)
    {
        _ = context;
        var source = await Db.PurchasePayments.AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == sourceId
                && !item.Deleted
                && (item.PurchaseInvoiceId == Guid.Empty || item.AdjustmentSourceType == "VendorAdvance"),
                cancellationToken)
            ?? throw new ArgumentException("Vendor advance payment source was not found.");
        EnsureScope(scope, source.CompanyId, source.StoreGroupId, source.StoreId);
        var paymentKey = FinalAccountsPaymentAdapterLines.PaymentMappingKey(source.PaymentMode);
        var reference = source.ReferenceNumber ?? source.VoucherId?.ToString("D") ?? source.Id.ToString("D");
        var lines = FinalAccountsPaymentAdapterLines.SettlementLines(FinalAccountsPaymentAdapterLines.VendorAdvanceMappingKey, paymentKey, source.Amount, $"Vendor advance {reference}");
        return FinalAccountsPaymentAdapterLines.Request(scope, SourceType, RuleCode, source.Id, reference, HashPayload(source.OnDate, source.Amount, source.PaymentMode, reference), lines);
    }
}
