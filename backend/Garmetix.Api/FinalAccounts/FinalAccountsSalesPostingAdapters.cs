using Garmetix.Api.Workspace;
using Garmetix.Core.Enums;
using Garmetix.Core.Models.Inventory;
using Garmetix.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Garmetix.Api.FinalAccounts;

public static class FinalAccountsSalesAdapterLines
{
    public const string CustomerReceivableMappingKey = "CUSTOMER.RECEIVABLE";
    public const string SalesRevenueMappingKey = "SALES.REVENUE";
    public const string SalesReturnMappingKey = "SALES.RETURN";
    public const string SalesDiscountMappingKey = "SALES.DISCOUNT";
    public const string SalesRoundingMappingKey = "SALES.ROUNDING";
    public const string OutputCgstMappingKey = "GST.OUTPUT_CGST";
    public const string OutputSgstMappingKey = "GST.OUTPUT_SGST";
    public const string OutputIgstMappingKey = "GST.OUTPUT_IGST";

    public static IReadOnlyList<FinalAccountsPostingPreviewLineRequest> SalesInvoiceLines(
        decimal billAmount,
        decimal taxableAmount,
        decimal discountAmount,
        decimal taxAmount,
        decimal? cgstAmount,
        decimal? sgstAmount,
        decimal? igstAmount,
        bool interState,
        decimal roundOff,
        string narration)
    {
        var lines = new List<FinalAccountsPostingPreviewLineRequest>();
        var bill = PositiveAmount(billAmount);
        var taxable = PositiveAmount(taxableAmount);
        var discount = PositiveAmount(discountAmount);
        var grossRevenue = FinalAccountsJournalRules.RoundAmount(taxable + discount);

        AddDebit(lines, CustomerReceivableMappingKey, bill, narration);
        AddDebit(lines, SalesDiscountMappingKey, discount, $"{narration} discount");
        AddCredit(lines, SalesRevenueMappingKey, grossRevenue, narration);
        AddOutputTax(lines, taxAmount, cgstAmount, sgstAmount, igstAmount, interState, creditTax: true, $"{narration} GST");
        AddSaleRoundOff(lines, roundOff, narration);
        EnsureBalanced(lines, narration);
        return lines;
    }

    public static IReadOnlyList<FinalAccountsPostingPreviewLineRequest> SalesReturnLines(
        decimal billAmount,
        decimal taxableAmount,
        decimal discountAmount,
        decimal taxAmount,
        decimal? cgstAmount,
        decimal? sgstAmount,
        decimal? igstAmount,
        bool interState,
        decimal roundOff,
        string narration)
    {
        var lines = new List<FinalAccountsPostingPreviewLineRequest>();
        var bill = PositiveAmount(billAmount);
        var taxable = PositiveAmount(taxableAmount);
        var discount = PositiveAmount(discountAmount);
        var grossReturn = FinalAccountsJournalRules.RoundAmount(taxable + discount);

        AddDebit(lines, SalesReturnMappingKey, grossReturn, narration);
        AddOutputTax(lines, taxAmount, cgstAmount, sgstAmount, igstAmount, interState, creditTax: false, $"{narration} GST reversal");
        AddCredit(lines, CustomerReceivableMappingKey, bill, narration);
        AddCredit(lines, SalesDiscountMappingKey, discount, $"{narration} discount reversal");
        AddReturnRoundOff(lines, roundOff, narration);
        EnsureBalanced(lines, narration);
        return lines;
    }

    public static IReadOnlyList<FinalAccountsPostingPreviewLineRequest> SalesCancellationLines(
        decimal billAmount,
        decimal taxableAmount,
        decimal discountAmount,
        decimal taxAmount,
        decimal? cgstAmount,
        decimal? sgstAmount,
        decimal? igstAmount,
        bool interState,
        decimal roundOff,
        string narration)
    {
        var original = SalesInvoiceLines(
            billAmount,
            taxableAmount,
            discountAmount,
            taxAmount,
            cgstAmount,
            sgstAmount,
            igstAmount,
            interState,
            roundOff,
            narration);

        return original
            .Select(line => new FinalAccountsPostingPreviewLineRequest(
                line.MappingKey,
                line.Credit,
                line.Debit,
                $"{line.Narration} cancellation"))
            .ToList();
    }

    public static string InvoiceHashPayload(Invoice invoice, IReadOnlyList<InvoiceItem> items)
        => string.Join("|", [
            invoice.OnDate.ToString("O"),
            invoice.InvoiceNumber,
            invoice.InvoiceType.ToString(),
            invoice.InvoiceStatus.ToString(),
            invoice.ReturnInvoice.ToString(),
            invoice.OriginalInvoiceId?.ToString("D") ?? string.Empty,
            invoice.CustomerId.ToString("D"),
            invoice.StoreId.ToString("D"),
            invoice.PaymentMode?.ToString() ?? string.Empty,
            invoice.CreditSale.ToString(),
            invoice.B2BSale.ToString(),
            invoice.SaleInvoiceType.ToString(),
            FinalAccountsJournalRules.RoundAmount(invoice.MRP).ToString("0.00"),
            FinalAccountsJournalRules.RoundAmount(invoice.BillAmount).ToString("0.00"),
            FinalAccountsJournalRules.RoundAmount(invoice.NetAmount).ToString("0.00"),
            FinalAccountsJournalRules.RoundAmount(invoice.DiscountAmount).ToString("0.00"),
            FinalAccountsJournalRules.RoundAmount(invoice.TaxAmount).ToString("0.00"),
            FinalAccountsJournalRules.RoundAmount(invoice.CGSTAmount ?? 0m).ToString("0.00"),
            FinalAccountsJournalRules.RoundAmount(invoice.SGSTAmount ?? 0m).ToString("0.00"),
            FinalAccountsJournalRules.RoundAmount(invoice.IGSTAmount ?? 0m).ToString("0.00"),
            FinalAccountsJournalRules.RoundAmount(invoice.RoundOff).ToString("0.00"),
            DimensionSummary(items)
        ]);

    public static string CommercialNoteHashPayload(CommercialNote note)
        => string.Join("|", [
            note.OnDate.ToString("O"),
            note.NoteNumber,
            note.NoteType.ToString(),
            note.PartyType.ToString(),
            note.PartyId?.ToString("D") ?? string.Empty,
            note.CustomerId?.ToString("D") ?? string.Empty,
            note.SourceType,
            note.SourceId?.ToString("D") ?? string.Empty,
            note.SourceNumber ?? string.Empty,
            FinalAccountsJournalRules.RoundAmount(note.TaxableAmount).ToString("0.00"),
            FinalAccountsJournalRules.RoundAmount(note.TaxAmount).ToString("0.00"),
            FinalAccountsJournalRules.RoundAmount(note.Amount).ToString("0.00"),
            FinalAccountsJournalRules.RoundAmount(note.AdjustedAmount).ToString("0.00"),
            note.StoreGroupId.ToString("D"),
            note.StoreId.ToString("D")
        ]);

    public static string DimensionSummary(IReadOnlyList<InvoiceItem> items)
    {
        var productIds = items.Select(item => item.ProductId).Where(id => id != Guid.Empty).Distinct().OrderBy(id => id).ToList();
        var categoryIds = items.Select(item => item.ProductCategoryId).Where(id => id.HasValue).Select(id => id!.Value).Distinct().OrderBy(id => id).ToList();
        var storeItemQuantity = FinalAccountsJournalRules.RoundAmount(items.Sum(item => item.BilledQuantity));
        return string.Join(";", [
            $"items={items.Count}",
            $"qty={storeItemQuantity:0.00}",
            $"products={productIds.Count}:{string.Join(",", productIds.Select(id => id.ToString("D")))}",
            $"categories={categoryIds.Count}:{string.Join(",", categoryIds.Select(id => id.ToString("D")))}"
        ]);
    }

    private static void AddOutputTax(
        List<FinalAccountsPostingPreviewLineRequest> lines,
        decimal taxAmount,
        decimal? cgstAmount,
        decimal? sgstAmount,
        decimal? igstAmount,
        bool interState,
        bool creditTax,
        string narration)
    {
        foreach (var (mappingKey, amount) in OutputTaxBreakdown(taxAmount, cgstAmount, sgstAmount, igstAmount, interState))
        {
            if (creditTax)
            {
                AddCredit(lines, mappingKey, amount, narration);
            }
            else
            {
                AddDebit(lines, mappingKey, amount, narration);
            }
        }
    }

    private static IReadOnlyList<(string MappingKey, decimal Amount)> OutputTaxBreakdown(
        decimal taxAmount,
        decimal? cgstAmount,
        decimal? sgstAmount,
        decimal? igstAmount,
        bool interState)
    {
        var rows = new List<(string MappingKey, decimal Amount)>();
        AddTaxAmount(rows, OutputCgstMappingKey, cgstAmount ?? 0m);
        AddTaxAmount(rows, OutputSgstMappingKey, sgstAmount ?? 0m);
        AddTaxAmount(rows, OutputIgstMappingKey, igstAmount ?? 0m);

        var explicitTotal = rows.Sum(item => item.Amount);
        var expectedTotal = Math.Max(PositiveAmount(taxAmount), explicitTotal);
        var residual = FinalAccountsJournalRules.RoundAmount(expectedTotal - explicitTotal);
        if (residual <= 0m)
        {
            return rows;
        }

        if (interState)
        {
            AddTaxAmount(rows, OutputIgstMappingKey, residual);
            return rows;
        }

        var half = FinalAccountsJournalRules.RoundAmount(residual / 2m);
        AddTaxAmount(rows, OutputCgstMappingKey, half);
        AddTaxAmount(rows, OutputSgstMappingKey, FinalAccountsJournalRules.RoundAmount(residual - half));
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

    private static void AddSaleRoundOff(List<FinalAccountsPostingPreviewLineRequest> lines, decimal roundOff, string narration)
    {
        var rounded = FinalAccountsJournalRules.RoundAmount(roundOff);
        if (rounded > 0m)
        {
            AddCredit(lines, SalesRoundingMappingKey, rounded, $"{narration} round off");
        }
        else if (rounded < 0m)
        {
            AddDebit(lines, SalesRoundingMappingKey, Math.Abs(rounded), $"{narration} round off");
        }
    }

    private static void AddReturnRoundOff(List<FinalAccountsPostingPreviewLineRequest> lines, decimal roundOff, string narration)
    {
        var rounded = FinalAccountsJournalRules.RoundAmount(roundOff);
        if (rounded > 0m)
        {
            AddDebit(lines, SalesRoundingMappingKey, rounded, $"{narration} round off reversal");
        }
        else if (rounded < 0m)
        {
            AddCredit(lines, SalesRoundingMappingKey, Math.Abs(rounded), $"{narration} round off reversal");
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
            throw new InvalidOperationException($"Sales posting preview is not balanced for {narration}.");
        }
    }
}

public sealed class SalesInvoiceAdapter(GarmetixDbContext db) : FinalAccountsPostingAdapterBase(db)
{
    public override string AdapterKey => "sales-invoice";
    public override string SourceType => "Sales";
    public override string RuleCode => "SalesInvoice";
    public override string DisplayName => "Sales Invoice";
    public override string SourceTable => "SalesInvoices";
    public override string Description => "Debits customer receivable and credits sales revenue, GST and rounding for regular sales invoices.";

    public override async Task<FinalAccountsPostingPreviewRequest> BuildPreviewAsync(Guid sourceId, FinalAccountsScopeDto scope, HttpContext context, CancellationToken cancellationToken)
    {
        _ = context;
        var source = await Db.SalesInvoices.AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == sourceId
                && !item.Deleted
                && !item.ReturnInvoice
                && item.InvoiceType != InvoiceType.Return
                && item.InvoiceStatus != InvoiceStatus.Cancelled,
                cancellationToken)
            ?? throw new ArgumentException("Sales invoice source was not found.");
        var items = await LoadInvoiceItemsAsync(source.Id, cancellationToken);
        var storeGroupId = await ResolveStoreGroupIdAsync(source.StoreId, cancellationToken);
        EnsureScope(scope, source.CompanyId, storeGroupId, source.StoreId);
        var narration = $"Sales invoice {source.InvoiceNumber}";
        var lines = FinalAccountsSalesAdapterLines.SalesInvoiceLines(
            source.BillAmount,
            source.NetAmount,
            source.DiscountAmount,
            source.TaxAmount,
            source.CGSTAmount,
            source.SGSTAmount,
            source.IGSTAmount,
            source.InterState,
            source.RoundOff,
            narration);

        return FinalAccountsPaymentAdapterLines.Request(scope, SourceType, RuleCode, source.Id, source.InvoiceNumber, FinalAccountsSalesAdapterLines.InvoiceHashPayload(source, items), lines);
    }

    private Task<List<InvoiceItem>> LoadInvoiceItemsAsync(Guid invoiceId, CancellationToken cancellationToken)
        => Db.InvoiceItems.AsNoTracking()
            .Where(item => item.InvoiceId == invoiceId && !item.Deleted)
            .OrderBy(item => item.ProductId)
            .ThenBy(item => item.Id)
            .ToListAsync(cancellationToken);

    private Task<Guid?> ResolveStoreGroupIdAsync(Guid storeId, CancellationToken cancellationToken)
        => Db.Stores.AsNoTracking()
            .Where(store => store.Id == storeId && !store.Deleted)
            .Select(store => (Guid?)store.StoreGroupId)
            .FirstOrDefaultAsync(cancellationToken);
}

public sealed class SalesReturnAdapter(GarmetixDbContext db) : FinalAccountsPostingAdapterBase(db)
{
    public override string AdapterKey => "sales-return";
    public override string SourceType => "Sales";
    public override string RuleCode => "SalesReturn";
    public override string DisplayName => "Sales Return";
    public override string SourceTable => "SalesInvoices";
    public override string Description => "Debits sale return and GST reversal while crediting customer receivable for return invoices.";

    public override async Task<FinalAccountsPostingPreviewRequest> BuildPreviewAsync(Guid sourceId, FinalAccountsScopeDto scope, HttpContext context, CancellationToken cancellationToken)
    {
        _ = context;
        var source = await Db.SalesInvoices.AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == sourceId
                && !item.Deleted
                && (item.ReturnInvoice || item.InvoiceType == InvoiceType.Return)
                && item.InvoiceStatus != InvoiceStatus.Cancelled,
                cancellationToken)
            ?? throw new ArgumentException("Sales return source was not found.");
        var items = await LoadInvoiceItemsAsync(source.Id, cancellationToken);
        var storeGroupId = await ResolveStoreGroupIdAsync(source.StoreId, cancellationToken);
        EnsureScope(scope, source.CompanyId, storeGroupId, source.StoreId);
        var narration = $"Sales return {source.InvoiceNumber}";
        var lines = FinalAccountsSalesAdapterLines.SalesReturnLines(
            source.BillAmount,
            source.NetAmount,
            source.DiscountAmount,
            source.TaxAmount,
            source.CGSTAmount,
            source.SGSTAmount,
            source.IGSTAmount,
            source.InterState,
            source.RoundOff,
            narration);

        return FinalAccountsPaymentAdapterLines.Request(scope, SourceType, RuleCode, source.Id, source.InvoiceNumber, FinalAccountsSalesAdapterLines.InvoiceHashPayload(source, items), lines);
    }

    private Task<List<InvoiceItem>> LoadInvoiceItemsAsync(Guid invoiceId, CancellationToken cancellationToken)
        => Db.InvoiceItems.AsNoTracking()
            .Where(item => item.InvoiceId == invoiceId && !item.Deleted)
            .OrderBy(item => item.ProductId)
            .ThenBy(item => item.Id)
            .ToListAsync(cancellationToken);

    private Task<Guid?> ResolveStoreGroupIdAsync(Guid storeId, CancellationToken cancellationToken)
        => Db.Stores.AsNoTracking()
            .Where(store => store.Id == storeId && !store.Deleted)
            .Select(store => (Guid?)store.StoreGroupId)
            .FirstOrDefaultAsync(cancellationToken);
}

public sealed class SalesInvoiceCancellationAdapter(GarmetixDbContext db) : FinalAccountsPostingAdapterBase(db)
{
    public override string AdapterKey => "sales-invoice-cancellation";
    public override string SourceType => "Sales";
    public override string RuleCode => "SalesCancellation";
    public override string DisplayName => "Sales Invoice Cancellation";
    public override string SourceTable => "SalesInvoices";
    public override string Description => "Reverses revenue, GST, discount, rounding and receivable legs for cancelled regular sales invoices.";

    public override async Task<FinalAccountsPostingPreviewRequest> BuildPreviewAsync(Guid sourceId, FinalAccountsScopeDto scope, HttpContext context, CancellationToken cancellationToken)
    {
        _ = context;
        var source = await Db.SalesInvoices.AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == sourceId
                && !item.Deleted
                && !item.ReturnInvoice
                && item.InvoiceType != InvoiceType.Return
                && item.InvoiceStatus == InvoiceStatus.Cancelled,
                cancellationToken)
            ?? throw new ArgumentException("Cancelled sales invoice source was not found.");
        var items = await LoadInvoiceItemsAsync(source.Id, cancellationToken);
        var storeGroupId = await ResolveStoreGroupIdAsync(source.StoreId, cancellationToken);
        EnsureScope(scope, source.CompanyId, storeGroupId, source.StoreId);
        var narration = $"Cancel sales invoice {source.InvoiceNumber}";
        var lines = FinalAccountsSalesAdapterLines.SalesCancellationLines(
            source.BillAmount,
            source.NetAmount,
            source.DiscountAmount,
            source.TaxAmount,
            source.CGSTAmount,
            source.SGSTAmount,
            source.IGSTAmount,
            source.InterState,
            source.RoundOff,
            narration);

        return FinalAccountsPaymentAdapterLines.Request(scope, SourceType, RuleCode, source.Id, source.InvoiceNumber, FinalAccountsSalesAdapterLines.InvoiceHashPayload(source, items), lines);
    }

    private Task<List<InvoiceItem>> LoadInvoiceItemsAsync(Guid invoiceId, CancellationToken cancellationToken)
        => Db.InvoiceItems.AsNoTracking()
            .Where(item => item.InvoiceId == invoiceId && !item.Deleted)
            .OrderBy(item => item.ProductId)
            .ThenBy(item => item.Id)
            .ToListAsync(cancellationToken);

    private Task<Guid?> ResolveStoreGroupIdAsync(Guid storeId, CancellationToken cancellationToken)
        => Db.Stores.AsNoTracking()
            .Where(store => store.Id == storeId && !store.Deleted)
            .Select(store => (Guid?)store.StoreGroupId)
            .FirstOrDefaultAsync(cancellationToken);
}

public sealed class SalesCreditNoteAdapter(GarmetixDbContext db) : FinalAccountsPostingAdapterBase(db)
{
    public override string AdapterKey => "sales-credit-note";
    public override string SourceType => "Sales";
    public override string RuleCode => "SalesReturn";
    public override string DisplayName => "Sales Credit Note";
    public override string SourceTable => "CommercialNotes";
    public override string Description => "Maps customer credit notes to sale return, GST reversal and receivable reduction.";

    public override async Task<FinalAccountsPostingPreviewRequest> BuildPreviewAsync(Guid sourceId, FinalAccountsScopeDto scope, HttpContext context, CancellationToken cancellationToken)
    {
        _ = context;
        var source = await Db.CommercialNotes.AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == sourceId
                && !item.Deleted
                && item.NoteType == NoteType.CreditNote
                && item.PartyType == PartyType.Customer,
                cancellationToken)
            ?? throw new ArgumentException("Sales credit note source was not found.");
        EnsureScope(scope, source.CompanyId, source.StoreGroupId, source.StoreId);
        var narration = $"Sales credit note {source.NoteNumber}";
        var lines = FinalAccountsSalesAdapterLines.SalesReturnLines(
            source.Amount,
            source.TaxableAmount,
            0m,
            source.TaxAmount,
            null,
            null,
            null,
            interState: false,
            roundOff: FinalAccountsJournalRules.RoundAmount(source.Amount - source.TaxableAmount - source.TaxAmount),
            narration);

        return FinalAccountsPaymentAdapterLines.Request(scope, SourceType, RuleCode, source.Id, source.NoteNumber, FinalAccountsSalesAdapterLines.CommercialNoteHashPayload(source), lines);
    }
}
